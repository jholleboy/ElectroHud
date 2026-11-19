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
        private const float CardWidth = 102;
        private const float PanelHeight = 142;
        private static readonly string[] Names = { "Saúde", "Fome", "Sede", "Sono", "Estresse", "Combustível" };
        private static readonly string[] Icons = { "health", "hunger", "thirst", "sleep", "stress", "fuel" };
        private static readonly string[] Defaults = { "#50FF92", "#FFA94F", "#36E6FF", "#AC7DFF", "#FF68BA", "#5488FF" };
        private readonly PointF[] ring = new PointF[Segments];
        private readonly Gauge[] gauges = new Gauge[Names.Length];
        private readonly List<Texture> textures = new List<Texture>();
        private readonly Settings settings;
        private readonly Color cyan, magenta, background, edge;
        private readonly string cache;
        private readonly Texture logo;
        private long previousFrame;

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
                logo = Text("logo", "ElectroHud", cyan, 29, 244, 54, true);
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
                        Label = Text(Icons[i] + "-label", Names[i], WithOpacity(Color.White, .96f), 19, 184, 42),
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

        public void Draw(GraphicsEventArgs frame, Sample sample)
        {
            int count = 0;
            foreach (var gauge in gauges)
            {
                if (gauge == null)
                    continue;
                if (float.IsNaN(sample.Value(gauge.Kind)))
                    gauge.Display = float.NaN;
                else
                    ++count;
            }
            if (count == 0 || sample.Screen.Width <= 0 || sample.Screen.Height <= 0 || settings.Opacity <= 0)
            {
                previousFrame = 0;
                return;
            }

            long now = Stopwatch.GetTimestamp();
            float elapsed = previousFrame == 0 ? .05f : Math.Min(.12f, (now - previousFrame) / (float)Stopwatch.Frequency);
            previousFrame = now;
            float progress = settings.Animate ? 1 - (float)Math.Exp(-12 * elapsed) : 1f;
            float width = Math.Max(152, count * CardWidth + 22);
            float scale = Math.Min(sample.Screen.Width / 1920f, sample.Screen.Height / 1080f) * settings.Scale;
            float insetX = sample.Screen.Width * (1 - sample.SafeZone) * .5f;
            float insetY = sample.Screen.Height * (1 - sample.SafeZone) * .5f;
            float margin = Math.Min(12 * scale, Math.Min(sample.Screen.Width, sample.Screen.Height) * .025f);
            insetX += margin;
            insetY += margin;
            scale = Math.Min(scale, Math.Min((sample.Screen.Width - 2 * insetX) / width,
                (sample.Screen.Height - 2 * insetY) / PanelHeight));
            float left = Clamp(sample.Screen.Width * settings.X - width * scale / 2,
                insetX, sample.Screen.Width - insetX - width * scale);
            float top = Clamp(sample.Screen.Height * settings.Y - PanelHeight * scale / 2,
                insetY, sample.Screen.Height - insetY - PanelHeight * scale);

            DrawPanel(frame, left, top, width, scale);
            int index = 0;
            foreach (var gauge in gauges)
            {
                if (gauge == null)
                    continue;
                float value = sample.Value(gauge.Kind);
                if (float.IsNaN(value))
                    continue;
                gauge.Display = float.IsNaN(gauge.Display) ? value : gauge.Display + (value - gauge.Display) * progress;
                float center = (width - count * CardWidth) / 2 + (index++ + .5f) * CardWidth;
                DrawGauge(frame, gauge, value, left + center * scale, top, scale, now);
            }
        }

        private void DrawPanel(GraphicsEventArgs f, float x, float y, float width, float scale)
        {
            Rect(f, x, y, scale, 5, 0, width - 10, PanelHeight, background);
            Rect(f, x, y, scale, 0, 5, 5, PanelHeight - 10, background);
            Rect(f, x, y, scale, width - 5, 5, 5, PanelHeight - 10, background);
            Rect(f, x, y, scale, 6, 23, width - 12, 1, edge);
            Rect(f, x, y, scale, 5, 0, 54, 2, cyan);
            Rect(f, x, y, scale, 0, 5, 2, 19, cyan);
            Rect(f, x, y, scale, width - 38, 0, 33, 2, magenta);
            Rect(f, x, y, scale, width - 2, 5, 2, 13, magenta);
            Rect(f, x, y, scale, 5, PanelHeight - 2, 31, 2, magenta);
            Rect(f, x, y, scale, width - 58, PanelHeight - 2, 53, 2, cyan);
            for (int i = 0; i < 3; ++i)
                Rect(f, x, y, scale, width - 20 - i * 6, 13, 3, 3, i == 2 ? magenta : cyan);
            Sprite(f, logo, x, y, scale, 10, 1, 106, 23);
        }

        private void DrawGauge(GraphicsEventArgs f, Gauge gauge, float actual, float x, float y, float scale, long now)
        {
            int active = (int)Math.Ceiling(Segments * gauge.Display / 100);
            for (int i = 0; i < Segments; ++i)
            {
                var point = ring[i];
                Rect(f, x, y, scale, point.X - 1.2f, 61 + point.Y - 1.2f, 2.4f, 2.4f,
                    i < active ? gauge.Color : gauge.Track);
            }
            if (actual <= gauge.Critical)
            {
                float pulse = settings.Animate ? .65f + .35f * (float)Math.Sin(now / (double)Stopwatch.Frequency * 6) : 1;
                Rect(f, x, y, scale, -23, 92, 46, 4, Alpha(gauge.Glow, pulse));
                Rect(f, x, y, scale, -19, 93, 38, 1, Alpha(gauge.Color, pulse));
            }
            Sprite(f, gauge.Icon, x, y, scale, -16, 45, 32, 32);
            Sprite(f, gauge.Label, x, y, scale, -46, 117, 92, 21);

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
            Sprite(f, gauge.Glyphs[digit], x, y, scale, position, 96, 9, 19);
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

        private Texture Text(string name, string text, Color color, float fontSize, int width, int height, bool bold = false)
        {
            string path = Path.Combine(cache, name + ".png");
            using (var image = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var graphics = System.Drawing.Graphics.FromImage(image))
            using (var font = new Font("Consolas", fontSize, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
