using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ElectroHud
{
    public enum Indicator { Health, Hunger, Thirst, Sleep, Stress, Fuel, Armor }

    public sealed class IndicatorSettings
    {
        public bool Enabled { get; set; } = true;
        public string Color { get; set; }
        public int CriticalPercent { get; set; } = 25;

        internal Color GetColor(string fallback)
        {
            return ParseColor(Color) ?? ParseColor(fallback).Value;
        }

        private static Color? ParseColor(string value)
        {
            if (value == null || value.Length != 7 || value[0] != '#'
                || !int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return null;
            return System.Drawing.Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }
    }

    [XmlRoot("ElectroHud")]
    public sealed class Settings
    {
        public bool Enabled { get; set; } = true;
        public float RightMargin { get; set; } = .018f;
        public float TopOffset { get; set; } = .125f;
        public float BottomMargin { get; set; } = .045f;
        public float Scale { get; set; } = .75f;
        public bool ShowTerritory { get; set; } = true;
        public bool ShowVehicle { get; set; } = true;
        public bool SuppressReplacedLsrIndicators { get; set; } = true;
        public float Opacity { get; set; } = .86f;
        public bool Diagnostics { get; set; }
        public bool Animate { get; set; } = true;
        public bool HideInMenus { get; set; } = true;
        public bool HideInScenes { get; set; } = true;
        public string ToggleKey { get; set; } = "F9";
        public string Cyan { get; set; } = "#33ECFF";
        public string Magenta { get; set; } = "#F354F8";
        public IndicatorSettings Health { get; set; } = new IndicatorSettings { Color = "#50FF92" };
        public IndicatorSettings Armor { get; set; } = new IndicatorSettings { Color = "#82D1FF" };
        public IndicatorSettings Hunger { get; set; } = new IndicatorSettings { Color = "#FFA94F" };
        public IndicatorSettings Thirst { get; set; } = new IndicatorSettings { Color = "#36E6FF" };
        public IndicatorSettings Sleep { get; set; } = new IndicatorSettings { Color = "#AC7DFF" };
        public IndicatorSettings Stress { get; set; } = new IndicatorSettings { Color = "#FF68BA" };
        public IndicatorSettings Fuel { get; set; } = new IndicatorSettings { Color = "#5488FF", CriticalPercent = 15 };

        internal IndicatorSettings[] Indicators => new[] { Health, Hunger, Thirst, Sleep, Stress, Fuel, Armor };
        internal Keys Key => Enum.TryParse(ToggleKey, true, out Keys key) && Enum.IsDefined(typeof(Keys), key) ? key : Keys.F9;

        internal static Settings Load(string path, Action<string> log)
        {
            if (!File.Exists(path))
                return new Settings();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var result = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                    result.RightMargin = InRange(result.RightMargin, 0, .3f, .018f);
                    result.TopOffset = InRange(result.TopOffset, .125f, .42f, .125f);
                    result.BottomMargin = InRange(result.BottomMargin, 0, .23f, .045f);
                    result.Scale = InRange(result.Scale, .35f, 2, .75f);
                    result.Opacity = InRange(result.Opacity, 0, 1, .86f);
                    return result;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                log("Settings.xml: " + e.Message);
                return new Settings();
            }
        }

        internal static float InRange(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(max, Math.Max(min, value));
        }
    }

    internal struct Sample
    {
        public bool Visible;
        public bool Menu, InVehicle, Searching, RoadVehicle;
        public uint VehicleHandle;
        public int Wanted;
        public float Speed, SpeedLimit;
        public string Region, Street, Agency, Gang;
        public float SafeZone;
        public Size Screen;
        public float Health, Hunger, Thirst, Sleep, Fuel, Armor;

        public float Value(Indicator kind)
        {
            switch (kind)
            {
                case Indicator.Health: return Health;
                case Indicator.Armor: return Armor;
                case Indicator.Hunger: return Hunger;
                case Indicator.Thirst: return Thirst;
                case Indicator.Sleep: return Sleep;
                case Indicator.Fuel: return Fuel;
                default: return float.NaN;
            }
        }
    }
}
