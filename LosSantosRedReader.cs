using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Rage;

namespace ElectroHud
{
    // The reference project exposes public members, but does not publish a plugin API.
    internal sealed class LosSantosRedReader : IDisposable
    {
        private const string AssemblyName = "Los Santos RED";
        private readonly Dictionary<Type, Dictionary<string, MemberInfo>> members =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();
        private PropertyInfo controller;
        private long nextProbe;
        private bool assemblyDetected;
        private readonly Settings settings;
        private readonly Dictionary<string, string> lastStatus = new Dictionary<string, string>();
        private readonly string channel;
        private MemoryMappedFile map;
        private MemoryMappedViewAccessor view;
        private Mutex mutex;
        private long nextBridgeProbe;
        private BridgeSnapshot cached;

        private struct BridgeSnapshot
        {
            public long Timestamp;
            public int Flags;
            public uint Handle;
            public float Hunger, Thirst, Sleep, Fuel, Limit;
            public int Wanted;
            public string Region, Street, Agency, Gang, Pursuit, Recognition;
        }

        private const int Magic = 0x45485233;
        private const int Size = 1232;

        public LosSantosRedReader(Settings settings)
        {
            this.settings = settings;
            using (var process = Process.GetCurrentProcess())
                channel = @"Local\ElectroHud.Lsr.v3." + process.Id;
            Report("Estresse", "oculto: o LS RED de referência não fornece esta estatística");
        }

        public void ReportNative(float health, float armor)
        {
            ReportValue("Saúde", settings.Health, health, "Ped.Health / Ped.MaxHealth", "pedestre ou vida máxima indisponível");
            ReportValue("Colete", settings.Armor, armor, "Ped.Armor / GET_PLAYER_MAX_ARMOUR", "capacidade indisponível");
        }

        public void Read(ref Sample sample, Vehicle currentVehicle)
        {
            uint handle = currentVehicle != null && currentVehicle.Exists() ? (uint)currentVehicle.Handle : 0u;
            Report("Veículo", handle == 0 ? "jogador fora de veículo" : "jogador em veículo");

            try
            {
                if (ReadBridge(ref sample, handle))
                    return;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException)
            {
                Report("Ponte", "falha de leitura: " + e.GetBaseException().Message);
                Dispose();
            }

            try
            {
                Probe();
                Report("Detecção LS RED", controller == null
                    ? (assemblyDetected ? "assembly detectada, EntryPoint.ModController indisponível"
                        : "assembly não encontrada no domínio do ElectroHud")
                    : "assembly detectada; EntryPoint.ModController encontrado");
                object mod = controller?.GetValue(null, null);
                object player = Flag(mod, "IsRunning") ? Get(mod, "Player") : null;
                if (player == null)
                {
                    Report("Instância LS RED", mod == null ? "controlador não instanciado"
                        : !Flag(mod, "IsRunning") ? "controlador presente, LS RED ainda inativo"
                        : "controlador ativo, mas Player indisponível");
                    ReportMissing("instância ativa indisponível", handle);
                    return;
                }
                Report("Instância LS RED", "Player encontrado por reflexão pública");
                sample.Menu |= Flag(mod, "IsDisplayingAlertScreen")
                    || Flag(player, "IsShowingFrontEndMenus") || Flag(player, "IsDisplayingCustomMenus");
                object location = Get(player, "CurrentLocation");
                object zone = Get(location, "CurrentZone");
                sample.Region = Get(zone, "DisplayName") as string;
                sample.Street = Get(Get(location, "CurrentStreet"), "ProperStreetName") as string;
                sample.Agency = Get(Get(zone, "AssignedLEAgency"), "ShortName") as string;
                sample.Gang = Get(Get(zone, "AssignedGang"), "ShortName") as string;
                if (Get(player, "WantedLevel") is int wanted) sample.Wanted = wanted;
                sample.Searching = Flag(player, "IsInSearchMode");
                object manager = Get(Get(Get(mod, "ModDataFileManager"), "Settings"), "SettingsManager");
                object needsSettings = Get(manager, "NeedsSettings");
                object needs = Get(player, "HumanState");
                sample.Hunger = Need(needs, needsSettings, "Hunger", "Fome", settings.Hunger);
                sample.Thirst = Need(needs, needsSettings, "Thirst", "Sede", settings.Thirst);
                sample.Sleep = Need(needs, needsSettings, "Sleep", "Sono", settings.Sleep);

                object vehicleSettings = Get(manager, "VehicleSettings");
                bool active = Flag(vehicleSettings, "UseCustomFuelSystem");
                Report("Sistema de combustível", vehicleSettings == null ? "VehicleSettings indisponível" : active ? "ativo" : "desativado");
                object vehicle = Get(player, "CurrentVehicle");
                string reason = handle == 0 ? "jogador fora de veículo" : "sistema ausente ou desativado";
                if (handle != 0 && active)
                {
                    reason = "veículo do LS RED ainda não localizado ou diferente do veículo real";
                    if (Get(vehicle, "Vehicle") is Vehicle tracked && tracked.Exists() && (uint)tracked.Handle == handle)
                    {
                        reason = "RequiresFuel falso ou indisponível";
                        if (Flag(vehicle, "RequiresFuel"))
                        {
                            reason = "capacidade ou nível indisponível";
                            if (Get(vehicleSettings, "CustomFuelSystemFuelMax") is float capacity && capacity > 0)
                                sample.Fuel = Settings.InRange(tracked.FuelLevel * 100f / capacity, 0, 100, float.NaN);
                        }
                    }
                }
                ReportValue("Combustível", settings.Fuel, sample.Fuel,
                    "Vehicle.FuelLevel / VehicleSettings.CustomFuelSystemFuelMax", reason);
            }
            catch (Exception e)
            {
                sample.Hunger = sample.Thirst = sample.Sleep = sample.Fuel = float.NaN;
                Report("Instância LS RED", "falha de leitura: " + e.GetBaseException().Message);
                ReportMissing("falha de leitura", handle);
            }
        }

