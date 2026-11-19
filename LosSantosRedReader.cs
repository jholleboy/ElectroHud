using System;
using System.Collections.Generic;
using System.Reflection;
using Rage;

namespace ElectroHud
{
    // The reference project exposes public members, but does not publish a plugin API.
    internal sealed class LosSantosRedReader
    {
        private const string AssemblyName = "Los Santos RED";
        private readonly Dictionary<Type, Dictionary<string, MemberInfo>> members =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();
        private PropertyInfo controller;
        private long nextProbe;
        private bool reportedReadFailure;

        public void Read(ref Sample sample, Vehicle currentVehicle)
        {
            if (controller == null)
                Probe();
            if (controller == null)
                return;

            try
            {
                object mod = controller.GetValue(null, null);
                if (!Flag(mod, "IsRunning"))
                    return;

                object player = Get(mod, "Player");
                sample.Menu |= Flag(mod, "IsDisplayingAlertScreen")
                    || Flag(player, "IsShowingFrontEndMenus") || Flag(player, "IsDisplayingCustomMenus");
                object manager = Get(Get(Get(mod, "ModDataFileManager"), "Settings"), "SettingsManager");
                object needsSettings = Get(manager, "NeedsSettings");
                if (Flag(needsSettings, "ApplyNeeds"))
                {
                    object needs = Get(player, "HumanState");
                    sample.Hunger = Need(needs, needsSettings, "Hunger");
                    sample.Thirst = Need(needs, needsSettings, "Thirst");
                    sample.Sleep = Need(needs, needsSettings, "Sleep");
                }

                object vehicleSettings = Get(manager, "VehicleSettings");
                object vehicle = Get(player, "CurrentVehicle");
                if (currentVehicle != null && currentVehicle.Exists()
                    && Flag(vehicleSettings, "UseCustomFuelSystem") && Flag(vehicle, "RequiresFuel")
                    && Get(vehicle, "Vehicle") is Vehicle tracked && tracked.Exists() && tracked.Handle == currentVehicle.Handle
                    && Get(vehicleSettings, "CustomFuelSystemFuelMax") is float capacity && capacity > 0)
                {
                    float percent = tracked.FuelLevel * 100f / capacity;
                    sample.Fuel = Settings.InRange(percent, 0, 100, float.NaN);
                }
                reportedReadFailure = false;
            }
            catch (Exception e)
            {
                // A concurrent unload or a different LSR build may invalidate optional reads.
                sample.Hunger = sample.Thirst = sample.Sleep = sample.Fuel = float.NaN;
                if (!reportedReadFailure)
                {
                    Game.LogTrivial("[ElectroHud] Leitura do Los Santos RED indisponível: " + e.GetBaseException().Message);
                    reportedReadFailure = true;
                }
            }
        }

        private float Need(object needs, object settings, string name)
        {
            if (!Flag(settings, "Apply" + name))
                return float.NaN;
            return Get(Get(needs, name), "CurrentValue") is float value
                ? Settings.InRange(value, 0, 100, float.NaN) : float.NaN;
        }

        private void Probe()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now < nextProbe)
                return;
            nextProbe = now + 3 * System.Diagnostics.Stopwatch.Frequency;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;
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
