using System;
using System.IO;
using System.Threading;
using Rage;
using Rage.Native;

[assembly: Rage.Attributes.Plugin("ElectroHud", Description = "HUD neon para GTA V Legacy", Author = "ElectroHud", PrefersSingleInstance = true, ShouldTickInPauseMenu = true)]

namespace ElectroHud
{
    public static class Plugin
    {
        private static readonly object Sync = new object();
        private static HudRenderer renderer;
        private static Sample latest;
        private static long lastDraw;
        private static int drawnMask;
        private static volatile bool stopping;

        public static void Main()
        {
            stopping = false;
            string directory = Path.Combine("Plugins", "ElectroHud");
            Settings settings = Settings.Load(Path.Combine(directory, "Settings.xml"),
                message => Game.LogTrivial("[ElectroHud] " + message));
            try
            {
                renderer = new HudRenderer(directory, settings);
                using (var lsr = new LosSantosRedReader(settings))
                {
                    bool visible = settings.Enabled;
                    bool keyWasDown = Game.IsKeyDownRightNow(settings.Key);
                    Game.RawFrameRender += OnRender;

                    while (!stopping)
                    {
                        bool keyDown = Game.IsKeyDownRightNow(settings.Key);
                        if (keyDown && !keyWasDown)
                            visible = !visible;
                        keyWasDown = keyDown;
                        var sample = new Sample
                        {
                            Screen = Game.Resolution,
                            SafeZone = Settings.InRange(NativeFunction.Natives.GET_SAFE_ZONE_SIZE<float>(), .8f, 1f, 1f),
                            Visible = visible, SpeedLimit = float.NaN,
                            Health = float.NaN, Armor = float.NaN, Hunger = float.NaN, Thirst = float.NaN,
                            Sleep = float.NaN, Fuel = float.NaN
                        };

                        if (sample.Visible && settings.HideInScenes
                            && (NativeFunction.Natives.IS_CUTSCENE_ACTIVE<bool>()
                                || NativeFunction.Natives.IS_SCREEN_FADED_OUT<bool>()
                                || NativeFunction.Natives.IS_SCREEN_FADING_OUT<bool>()))
                            sample.Visible = false;
                        if (sample.Visible)
                        {
                            sample.Menu = settings.HideInMenus && NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>();
                            var ped = Game.LocalPlayer.Character;
                            if (ped != null && ped.Exists())
                            {
                                // GTA V's normal player health bar represents the points above 100.
                                int max = ped.MaxHealth;
                                int floor = max > 100 ? 100 : 0;
                                if (max > floor)
                                    sample.Health = Settings.InRange((ped.Health - floor) * 100f / (max - floor), 0, 100, float.NaN);
                                int armorCapacity = NativeFunction.Natives.GET_PLAYER_MAX_ARMOUR<int>(
                                    NativeFunction.Natives.PLAYER_ID<int>());
                                if (armorCapacity > 0)
                                    sample.Armor = Settings.InRange(ped.Armor * 100f / armorCapacity, 0, 100, float.NaN);
                                lsr.ReportNative(sample.Health, sample.Armor);
                                var vehicle = ped.IsInAnyVehicle(false) ? ped.CurrentVehicle : null;
                                sample.InVehicle = vehicle != null && vehicle.Exists();
                                if (sample.InVehicle)
                                {
                                    sample.VehicleHandle = (uint)vehicle.Handle;
                                    sample.Speed = vehicle.Speed * 3.6f;
                                }
                                sample.Wanted = NativeFunction.Natives.GET_PLAYER_WANTED_LEVEL<int>(
                                    NativeFunction.Natives.PLAYER_ID<int>());
                                lsr.Read(ref sample, vehicle);
                                sample.Visible = !settings.HideInMenus || !sample.Menu;
                            }
                            else
                                sample.Visible = false;
                        }
                        int mask;
                        long acknowledged;
                        lock (Sync)
                        {
                            if (sample.Visible && settings.ShowTerritory)
                                renderer.PrepareTerritory(sample);
                            var age = System.Diagnostics.Stopwatch.GetTimestamp() - lastDraw;
                            mask = sample.Visible && settings.SuppressReplacedLsrIndicators
                                && age < System.Diagnostics.Stopwatch.Frequency / 2 ? drawnMask : 0;
                            if (float.IsNaN(sample.Hunger)) mask &= ~8;
                            if (float.IsNaN(sample.Thirst)) mask &= ~16;
                            if (float.IsNaN(sample.Sleep)) mask &= ~32;
                            if (float.IsNaN(sample.Fuel) || latest.VehicleHandle != sample.VehicleHandle) mask &= ~4;
                            if (latest.VehicleHandle != sample.VehicleHandle) mask &= ~(256 | 1024 | 2048);
                            acknowledged = latest.BridgeStamp;
                            if (sample.BridgeStamp == 0 || acknowledged == 0) mask = 0;
                            drawnMask = 0;
                            latest = sample;
                        }
                        lsr.PublishVisibility(mask, sample.VehicleHandle, acknowledged);
                        GameFiber.Sleep(50);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                Game.LogTrivial("[ElectroHud] " + e);
            }
            finally
            {
                Finally();
            }
        }

        public static void Finally()
        {
            stopping = true;
            Game.RawFrameRender -= OnRender;
            lock (Sync)
            {
                latest = default(Sample);
                renderer?.Dispose();
                renderer = null;
            }
        }

        private static void OnRender(object sender, GraphicsEventArgs e)
        {
            try
            {
                lock (Sync)
                {
                    if (!stopping && latest.Visible)
                    {
                        drawnMask = renderer?.Draw(e, latest) ?? 0;
                        lastDraw = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
            }
            catch (Exception error)
            {
                stopping = true;
                Game.LogTrivial("[ElectroHud] Renderização interrompida: " + error.Message);
            }
        }
    }
}