        private float Need(object needs, object options, string name, string label, IndicatorSettings option)
        {
            string reason = options == null ? "NeedsSettings indisponível"
                : !Flag(options, "ApplyNeeds") ? "ApplyNeeds desativado ou ausente"
                : !Flag(options, "Apply" + name) ? "Apply" + name + " desativado ou ausente"
                : "HumanState ou CurrentValue indisponível";
            float value = Flag(options, "ApplyNeeds") && Flag(options, "Apply" + name)
                && Get(Get(needs, name), "CurrentValue") is float raw
                ? Settings.InRange(raw, 0, 100, float.NaN) : float.NaN;
            ReportValue(label, option, value, "HumanState." + name + ".CurrentValue", reason);
            return value;
        }

        private bool ReadBridge(ref Sample sample, uint handle)
        {
            long now = Stopwatch.GetTimestamp();
            if (view == null && now >= nextBridgeProbe)
            {
                nextBridgeProbe = now + Stopwatch.Frequency * 2;
                try
                {
                    mutex = Mutex.OpenExisting(channel + ".Lock");
                    map = MemoryMappedFile.OpenExisting(channel, MemoryMappedFileRights.ReadWrite);
                    view = map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
                }
                catch (FileNotFoundException) { Dispose(); }
                catch (WaitHandleCannotBeOpenedException) { Dispose(); }
                catch { Dispose(); throw; }
            }
            if (view == null)
            {
                Report("Ponte", "publicador não encontrado");
                return false;
            }

            RefreshBridgeSnapshot();
            var snapshot = cached;
            long age = Stopwatch.GetTimestamp() - snapshot.Timestamp;
            if (snapshot.Timestamp <= 0 || age < 0 || age > Stopwatch.Frequency * 2)
            {
                Report("Ponte", "snapshot vencido ou protocolo incompatível");
                return false;
            }

            int flags = snapshot.Flags;
            uint tracked = snapshot.Handle;
            Report("Ponte", "snapshot v3 recebido");
            Report("Detecção LS RED", "detectado pelo publicador IPC");
            Report("Instância LS RED", "Player ativo confirmado pelo publicador");
            sample.Menu |= (flags & 1) != 0;
            sample.Searching = (flags & 64) != 0;
            sample.Wanted = snapshot.Wanted;
            sample.BridgeStamp = snapshot.Timestamp;
            sample.Pursuit = snapshot.Pursuit;
            sample.Region = snapshot.Region;
            sample.Street = snapshot.Street;
            sample.Agency = snapshot.Agency;
            sample.Gang = snapshot.Gang;
            bool sameVehicle = handle != 0 && handle == tracked;
            if (sameVehicle)
                sample.Recognition = snapshot.Recognition;
            if (sameVehicle && (flags & 128) != 0 && snapshot.Limit > 0 && !float.IsInfinity(snapshot.Limit))
                sample.SpeedLimit = snapshot.Limit;
            Report("Limite de velocidade", !sameVehicle ? "sem veículo rastreado correspondente"
                : (flags & 256) == 0 ? "tipo de veículo incompatível"
                : (flags & 512) != 0 ? "rua sem cadastro (valor padrão do LS RED)"
                : float.IsNaN(sample.SpeedLimit) ? "limite cadastrado indisponível"
                : "limite confirmado pelo cadastro de ruas do LS RED");
            sample.Hunger = BridgeNeed("Fome", settings.Hunger, snapshot.Hunger, flags, 8, "Hunger");
            sample.Thirst = BridgeNeed("Sede", settings.Thirst, snapshot.Thirst, flags, 16, "Thirst");
            sample.Sleep = BridgeNeed("Sono", settings.Sleep, snapshot.Sleep, flags, 32, "Sleep");

            bool active = (flags & 2) != 0;
            bool compatible = (flags & 4) != 0;
            Report("Sistema de combustível", active ? "ativo (ponte)" : "desativado (ponte)");
            if (handle != 0 && handle == tracked && active && compatible)
                sample.Fuel = Settings.InRange(snapshot.Fuel, 0, 100, float.NaN);
            ReportValue("Combustível", settings.Fuel, sample.Fuel, "ponte: Vehicle.FuelLevel / CustomFuelSystemFuelMax",
                handle == 0 ? "jogador fora de veículo" : !active ? "sistema desativado"
                : tracked != handle ? "veículo ainda não rastreado pelo LS RED" : !compatible
                ? "RequiresFuel falso" : "capacidade ou nível indisponível");
            return true;
        }

