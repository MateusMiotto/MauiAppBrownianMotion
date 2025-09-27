namespace MauiAppBrownianMotion.Models
{
    public class GbmDrawable : IDrawable
    {
        public Func<List<double[]>>? GetPaths;
        public Thickness Padding { get; set; } = new(30, 10, 10, 25);

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var paths = GetPaths?.Invoke() ?? new();
            if (paths.Count == 0) return;

            // área útil
            var plot = new RectF(
                (float)Padding.Left,
                (float)Padding.Top,
                dirtyRect.Width - (float)Padding.Left - (float)Padding.Right,
                dirtyRect.Height - (float)Padding.Top - (float)Padding.Bottom);

            // min/max
            double min = paths.SelectMany(p => p).Min();
            double max = paths.SelectMany(p => p).Max();
            if (max <= min) { max = min + 1; }

            // eixos simples
            canvas.StrokeSize = 1;
            canvas.StrokeColor = Colors.Gray;
            canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            canvas.DrawLine(plot.Left, plot.Top, plot.Left, plot.Bottom);

            // desenha cada série
            foreach (var series in paths)
            {
                canvas.StrokeSize = 1.5f;
                // cor automática
                canvas.StrokeColor = Color.FromRgb(Random.Shared.Next(40, 220),
                                                   Random.Shared.Next(40, 220),
                                                   Random.Shared.Next(40, 220));

                float xStep = plot.Width / (series.Length - 1);
                for (int i = 1; i < series.Length; i++)
                {
                    float x1 = plot.Left + (i - 1) * xStep;
                    float x2 = plot.Left + i * xStep;
                    float y1 = plot.Bottom - (float)((series[i - 1] - min) / (max - min) * plot.Height);
                    float y2 = plot.Bottom - (float)((series[i] - min) / (max - min) * plot.Height);
                    canvas.DrawLine(x1, y1, x2, y2);
                }
            }
        }
    }

}