using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Rage;

namespace ElectroHud
{
    internal sealed class HudRenderer : IDisposable
    {
        private const int Segments = 40;
        private const float CardWidth = 108;
        private const float PanelHeight = 111;
        private const float NeedsHeight = 99;
        private static readonly string[] Names = { "Saúde", "Fome", "Sede", "Sono", "Estresse", "Combustível", "Colete" };
        private static readonly string[] Icons = { "health", "hunger", "thirst", "sleep", "stress", "fuel", "armor" };
        private static readonly string[] Defaults = { "#50FF92", "#FFA94F", "#36E6FF", "#AC7DFF", "#FF68BA", "#5488FF", "#82D1FF" };
        private readonly PointF[] ring = new PointF[Segments];
        private readonly Gauge[] gauges = new Gauge[Names.Length];
        private readonly List<Texture> textures = new List<Texture>();
        private readonly Settings settings;
        private readonly Color cyan, magenta, background, edge;
        private readonly string cache;
        private static readonly int[] Order = { 0, 6, 1, 2, 3 };
        private long previousFrame;
        private float TerritoryWidth => settings.TerritoryMaxWidth;
        private readonly float[] rowHeights = new float[7];
        private readonly string[] territoryText = new string[7];
        private readonly Texture[] territory = new Texture[7];
        private readonly Texture[] vehicleDigits = new Texture[10];
        private readonly Texture[] warningDigits = new Texture[10];
        private Texture speedUnit;
        private readonly bool[] fullTerritoryRow = new bool[7];
        private static readonly int[] TerritoryOrder = { 3, 4, 2, 0, 1, 5, 6 };
        private static readonly int[] TerritoryBits = { 64, 128, 0, 512, 1024, 0, 0 };
        private static readonly System.Text.RegularExpressions.Regex GtaFormatting =
            new System.Text.RegularExpressions.Regex(@"~[a-zA-Z0-9_]+~");
        private int textureSerial;


        private sealed class Gauge
        {
            public Indicator Kind;
            public Color Color;
            public Color Track;
            public Color Glow;
            public int Critical;
            public Texture Icon;
            public Texture Label;
            public Texture[] Glyphs;
            public float Display = float.NaN;
        }

