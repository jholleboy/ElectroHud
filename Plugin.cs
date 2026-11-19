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
                            Visible = visible,
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
                                lsr.Read(ref sample, ped.IsInAnyVehicle(false) ? ped.CurrentVehicle : null);
                                sample.Visible = !settings.HideInMenus || !sample.Menu;
                            }
                            else
                                sample.Visible = false;
                        }
                        lock (Sync)
                            latest = sample;
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
                        renderer?.Draw(e, latest);
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
