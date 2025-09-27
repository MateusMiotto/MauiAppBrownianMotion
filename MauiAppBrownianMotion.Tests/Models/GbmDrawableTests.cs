using MauiAppBrownianMotion.Models;
using Moq;
using Xunit;

namespace MauiAppBrownianMotion.Tests.Models
{
    public class GbmDrawableTests
    {
        // Verifica que o drawable gera chamadas de linha para séries válidas.
        [Fact]
        public void Draw_GeraLinhas_ParaSeriesSimples()
        {
            var paths = new List<double[]>
                {
                    new double[] { 10, 11, 12, 13, 14 },
                    new double[] { 14, 13, 12, 11, 10 }
                };
            int lineCount = 0;

            var canvas = new Mock<ICanvas>();
            canvas.Setup(c => c.GetStringSize(It.IsAny<string>(), It.IsAny<IFont>(), It.IsAny<float>(), It.IsAny<HorizontalAlignment>(), It.IsAny<VerticalAlignment>()))
                  .Returns(new SizeF(30, 12));
            canvas.Setup(c => c.DrawLine(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()))
                  .Callback(() => lineCount++);

            var drawable = new GbmDrawable
            {
                GetPaths = () => paths,
                TargetYTicks = 4,
                TargetXTicks = 4
            };

            drawable.Draw(canvas.Object, new RectF(0, 0, 500, 400));

            Assert.True(lineCount > 0);
        }

        // Verifica que ao definir hover é desenhada uma linha vertical de marcação.
        [Fact]
        public void Draw_ComHover_GeraLinhaVertical()
        {
            var paths = new List<double[]>
                {
                    new double[] { 100, 102, 104, 106, 108 }
                };

            bool verticalDetectada = false;
            var canvas = new Mock<ICanvas>();
            canvas.Setup(c => c.GetStringSize(It.IsAny<string>(), It.IsAny<IFont>(), It.IsAny<float>(), It.IsAny<HorizontalAlignment>(), It.IsAny<VerticalAlignment>()))
                  .Returns(new SizeF(40, 14));

            canvas.Setup(c => c.DrawLine(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()))
                  .Callback<float, float, float, float>((x1, y1, x2, y2) =>
                  {
                      // Heurística: linha vertical tem mesmo X (~) e atravessa parte grande da área
                      if (Math.Abs(x1 - x2) < 0.01f && Math.Abs(y1 - y2) > 50)
                          verticalDetectada = true;
                  });

            var drawable = new GbmDrawable
            {
                GetPaths = () => paths
            };

            // Primeiro draw sem hover (para inicializar)
            drawable.Draw(canvas.Object, new RectF(0, 0, 400, 300));
            drawable.SetHover(new PointF(200, 150));
            drawable.Draw(canvas.Object, new RectF(0, 0, 400, 300));

            Assert.True(verticalDetectada);
        }
    }

}