        public HudRenderer(string directory, Settings settings)
        {
            this.settings = settings;
            cyan = WithOpacity(new IndicatorSettings { Color = settings.Cyan }.GetColor("#33ECFF"), .88f);
            magenta = WithOpacity(new IndicatorSettings { Color = settings.Magenta }.GetColor("#F354F8"), .88f);
            background = WithOpacity(Color.FromArgb(9, 15, 28), .96f);
            edge = WithOpacity(Color.FromArgb(30, 56, 79), .75f);
            cache = Path.Combine(Path.GetTempPath(), "ElectroHud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cache);
            try
            {
                for (int i = 0; i < vehicleDigits.Length; i++)
                {
                    vehicleDigits[i] = Text("speed-" + i, i.ToString(), WithOpacity(Color.White, 1), 58, 43, 69, true);
                    warningDigits[i] = Text("speed-warning-" + i, i.ToString(), WithOpacity(Color.Tomato, 1), 58, 43, 69, true);
                }
                speedUnit = Text("speed-unit", "km/h", WithOpacity(Color.White, .9f), 24, 120, 36);

                var options = settings.Indicators;
                for (int i = 0; i < options.Length; ++i)
                {
                    var option = options[i];
                    // There is no verified source for Stress in the reference build.
                    if (option == null || !option.Enabled || i == (int)Indicator.Stress)
                        continue;
                    Color color = option.GetColor(Defaults[i]);
                    var gauge = new Gauge
                    {
                        Kind = (Indicator)i,
                        Color = WithOpacity(color, 1f),
                        Track = WithOpacity(color, .21f),
                        Glow = WithOpacity(color, .16f),
                        Critical = Math.Min(100, Math.Max(0, option.CriticalPercent)),
                        Icon = LoadIcon(directory, Icons[i]),
                        Label = i == (int)Indicator.Fuel ? Text(Icons[i] + "-label", Names[i], WithOpacity(Color.White, .96f), 19, 184, 42) : null,
                        Glyphs = new Texture[11]
                    };
                    for (int j = 0; j < gauge.Glyphs.Length; ++j)
                    {
                        gauge.Glyphs[j] = Text(Icons[i] + "-" + j, j == 10 ? "%" : j.ToString(),
                            gauge.Color, 27, 34, 45, true);
                    }
                    gauges[i] = gauge;
                }
            }
            catch
            {
                Dispose();
                throw;
            }

            for (int i = 0; i < Segments; ++i)
            {
                double angle = i * Math.PI * 2 / Segments - Math.PI / 2;
                ring[i] = new PointF((float)Math.Cos(angle) * 26, (float)Math.Sin(angle) * 26);
            }
        }

        public void PrepareTerritory(Sample sample)
        {
            if (settings.ShowTerritory)
            {
                string region = PlainText(sample.Region);
                SetRow(0, region, Color.White, CanReplace(sample.Region));
                SetRow(1, PlainText(sample.Street), Color.Gainsboro, CanReplace(sample.Street));
                SetRow(2, sample.Wanted <= 0 ? "Sem nível de procurado" :
                    "Procurado: nível " + sample.Wanted + (sample.Searching ? " · Em busca" : ""), Color.White);
                SetRow(3, PlainText(sample.Pursuit), Color.Orange, CanReplace(sample.Pursuit));
                SetRow(4, PlainText(sample.VehicleContext), sample.VehicleContext?.Contains("~r~") == true
                    ? Color.Tomato : Color.Orange, CanReplace(sample.VehicleContext));
                SetRow(5, AdditionalName(region, sample.Agency), Color.DeepSkyBlue);
                SetRow(6, AdditionalName(region, sample.Gang), Color.Orchid);
            }

            int limit = settings.ShowVehicle && sample.InVehicle && sample.SpeedLimit > 0 && sample.SpeedLimit <= 999
                ? (int)Math.Round(sample.SpeedLimit, MidpointRounding.AwayFromZero) : 0;
            SetText(ref limitText, ref limitTexture, limit == 0 ? null : "Limite: " + limit + " km/h",
                WithOpacity(Color.White, .94f), 23, 296, 34, false, false, out _);
        }

        private string limitText;
        private Texture limitTexture;

        private static string PlainText(string raw)
        {
            return raw == null ? null : GtaFormatting.Replace(raw.Replace("~n~", " "), "").Trim();
        }

        private static string AdditionalName(string region, string name)
        {
            string label = PlainText(name);
            return string.IsNullOrEmpty(label) || (region != null
                && region.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0) ? null : label;
        }

        private static bool CanReplace(string raw)
        {
            if (raw == null)
                return false;
            foreach (System.Text.RegularExpressions.Match token in GtaFormatting.Matches(raw))
            {
                if (token.Length != 3 && !token.Value.StartsWith("~HUD_COLOUR_", StringComparison.OrdinalIgnoreCase))
                    return false; // Preserve native icons, input glyphs and other commands.
            }
            return true;
        }

        private readonly bool[] replaceableRows = new bool[7];

        private void SetRow(int index, string value, Color color, bool replaceable = false)
        {
            replaceableRows[index] = replaceable;
            if (territoryText[index] == value)
                return;
            Texture next = null;
            float height = 0;
            bool whole = false;
            if (!string.IsNullOrWhiteSpace(value))
            {
                int width = (settings.TerritoryMaxWidth - 32) * 2;
                const int maxHeight = 180;
                string name = "territory-" + textureSerial++;
                string path = Path.Combine(cache, name + ".png");
                using (var image = new Bitmap(width, maxHeight, PixelFormat.Format32bppArgb))
                using (var graphics = System.Drawing.Graphics.FromImage(image))
                using (var font = new Font("Consolas", 34, index == 0 ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(WithOpacity(color, 1)))
                using (var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                {
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var measured = graphics.MeasureString(value, font, new SizeF(width, maxHeight), format,
                        out int fitted, out _);
                    int pixels = Math.Max(44, Math.Min(maxHeight, (int)Math.Ceiling(measured.Height) + 2));
                    graphics.MeasureString(value, font, new SizeF(width, pixels), format, out fitted, out _);
                    whole = fitted == value.Length;
                    graphics.DrawString(value, font, brush, new RectangleF(0, 0, width, pixels), format);
                    using (var cropped = image.Clone(new Rectangle(0, 0, width, pixels), PixelFormat.Format32bppArgb))
                        cropped.Save(path, ImageFormat.Png);
                    height = pixels / 2f;
                }
                next = Load(path);
                DeleteCachedText(name);
            }
            ReplaceTexture(ref territory[index], next);
            territoryText[index] = value;
            fullTerritoryRow[index] = whole;
            rowHeights[index] = height;
        }

        private void SetText(ref string current, ref Texture texture, string value, Color color, float fontSize,
            int width, int height, bool bold, bool left, out bool whole)
        {
            whole = false;
            if (current == value)
                return;
            string name = "dynamic-" + textureSerial++;
            Texture next = string.IsNullOrWhiteSpace(value) ? null :
                Text(name, value, color, fontSize, width, height, bold, left, out whole);
            if (next != null)
                DeleteCachedText(name);
            ReplaceTexture(ref texture, next);
            current = value;
        }

        private void ReplaceTexture(ref Texture texture, Texture next)
        {
            if (texture != null)
            {
                textures.Remove(texture);
                if ((object)texture is IDisposable disposable)
                    disposable.Dispose();
            }
            texture = next;
        }

        private void DeleteCachedText(string name)
        {
            try { File.Delete(Path.Combine(cache, name + ".png")); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public int Draw(GraphicsEventArgs frame, Sample sample)
        {
            float width = sample.Screen.Width, height = sample.Screen.Height;
            if (width <= 0 || height <= 0 || settings.Opacity <= 0)
                return 0;

            long now = Stopwatch.GetTimestamp();
            float elapsed = previousFrame == 0 ? .05f : Math.Min(.12f, (now - previousFrame) / (float)Stopwatch.Frequency);
            previousFrame = now;
            float progress = settings.Animate ? 1 - (float)Math.Exp(-12 * elapsed) : 1f;
            int count = 0;
            float rowHeight = 0;
            foreach (int slot in Order)
                if (Available(sample, slot))
                    count++;
            foreach (float row in rowHeights)
                if (row > 0)
                    rowHeight += row + settings.TerritoryLineSpacing;

            bool vehicle = settings.ShowVehicle && sample.InVehicle && !float.IsNaN(sample.Speed);
            bool fuel = vehicle && Available(sample, (int)Indicator.Fuel);
            bool limit = vehicle && limitTexture != null;
            float needsWidth = count * CardWidth + 22;
            float vehicleWidth = 156 + (fuel ? 108 : 0);
            float territoryHeight = rowHeight == 0 ? 0 : 16 + rowHeight;
            float right = width * (1 - (1 - sample.SafeZone) / 2 - settings.RightMargin);
            float top = height * ((1 - sample.SafeZone) / 2 + settings.TopOffset);
            float bottom = height * (1 - (1 - sample.SafeZone) / 2 - settings.BottomMargin);
            float scale = Math.Min(width / 1920f, height / 1080f) * settings.Scale;
            // Match the V4 vehicle anchor and sizing independently of the territory settings.
            scale = Math.Min(scale, width * .48f / Math.Max(needsWidth, 405));
            float vehicleScale = scale;
            float needsHeight = count > 0 ? (NeedsHeight + 9) * scale : 0;
            float availableHeight = bottom - top - needsHeight - (vehicle ? (PanelHeight + 12) * vehicleScale : 0);
            float territoryScale = Math.Min(scale / settings.Scale * settings.TerritoryScale,
                Math.Min(availableHeight / Math.Max(1, territoryHeight), width * .48f / TerritoryWidth));

            int mask = 0;
            if (count > 0)
            {
                float left = right - needsWidth * scale;
                DrawPanel(frame, left, top, needsWidth, scale, NeedsHeight);
                int index = 0;
                foreach (int slot in Order)
                {
                    if (!Available(sample, slot))
                        continue;
                    DrawValue(frame, gauges[slot], sample, left + (11 + (index++ + .5f) * CardWidth) * scale, top, scale, now, progress);
                    if (slot >= 1 && slot <= 3)
                        mask |= 1 << (slot + 2);
                }
                top += (NeedsHeight + 9) * scale;
            }
            if (territoryHeight > 0 && territoryScale >= .35f)
            {
                float left = right - TerritoryWidth * territoryScale;
                DrawPanel(frame, left, top, TerritoryWidth, territoryScale, territoryHeight);
                float y = 8;
                foreach (int i in TerritoryOrder)
                {
                    if (territory[i] == null)
                        continue;
                    Sprite(frame, territory[i], left, top, territoryScale, 16, y, TerritoryWidth - 32, rowHeights[i]);
                    y += rowHeights[i] + settings.TerritoryLineSpacing;
                    if (fullTerritoryRow[i] && replaceableRows[i])
                        mask |= TerritoryBits[i];
                }
            }
            if (vehicle)
            {
                scale = vehicleScale;
                float left = right - vehicleWidth * scale;
                float y = bottom - PanelHeight * scale;
                DrawPanel(frame, left, y, vehicleWidth, scale);
                if (fuel)
                {
                    DrawValue(frame, gauges[(int)Indicator.Fuel], sample, left + 54 * scale, y, scale, now, progress);
                    mask |= 4;
                }
                float speedX = left + (fuel ? 108 : 0) * scale;
                for (int i = 0; i < Segments; ++i)
                {
                    if (i > 14 && i < 26)
                        continue;
                    PointF point = ring[i];
                    Rect(frame, speedX, y, scale, 77 + point.X * 1.85f, 56 + point.Y * 1.85f,
                        2.5f, 2.5f, i < 14 ? magenta : cyan);
                }
                DrawNumber(frame, sample.Speed, speedX, y, scale, 78, limit ? 14 : 21,
                    sample.FelonySpeeding ? warningDigits : vehicleDigits);
                mask |= 256;
                Sprite(frame, speedUnit, speedX, y, scale, 48, limit ? 58 : 71, 60, 18);
                if (limit)
                {
                    Sprite(frame, limitTexture, speedX, y, scale, 4, 82, 148, 17);
                    mask |= 2048;
                }
                else
                {
                    Rect(frame, speedX, y, scale, 36, 96, 42, 2, cyan);
                    Rect(frame, speedX, y, scale, 78, 96, 42, 2, magenta);
                }
            }
            return mask;
        }

        private bool Available(Sample sample, int slot)
        {
            var gauge = gauges[slot];
            if (gauge == null)
                return false;
            if (!float.IsNaN(sample.Value(gauge.Kind)))
                return true;
            gauge.Display = float.NaN;
            return false;
        }

        private void DrawValue(GraphicsEventArgs f, Gauge gauge, Sample sample, float x, float y, float scale, long now, float progress)
        {
            float value = sample.Value(gauge.Kind);
            gauge.Display = float.IsNaN(gauge.Display) ? value : gauge.Display + (value - gauge.Display) * progress;
            DrawGauge(f, gauge, value, x, y, scale, now);
        }

        private void DrawNumber(GraphicsEventArgs f, float value, float x, float y, float scale, float center, float top, Texture[] digits)
        {
            int number = (int)Math.Round(Math.Max(0, Math.Min(999, value)));
            int divisor = number >= 100 ? 100 : number >= 10 ? 10 : 1;
            float position = center - (divisor == 100 ? 3 : divisor == 10 ? 2 : 1) * 15f / 2;
            for (; divisor > 0; divisor /= 10, position += 15)
                Sprite(f, digits[number / divisor % 10], x, y, scale, position, top, 21.5f, 49);
        }

        private void DrawPanel(GraphicsEventArgs f, float x, float y, float width, float scale, float height = PanelHeight)
        {
            Rect(f, x, y, scale, 5, 0, width - 10, height, background);
            Rect(f, x, y, scale, 0, 5, 5, height - 10, background);
            Rect(f, x, y, scale, width - 5, 5, 5, height - 10, background);
            Rect(f, x, y, scale, 11, 5, width - 22, 1, edge);
            Rect(f, x, y, scale, 5, 0, 54, 2, cyan);
            Rect(f, x, y, scale, 0, 5, 2, 19, cyan);
            Rect(f, x, y, scale, width - 38, 0, 33, 2, magenta);
            Rect(f, x, y, scale, width - 2, 5, 2, 13, magenta);
            Rect(f, x, y, scale, 5, height - 2, 31, 2, magenta);
            Rect(f, x, y, scale, width - 58, height - 2, 53, 2, cyan);
            for (int i = 0; i < 3; ++i)
                Rect(f, x, y, scale, width - 20 - i * 6, 13, 3, 3, i == 2 ? magenta : cyan);
        }

        private void DrawGauge(GraphicsEventArgs f, Gauge gauge, float actual, float x, float y, float scale, long now)
        {
            int active = (int)Math.Ceiling(Segments * gauge.Display / 100);
            for (int i = 0; i < Segments; ++i)
            {
                var point = ring[i];
                Rect(f, x, y, scale, point.X - 1.2f, 40 + point.Y - 1.2f, 2.4f, 2.4f,
                    i < active ? gauge.Color : gauge.Track);
            }
            if (actual <= gauge.Critical)
            {
                float pulse = settings.Animate ? .65f + .35f * (float)Math.Sin(now / (double)Stopwatch.Frequency * 6) : 1;
                Rect(f, x, y, scale, -23, 71, 46, 4, Alpha(gauge.Glow, pulse));
                Rect(f, x, y, scale, -19, 72, 38, 1, Alpha(gauge.Color, pulse));
            }
            Sprite(f, gauge.Icon, x, y, scale, -16, 24, 32, 32);
            if (gauge.Label != null)
                Sprite(f, gauge.Label, x, y, scale, -46, 92, 92, 21);

            int percent = (int)Math.Round(gauge.Display);
            int digits = percent >= 100 ? 3 : (percent >= 10 ? 2 : 1);
            float position = -(digits + 1) * 8f / 2;
            if (digits == 3) DrawDigit(f, gauge, x, y, scale, ref position, percent / 100);
            if (digits >= 2) DrawDigit(f, gauge, x, y, scale, ref position, percent / 10 % 10);
            DrawDigit(f, gauge, x, y, scale, ref position, percent % 10);
            DrawDigit(f, gauge, x, y, scale, ref position, 10);
        }

        private static void DrawDigit(GraphicsEventArgs f, Gauge gauge, float x, float y, float scale, ref float position, int digit)
        {
            Sprite(f, gauge.Glyphs[digit], x, y, scale, position, 74, 9, 19);
            position += 8;
        }

        private Color WithOpacity(Color color, float factor) => Alpha(color, settings.Opacity * factor);
        private static Color Alpha(Color color, float factor) => Color.FromArgb((int)Math.Round(color.A * factor), color);
        private static float Clamp(float value, float min, float max) => Math.Min(Math.Max(min, max), Math.Max(min, value));
        private static void Rect(GraphicsEventArgs f, float x, float y, float scale, float dx, float dy, float w, float h, Color color)
            => f.Graphics.DrawRectangle(new RectangleF(x + dx * scale, y + dy * scale, w * scale, h * scale), color);
        private static void Sprite(GraphicsEventArgs f, Texture texture, float x, float y, float scale, float dx, float dy, float w, float h)
            => f.Graphics.DrawTexture(texture, new RectangleF(x + dx * scale, y + dy * scale, w * scale, h * scale));

        private Texture Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Recurso gráfico ausente.", path);
            var texture = Game.CreateTextureFromFile(path);
            if (texture == null)
                throw new InvalidOperationException("Falha ao carregar textura: " + path);
            textures.Add(texture);
            return texture;
        }

        private Texture LoadIcon(string directory, string name)
        {
            string path = Path.Combine(directory, "assets", name + ".png");
            if (settings.Opacity >= 1)
                return Load(path);
            string output = Path.Combine(cache, name + "-icon.png");
            using (var source = new Bitmap(path))
            using (var image = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            using (var graphics = System.Drawing.Graphics.FromImage(image))
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = settings.Opacity });
                graphics.DrawImage(source, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                image.Save(output, ImageFormat.Png);
            }
            return Load(output);
        }

        private Texture Text(string name, string text, Color color, float fontSize, int width, int height, bool bold = false, bool left = false)
        {
            return Text(name, text, color, fontSize, width, height, bold, left, out _);
        }

        private Texture Text(string name, string text, Color color, float fontSize, int width, int height,
            bool bold, bool left, out bool whole)
        {
            string path = Path.Combine(cache, name + ".png");
            using (var image = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var graphics = System.Drawing.Graphics.FromImage(image))
            using (var font = new Font("Consolas", fontSize, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = left ? StringAlignment.Near : StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                whole = graphics.MeasureString(text, font).Width <= width;
                graphics.DrawString(text, font, brush, new RectangleF(0, 0, width, height), format);
                image.Save(path, ImageFormat.Png);
            }
            return Load(path);
        }

        public void Dispose()
        {
            foreach (var texture in textures)
            {
                if ((object)texture is IDisposable resource)
                    resource.Dispose();
            }
            textures.Clear();
            try { Directory.Delete(cache, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
