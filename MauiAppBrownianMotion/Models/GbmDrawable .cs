using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MauiAppBrownianMotion.Models
{
    public class GbmDrawable : IDrawable
    {
        public Func<List<double[]>>? GetPaths;

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

        // Zoom/pan horizontal
        public double XZoom { get; set; } = 1.0;
        public double XPan { get; set; } = 0.0;

        // Margem superior reservada para tooltip (evita corte)
        public float TooltipTopMargin { get; set; } = 20f;

        // --- Hover support ---
        public PointF? HoverPoint { get; private set; }
        public void SetHover(PointF? p) => HoverPoint = p;

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

            // Grid
            canvas.StrokeColor = GridColor;
            canvas.StrokeSize = GridStrokeSize;
            foreach (var yt in yTicks)
            {
                float y = ValueToY(yt, visMin, visMax, plot);
                canvas.DrawLine(plot.Left, AlignPx(y), plot.Right, AlignPx(y));
            }
            foreach (var xtLocal in xTicksLocal)
            {
                float x = LocalIndexToX(xtLocal, visibleCount, plot);
                canvas.DrawLine(AlignPx(x), plot.Top, AlignPx(x), plot.Bottom);
            }

            // Eixos
            canvas.StrokeColor = AxisColor;
            canvas.StrokeSize = AxisStrokeSize;
            canvas.DrawLine(AlignPx(plot.Left), plot.Top, AlignPx(plot.Left), plot.Bottom);
            canvas.DrawLine(plot.Left, AlignPx(plot.Bottom), plot.Right, AlignPx(plot.Bottom));

            // Labels Y
            canvas.FontSize = FontSize;
            canvas.FontColor = AxisColor;
            foreach (var yt in yTicks)
            {
                float y = ValueToY(yt, visMin, visMax, plot);
                // tique
                canvas.DrawLine(plot.Left - 5, AlignPx(y), plot.Left, AlignPx(y));
                // texto
                string label = FormatY(yt, visMin, visMax);
                var rect = new RectF(0, y - 8, plot.Left - 7, 16);
                canvas.DrawString(label, rect, HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            // Labels X
            for (int i = 0; i < xTicksLocal.Count; i++)
            {
                int xtLocal = xTicksLocal[i];
                int xtGlobal = xTicksGlobal[i];
                float x = LocalIndexToX(xtLocal, visibleCount, plot);
                // tique
                canvas.DrawLine(AlignPx(x), plot.Bottom, AlignPx(x), plot.Bottom + 5);
                // texto
                string label = xtGlobal.ToString();
                var rect = new RectF(x - 30, plot.Bottom + 6, 60, 18);
                canvas.DrawString(label, rect, HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            // Séries
            for (int si = 0; si < paths.Count; si++)
            {
                var series = paths[si];
                if (series.Length < 2) continue;
                if (startIndex >= series.Length) continue;
                int localVisibleEnd = Math.Min(endIndex, series.Length - 1);
                if (localVisibleEnd <= startIndex) continue;

                canvas.StrokeSize = SeriesStrokeSize;
                // Cor pseudo-determinística para consistência entre frames
                var rnd = new Random(si * 7919);
                canvas.StrokeColor = Color.FromRgb(rnd.Next(40, 220), rnd.Next(40, 220), rnd.Next(40, 220));

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

            // Hover marker + tooltip
            //if (HoverPoint.HasValue)
            //{
            //    var hp = HoverPoint.Value;
            //    if (hp.X >= plot.Left && hp.X <= plot.Right && hp.Y >= plot.Top && hp.Y <= plot.Bottom && visibleCount > 0)
            //    {
            //        double tLocal = (hp.X - plot.Left) / plot.Width;
            //        tLocal = Math.Clamp(tLocal, 0.0, 1.0);
            //        int localIdx = (int)Math.Round(tLocal * (visibleCount - 1));
            //        localIdx = Math.Clamp(localIdx, 0, visibleCount - 1);
            //        int globalIdx = startIndex + localIdx;

            //        float hoverX = LocalIndexToX(localIdx, visibleCount, plot);

            //        // vertical line
            //        canvas.StrokeColor = Colors.Black.WithAlpha(0.4f);
            //        canvas.StrokeSize = 1;
            //        canvas.DrawLine(AlignPx(hoverX), plot.Top, AlignPx(hoverX), plot.Bottom);

            //        // Compose single-line tooltip text
            //        string values = string.Join(", ", paths.Select((s, i) =>
            //            globalIdx < s.Length ? $"S{i + 1}={s[globalIdx]:0.###}" : $"S{i + 1}=-"));
            //        string tip = $"i {globalIdx}: {values}";
            //        var tipSize = canvas.GetStringSize(tip, baseFont, FontSize);
            //        float boxX = hoverX + 8;
            //        if (boxX + tipSize.Width + 12 > plot.Right) boxX = hoverX - 8 - tipSize.Width - 12;
            //        // Posiciona tooltip dentro da margem reservada
            //        float boxY = plot.Top - TooltipTopMargin + (TooltipTopMargin - (tipSize.Height + 8)) / 2f;
            //        if (boxY < 0) boxY = 0;

            //        canvas.FillColor = Colors.White.WithAlpha(0.90f);
            //        canvas.FillRectangle(boxX, boxY, tipSize.Width + 12, tipSize.Height + 8);
            //        canvas.StrokeColor = AxisColor;
            //        canvas.DrawRectangle(boxX, boxY, tipSize.Width + 12, tipSize.Height + 8);
            //        canvas.FontColor = Colors.Black;
            //        canvas.DrawString(tip, boxX + 6, boxY + 4, HorizontalAlignment.Left);
            //    }
            //}
            // Hover marker + tooltip
            if (HoverPoint.HasValue)
            {
                var hp = HoverPoint.Value;
                if (hp.X >= plot.Left && hp.X <= plot.Right && hp.Y >= plot.Top && hp.Y <= plot.Bottom && visibleCount > 0)
                {
                    double tLocal = (hp.X - plot.Left) / plot.Width;
                    int localIdx = Math.Clamp((int)Math.Round(Math.Clamp(tLocal, 0, 1) * (visibleCount - 1)), 0, visibleCount - 1);
                    int globalIdx = startIndex + localIdx;

                    float hoverX = LocalIndexToX(localIdx, visibleCount, plot);

                    // linha vertical
                    canvas.StrokeColor = Colors.Black.WithAlpha(0.4f);
                    canvas.StrokeSize = 1;
                    canvas.DrawLine(AlignPx(hoverX), plot.Top, AlignPx(hoverX), plot.Bottom);

                    // texto
                    string values = string.Join(", ", paths.Select((s, i) =>
                        globalIdx < s.Length ? $"S{i + 1}={s[globalIdx]:0.###}" : $"S{i + 1}=-"));
                    string tip = $"i {globalIdx}: {values}";

                    // medir
                    var tipSize = canvas.GetStringSize(tip, baseFont, FontSize);
                    float boxW = tipSize.Width + 12f;
                    float boxH = tipSize.Height + 8f;

                    // X preferido: direita do hover; se não couber, esquerda
                    float boxX = hoverX + 8f;
                    if (boxX + boxW > dirtyRect.Right) boxX = hoverX - 8f - boxW;
                    // clamp em X
                    boxX = MathF.Min(MathF.Max(boxX, dirtyRect.Left + 1f), dirtyRect.Right - boxW - 1f);

                    // Y: 1) faixa reservada acima do plot; 2) abaixo; 3) dentro
                    float topBandH = plot.Top - dirtyRect.Top;
                    float bottomBandH = dirtyRect.Bottom - plot.Bottom;
                    float boxY;
                    if (boxH + 2f <= topBandH)
                        boxY = plot.Top - boxH - 2f;           // acima do plot
                    else if (boxH + 2f <= bottomBandH)
                        boxY = plot.Bottom + 2f;               // abaixo do plot
                    else
                        boxY = plot.Top + 2f;                  // dentro do plot
                                                               // clamp em Y
                    boxY = MathF.Min(MathF.Max(boxY, dirtyRect.Top + 1f), dirtyRect.Bottom - boxH - 1f);

                    // desenha caixa
                    canvas.FillColor = Colors.White.WithAlpha(0.90f);
                    canvas.FillRectangle(boxX, boxY, boxW, boxH);
                    canvas.StrokeColor = AxisColor;
                    canvas.DrawRectangle(boxX, boxY, boxW, boxH);

                    // texto centralizado no retângulo
                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = FontSize;
                    var tipRect = new RectF(boxX, boxY, boxW, boxH);
                    canvas.DrawString(tip, tipRect, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }


            // Título X
            canvas.FontColor = AxisColor;
            if (!string.IsNullOrEmpty(XAxisTitle))
            {
                canvas.FontSize = FontSize + 1;
                canvas.DrawString(XAxisTitle,
                    new RectF(plot.Left, plot.Bottom + 24, plot.Width, 20),
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            // Título Y com deslocamento que respeita o novo padding
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

            canvas.RestoreState();
        }

        // Helpers
        static float AlignPx(float v) => MathF.Round(v) + 0.5f;

        static float ValueToY(double value, double min, double max, RectF plot)
            => plot.Bottom - (float)((value - min) / (max - min) * plot.Height);

        static float LocalIndexToX(int localIndex, int visibleCount, RectF plot)
            => visibleCount <= 1 ? plot.Left : plot.Left + (float)localIndex / (visibleCount - 1) * plot.Width;

        // Escalas e formatação
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
    }
}