        private void RefreshBridgeSnapshot()
        {
            bool held = false;
            try
            {
                try { held = mutex.WaitOne(0); }
                catch (AbandonedMutexException)
                {
                    held = true;
                    cached = default(BridgeSnapshot);
                    return; // A previous writer may have stopped mid-write.
                }
                if (!held)
                    return;
                long stamp = view.ReadInt64(8);
                if (cached.Timestamp == stamp) return;
                cached = view.ReadInt32(0) == Magic && stamp > 0 ? new BridgeSnapshot
                {
                    Timestamp = stamp,
                    Handle = view.ReadUInt32(4),
                    Hunger = view.ReadSingle(16),
                    Thirst = view.ReadSingle(20),
                    Sleep = view.ReadSingle(24),
                    Fuel = view.ReadSingle(28),
                    Flags = view.ReadInt32(32),
                    Wanted = view.ReadInt32(36),
                    Limit = view.ReadSingle(40),
                    Region = ReadText(64),
                    Street = ReadText(256),
                    Agency = ReadText(448),
                    Gang = ReadText(640),
                    Pursuit = ReadText(840),
                    Recognition = ReadText(1032)
                } : default(BridgeSnapshot);
            }
            finally
            {
                if (held)
                    mutex.ReleaseMutex();
            }
        }

