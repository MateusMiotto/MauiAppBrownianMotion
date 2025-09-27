using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MauiAppBrownianMotion.Models
{
    public class GbmDrawable : IDrawable
    {
        // Fonte de dados (cada série é um array de double)
        public Func<List<double[]>>? GetPaths;

        // Layout / Aparência geral
        public Thickness Padding { get; set; } = new(60, 20, 15, 55);
        public int TargetYTicks { get; set; } = 6;
        public int TargetXTicks { get; set; } = 8;
        public Color AxisColor { get; set; } = Colors.Gray;
        public Color GridColor { get; set; } = Color.FromRgba(160, 160, 160, 80);
        public float AxisStrokeSize { get; set; } = 1f;
        public float SeriesStrokeSize { get; set; } = 1.5f;
        public float GridStrokeSize { get; set; } = 0.5f;
        public float FontSize { get; set; } = 11f;
        public string XAxisTitle { get; set; } = "Dias";
        public string YAxisTitle { get; set; } = "Preço";

        // Zoom / Pan horizontal
        public double XZoom { get; set; } = 1.0;
        public double XPan { get; set; } = 0.0;

        // Margem superior reservada para tooltip
        public float TooltipTopMargin { get; set; } = 20f;

        // --- Hover support ---
        public bool HoverEnabled { get; set; } = true; // permite desativar zoom e tooltip para cenários pesados
        public PointF? HoverPoint { get; private set; }
        public void SetHover(PointF? p)
        {
            if (!HoverEnabled) { HoverPoint = null; return; }
            HoverPoint = p;
        }

        #region Series Styling
        /// <summary>
        /// Representa o estilo de uma série (cor, espessura e padrão de tracejado opcional).
        /// </summary>
        public readonly record struct SeriesStyle(Color StrokeColor, float StrokeSize, float[]? DashPattern);

        /// <summary>
        /// Delegate para determinar o estilo de cada série a partir do índice.
        /// </summary>
        public Func<int, SeriesStyle>? SeriesStyleSelector { get; set; }

        /// <summary>
        /// Paleta configurada via SetSeriesColorPalette (se usada). Apenas cores sólidas.
        /// </summary>
        private Color[]? _palette;

        /// <summary>
        /// Define uma paleta de cores cíclica para as séries. Zera qualquer padrão de tracejado.
        /// </summary>
        public void SetSeriesColorPalette(params Color[] colors)
        {
            if (colors == null || colors.Length == 0)
            {
                _palette = null;
                SeriesStyleSelector = null;
                return;
            }
            _palette = colors.ToArray();
            SeriesStyleSelector = i => new SeriesStyle(_palette[i % _palette.Length], SeriesStrokeSize, null);
        }

        /// <summary>
        /// Gera automaticamente uma paleta HSL distribuída.
        /// </summary>
        public void UseAutoDistributedPalette(int count, float saturation = 0.60f, float lightness = 0.55f)
        {
            if (count <= 0) { SetSeriesColorPalette(); return; }
            var list = new List<Color>(count);
            for (int i = 0; i < count; i++)
            {
                float h = (float)i / count; // 0..1
                list.Add(HslToColor(h, saturation, lightness));
            }
            SetSeriesColorPalette(list.ToArray());
        }

        /// <summary>
        /// Obtém o estilo (cor/padrão) para a série. Se nenhum provider for definido, gera cor pseudo-determinística.
        /// </summary>
        public SeriesStyle GetSeriesStyle(int index)
        {
            if (SeriesStyleSelector != null)
                return SeriesStyleSelector(index);

            // Fallback determinístico (similar ao anterior, porém encapsulado)
            var rnd = new Random(index * 7919);
            var color = Color.FromRgb(rnd.Next(40, 220), rnd.Next(40, 220), rnd.Next(40, 220));
            return new SeriesStyle(color, SeriesStrokeSize, null);
        }

        private static Color HslToColor(float h, float s, float l)
        {
            // Conversão simples HSL -> RGB
            float r, g, b;
            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;
                r = HueToRgb(p, q, h + 1f / 3f);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1f / 3f);
            }
            return Color.FromRgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6f) return p + (q - p) * 6 * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6;
            return p;
        }
        #endregion

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var paths = GetPaths?.Invoke() ?? new();
            if (paths.Count == 0) return;

            // Comprimento total das séries
            int fullLen = paths.Max(p => p.Length);
            if (fullLen < 2) fullLen = 2;

            // Normalização de zoom/pan
            if (XZoom < 1) XZoom = 1;
            double visibleFraction = 1.0 / XZoom;
            if (visibleFraction > 1) { XZoom = 1; visibleFraction = 1; }

            int visibleCount = (int)Math.Ceiling(fullLen * visibleFraction);
            if (visibleCount < 2) visibleCount = 2;
            if (visibleCount > fullLen) visibleCount = fullLen;

            int maxStart = Math.Max(0, fullLen - visibleCount);

            if (XPan < 0) XPan = 0;
            if (XPan > 1) XPan = 1;

            int startIndex = (int)Math.Floor(XPan * maxStart + 1e-9);
            if (startIndex > maxStart) startIndex = maxStart;

            int endIndex = startIndex + visibleCount - 1;
            if (endIndex >= fullLen) { endIndex = fullLen - 1; startIndex = Math.Max(0, endIndex - visibleCount + 1); }

            visibleCount = endIndex - startIndex + 1;
            if (visibleCount < 2)
            {
                visibleCount = Math.Min(2, fullLen);
                startIndex = 0;
                endIndex = Math.Min(fullLen - 1, 1);
            }

            double visMin = double.PositiveInfinity, visMax = double.NegativeInfinity;
            foreach (var s in paths)
            {
                if (s.Length == 0) continue;
                int localEnd = Math.Min(endIndex, s.Length - 1);
                if (localEnd < startIndex) continue;
                for (int i = startIndex; i <= localEnd; i++)
                {
                    double v = s[i];
                    if (v < visMin) visMin = v;
                    if (v > visMax) visMax = v;
                }
            }
            if (!double.IsFinite(visMin) || !double.IsFinite(visMax)) { visMin = 0; visMax = 1; }
            if (visMax <= visMin) visMax = visMin + 1;

            // Ticks Y
            var yTicks = GenerateNiceTicks(visMin, visMax, TargetYTicks);

            // Medição de largura máxima dos rótulos Y para ajustar Padding.Left
            IFont baseFont = Microsoft.Maui.Graphics.Font.Default;
            canvas.Font = baseFont;
            canvas.FontSize = FontSize;

            float maxYLabelW = 0f;
            foreach (var yt in yTicks)
            {
                string label = FormatY(yt, visMin, visMax);
                var size = canvas.GetStringSize(label, baseFont, FontSize);
                if (size.Width > maxYLabelW) maxYLabelW = size.Width;
            }
            float leftPad = MathF.Max((float)Padding.Left, maxYLabelW + 12f);

            // Reserva espaço superior para tooltip
            float topWithTooltip = (float)Padding.Top + TooltipTopMargin;
            var plot = new RectF(
                leftPad,
                topWithTooltip,
                dirtyRect.Width - leftPad - (float)Padding.Right,
                dirtyRect.Height - topWithTooltip - (float)Padding.Bottom);

            if (plot.Width <= 0 || plot.Height <= 0) return;

            // Ticks X locais/globais
            var xTicksLocal = GenerateIndexTicks(visibleCount, TargetXTicks);
            var xTicksGlobal = xTicksLocal.Select(i => i + startIndex).ToList();

            // Clipping de segurança
            canvas.SaveState();
            canvas.ClipRectangle(dirtyRect);

            DrawGrid(canvas, plot, yTicks, xTicksLocal, visMin, visMax, visibleCount); // atualizado
            DrawAxes(canvas, plot);
            DrawYLabels(canvas, plot, yTicks, visMin, visMax, leftPad);
            DrawXLabels(canvas, plot, xTicksLocal, xTicksGlobal, visibleCount);
            DrawSeries(canvas, plot, paths, startIndex, endIndex, visibleCount, visMin, visMax);
            if (HoverEnabled)
                DrawHover(canvas, plot, paths, startIndex, endIndex, visibleCount, visMin, visMax, dirtyRect, baseFont);
            DrawTitles(canvas, plot, leftPad);

            canvas.RestoreState();
        }

        #region Draw Sections
        private void DrawGrid(ICanvas canvas, RectF plot, List<double> yTicks, List<int> xTicksLocal, double visMin, double visMax, int visibleCount)
        {
            canvas.StrokeColor = GridColor;
            canvas.StrokeSize = GridStrokeSize;
            // Linhas horizontais alinhadas ao mesmo mapeamento usado para séries/labels
            foreach (var yt in yTicks)
            {
                float y = ValueToY(yt, visMin, visMax, plot);
                canvas.DrawLine(plot.Left, AlignPx(y), plot.Right, AlignPx(y));
            }
            // Linhas verticais: usar visibleCount para que a última chegue exatamente ao fim
            foreach (var xtLocal in xTicksLocal)
            {
                float x = LocalIndexToX(xtLocal, visibleCount, plot);
                canvas.DrawLine(AlignPx(x), plot.Top, AlignPx(x), plot.Bottom);
            }
        }

        private void DrawAxes(ICanvas canvas, RectF plot)
        {
            canvas.StrokeColor = AxisColor;
            canvas.StrokeSize = AxisStrokeSize;
            canvas.DrawLine(AlignPx(plot.Left), plot.Top, AlignPx(plot.Left), plot.Bottom);
            canvas.DrawLine(plot.Left, AlignPx(plot.Bottom), plot.Right, AlignPx(plot.Bottom));
        }

        private void DrawYLabels(ICanvas canvas, RectF plot, List<double> yTicks, double visMin, double visMax, float leftPad)
        {
            canvas.FontSize = FontSize;
            canvas.FontColor = AxisColor;
            foreach (var yt in yTicks)
            {
                float y = ValueToY(yt, visMin, visMax, plot);
                canvas.DrawLine(plot.Left - 5, AlignPx(y), plot.Left, AlignPx(y));
                string label = FormatY(yt, visMin, visMax);
                var rect = new RectF(0, y - 8, plot.Left - 7, 16);
                canvas.DrawString(label, rect, HorizontalAlignment.Right, VerticalAlignment.Center);
            }
        }

        private void DrawXLabels(ICanvas canvas, RectF plot, List<int> xTicksLocal, List<int> xTicksGlobal, int visibleCount)
        {
            for (int i = 0; i < xTicksLocal.Count; i++)
            {
                int xtLocal = xTicksLocal[i];
                int xtGlobal = xTicksGlobal[i];
                float x = LocalIndexToX(xtLocal, visibleCount, plot);
                canvas.DrawLine(AlignPx(x), plot.Bottom, AlignPx(x), plot.Bottom + 5);
                string label = xtGlobal.ToString();
                var rect = new RectF(x - 30, plot.Bottom + 6, 60, 18);
                canvas.DrawString(label, rect, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        private void DrawSeries(ICanvas canvas, RectF plot, List<double[]> paths, int startIndex, int endIndex, int visibleCount, double visMin, double visMax)
        {
            for (int si = 0; si < paths.Count; si++)
            {
                var series = paths[si];
                if (series.Length < 2) continue;
                if (startIndex >= series.Length) continue;
                int localVisibleEnd = Math.Min(endIndex, series.Length - 1);
                if (localVisibleEnd <= startIndex) continue;

                var style = GetSeriesStyle(si);
                canvas.StrokeSize = style.StrokeSize;
                canvas.StrokeColor = style.StrokeColor;
                canvas.StrokeDashPattern = style.DashPattern;

                float xStep = plot.Width / Math.Max(1, (visibleCount - 1));
                float prevX = plot.Left;
                float prevY = ValueToY(series[startIndex], visMin, visMax, plot);

                for (int global = startIndex + 1; global <= localVisibleEnd; global++)
                {
                    int local = global - startIndex;
                    float x = plot.Left + local * xStep;
                    float y = ValueToY(series[global], visMin, visMax, plot);
                    canvas.DrawLine(prevX, prevY, x, y);
                    prevX = x; prevY = y;
                }
            }
            // Reset dash caso alguma série tenha usado
            canvas.StrokeDashPattern = null;
        }

        private void DrawHover(ICanvas canvas, RectF plot, List<double[]> paths, int startIndex, int endIndex, int visibleCount, double visMin, double visMax, RectF dirtyRect, IFont baseFont)
        {
            if (!HoverPoint.HasValue) return;
            var hp = HoverPoint.Value;
            if (!(hp.X >= plot.Left && hp.X <= plot.Right && hp.Y >= plot.Top && hp.Y <= plot.Bottom && visibleCount > 0)) return;

            double tLocal = (hp.X - plot.Left) / plot.Width;
            int localIdx = Math.Clamp((int)Math.Round(Math.Clamp(tLocal, 0, 1) * (visibleCount - 1)), 0, visibleCount - 1);
            int globalIdx = startIndex + localIdx;

            float hoverX = LocalIndexToX(localIdx, visibleCount, plot);

            int closestSeriesIndex = -1;
            double closestValue = double.NaN;
            float smallestDy = float.MaxValue;
            for (int si = 0; si < paths.Count; si++)
            {
                var s = paths[si];
                if (globalIdx >= s.Length) continue;
                float yVal = ValueToY(s[globalIdx], visMin, visMax, plot);
                float dy = MathF.Abs(yVal - hp.Y);
                if (dy < smallestDy)
                {
                    smallestDy = dy;
                    closestSeriesIndex = si;
                    closestValue = s[globalIdx];
                }
            }

            // linha vertical geral
            canvas.StrokeColor = Colors.Black.WithAlpha(0.35f);
            canvas.StrokeSize = 1;
            canvas.DrawLine(AlignPx(hoverX), plot.Top, AlignPx(hoverX), plot.Bottom);

            if (closestSeriesIndex < 0) return;

            var chosenStyle = GetSeriesStyle(closestSeriesIndex);
            var seriesColor = chosenStyle.StrokeColor;

            string tip = $"Dia: {globalIdx} Série: {closestSeriesIndex + 1} Preço: R${closestValue:0.###}";
            var tipSize = canvas.GetStringSize(tip, baseFont, FontSize);
            float boxW = tipSize.Width + 14f;
            float boxH = tipSize.Height + 10f;

            float boxX = hoverX + 8f;
            if (boxX + boxW > dirtyRect.Right) boxX = hoverX - 8f - boxW;
            boxX = MathF.Min(MathF.Max(boxX, dirtyRect.Left + 1f), dirtyRect.Right - boxW - 1f);

            float topBandH = plot.Top - dirtyRect.Top;
            float bottomBandH = dirtyRect.Bottom - plot.Bottom;
            float boxY;
            if (boxH + 2f <= topBandH)
                boxY = plot.Top - boxH - 2f;
            else if (boxH + 2f <= bottomBandH)
                boxY = plot.Bottom + 2f;
            else
                boxY = plot.Top + 2f;
            boxY = MathF.Min(MathF.Max(boxY, dirtyRect.Top + 1f), dirtyRect.Bottom - boxH - 1f);

            // fundo tooltip
            canvas.FillColor = Colors.White.WithAlpha(0.92f);
            canvas.FillRectangle(boxX, boxY, boxW, boxH);
            canvas.StrokeColor = seriesColor;
            canvas.StrokeSize = 3f;
            canvas.DrawRectangle(boxX, boxY, boxW, boxH);

            // texto tooltip
            canvas.FontColor = Colors.Black;
            var tipRect = new RectF(boxX, boxY, boxW, boxH);
            canvas.DrawString(tip, tipRect, HorizontalAlignment.Center, VerticalAlignment.Center);

            // ponto destacado
            var chosenSeries = paths[closestSeriesIndex];
            if (globalIdx < chosenSeries.Length)
            {
                float pointY = ValueToY(chosenSeries[globalIdx], visMin, visMax, plot);
                canvas.FillColor = seriesColor;
                float r = 4.5f;
                canvas.FillEllipse(hoverX - r, pointY - r, r * 2, r * 2);
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 1f;
                canvas.DrawEllipse(hoverX - r, pointY - r, r * 2, r * 2);
            }

            // Re-desenhar série destacada (mais espessa) por cima
            if (chosenSeries.Length > 1 && startIndex < chosenSeries.Length)
            {
                int localVisibleEnd = Math.Min(endIndex, chosenSeries.Length - 1);
                if (localVisibleEnd > startIndex)
                {
                    canvas.StrokeColor = seriesColor;
                    canvas.StrokeSize = SeriesStrokeSize * 1.9f;
                    canvas.StrokeDashPattern = chosenStyle.DashPattern; // manter padrão (se houver)
                    float xStep = plot.Width / Math.Max(1, (visibleCount - 1));
                    float prevX = plot.Left;
                    float prevY = ValueToY(chosenSeries[startIndex], visMin, visMax, plot);
                    for (int global = startIndex + 1; global <= localVisibleEnd; global++)
                    {
                        int local = global - startIndex;
                        float x = plot.Left + local * xStep;
                        float y = ValueToY(chosenSeries[global], visMin, visMax, plot);
                        canvas.DrawLine(prevX, prevY, x, y);
                        prevX = x; prevY = y;
                    }
                    canvas.StrokeDashPattern = null; // limpar
                }
            }
        }

        private void DrawTitles(ICanvas canvas, RectF plot, float leftPad)
        {
            canvas.FontColor = AxisColor;
            if (!string.IsNullOrEmpty(XAxisTitle))
            {
                canvas.FontSize = FontSize + 1;
                canvas.DrawString(XAxisTitle,
                    new RectF(plot.Left, plot.Bottom + 24, plot.Width, 20),
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            if (!string.IsNullOrEmpty(YAxisTitle))
            {
                canvas.SaveState();
                float yTitleOffsetX = MathF.Max(20f, leftPad * 0.35f);
                canvas.Translate(yTitleOffsetX, plot.Top + plot.Height / 2);
                canvas.Rotate(-90);
                canvas.FontSize = FontSize + 1;
                canvas.DrawString(YAxisTitle,
                    new RectF(-plot.Height / 2, -12, plot.Height, 24),
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                canvas.RestoreState();
            }
        }
        #endregion

        #region Helpers
        static float AlignPx(float v) => MathF.Round(v) + 0.5f;
        static float ValueToY(double value, double min, double max, RectF plot)
            => plot.Bottom - (float)((value - min) / (max - min) * plot.Height);
        static float LocalIndexToX(int localIndex, int visibleCount, RectF plot)
            => visibleCount <= 1 ? plot.Left : plot.Left + (float)localIndex / (visibleCount - 1) * plot.Width;

        static List<double> GenerateNiceTicks(double min, double max, int target)
        {
            var ticks = new List<double>();
            if (target < 2) target = 2;
            double range = NiceNum(max - min, false);
            double step = NiceNum(range / (target - 1), true);
            double graphMin = Math.Floor(min / step) * step;
            double graphMax = Math.Ceiling(max / step) * step;
            for (double v = graphMin; v <= graphMax + 0.5 * step; v += step)
            {
                double rv = Math.Round(v / step) * step;
                if (rv >= graphMin - 1e-9 && rv <= graphMax + 1e-9)
                    ticks.Add(rv);
            }
            return ticks;
        }

        static double NiceNum(double value, bool round)
        {
            if (value <= 0) return 1;
            double exp = Math.Floor(Math.Log10(value));
            double f = value / Math.Pow(10, exp);
            double nf = round
                ? (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10)
                : (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10);
            return nf * Math.Pow(10, exp);
        }

        static List<int> GenerateIndexTicks(int length, int target)
        {
            var ticks = new List<int>();
            if (length <= 1) { ticks.Add(0); return ticks; }
            if (target < 2) target = 2;
            int step = Math.Max(1, (int)Math.Round((length - 1) / (double)(target - 1)));
            for (int i = 0; i < length; i += step) ticks.Add(i);
            if (ticks[^1] != length - 1) ticks.Add(length - 1);
            return ticks;
        }

        static string FormatY(double v, double min, double max)
        {
            double range = Math.Abs(max - min);
            if (range < 1e-6) return v.ToString("0.####");
            if (range < 0.01) return v.ToString("0.####");
            if (range < 0.1) return v.ToString("0.###");
            if (range < 1) return v.ToString("0.##");
            if (range < 10) return v.ToString("0.##");
            if (range < 100) return v.ToString("0.#");
            return v.ToString("0");
        }
        #endregion
    }
}