        private readonly byte[] textBuffer = new byte[192];

        private string ReadText(long offset)
        {
            view.ReadArray(offset, textBuffer, 0, textBuffer.Length);
            int length = Array.IndexOf(textBuffer, (byte)0);
            return System.Text.Encoding.UTF8.GetString(textBuffer, 0, length < 0 ? textBuffer.Length : length);
        }

        public void PublishVisibility(int mask, uint vehicleHandle, long snapshot = 0)
        {
            if (view == null)
                return;
            bool held = false;
            try
            {
                try { held = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { held = true; mask = 0; }
                if (!held)
                    return;
                view.Write(832, mask);
                view.Write(836, vehicleHandle);
                view.Write(1224, snapshot);
                view.Write(48, mask == 0 ? 0L : Stopwatch.GetTimestamp());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException)
            {
                Report("Ponte", "falha no sinal de visibilidade: " + e.Message);
            }
            finally
            {
                if (held) mutex.ReleaseMutex();
            }
        }

        private float BridgeNeed(string label, IndicatorSettings options, float value, int flags, int bit, string member)
        {
            float percent = (flags & bit) != 0 ? Settings.InRange(value, 0, 100, float.NaN) : float.NaN;
            ReportValue(label, options, percent, "ponte: HumanState." + member + ".CurrentValue",
                (flags & bit) == 0 ? "necessidade desativada no LS RED" : "CurrentValue indisponível");
            return percent;
        }

        private void ReportMissing(string reason, uint handle)
        {
            ReportValue("Fome", settings.Hunger, float.NaN, null, reason);
            ReportValue("Sede", settings.Thirst, float.NaN, null, reason);
            ReportValue("Sono", settings.Sleep, float.NaN, null, reason);
            Report("Sistema de combustível", "indisponível: " + reason);
            ReportValue("Combustível", settings.Fuel, float.NaN, null, handle == 0 ? "jogador fora de veículo" : reason);
        }

        private void ReportValue(string name, IndicatorSettings option, float value, string source, string missing)
        {
            Report(name, option == null || !option.Enabled ? "oculto pela configuração do ElectroHud"
                : float.IsNaN(value) ? "oculto: " + missing : "localizado em " + source);
        }

        private void Report(string category, string message)
        {
            if (!settings.Diagnostics || (lastStatus.TryGetValue(category, out string previous) && previous == message))
                return;
            lastStatus[category] = message;
            Game.LogTrivial("[ElectroHud] " + category + ": " + message);
        }

        public void Dispose()
        {
            PublishVisibility(0, 0);
            view?.Dispose();
            map?.Dispose();
            mutex?.Dispose();
            cached = default(BridgeSnapshot);
            view = null;
            map = null;
            mutex = null;
        }

        private void Probe()
        {
            if (controller != null)
                return;
            long now = Stopwatch.GetTimestamp();
            if (now < nextProbe)
                return;
            nextProbe = now + 3 * System.Diagnostics.Stopwatch.Frequency;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;
                assemblyDetected = true;
                controller = assembly.GetType("EntryPoint", false)
                    ?.GetProperty("ModController", BindingFlags.Public | BindingFlags.Static);
                if (controller != null)
                    break;
            }
        }

        private bool Flag(object target, string name)
        {
            return Get(target, name) is bool value && value;
        }

        private object Get(object target, string name)
        {
            if (target == null)
                return null;
            Type type = target.GetType();
            if (!members.TryGetValue(type, out var cache))
                members.Add(type, cache = new Dictionary<string, MemberInfo>(StringComparer.Ordinal));
            if (!cache.TryGetValue(name, out var member))
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                member = (MemberInfo)type.GetProperty(name, flags) ?? type.GetField(name, flags);
                cache.Add(name, member);
            }
            if (member is PropertyInfo property && property.GetGetMethod() != null)
                return property.GetValue(target, null);
            return (member as FieldInfo)?.GetValue(target);
        }
    }
}
