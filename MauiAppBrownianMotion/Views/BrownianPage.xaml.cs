using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;
#if WINDOWS
using Microsoft.UI.Xaml.Input;
#endif

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        /// <summary>
        /// Drawable responsável por desenhar as trajetórias no componente de gráfico.
        /// Sua fonte de dados (<see cref="GbmDrawable.GetPaths"/>) é atribuída dinamicamente a partir do ViewModel.
        /// </summary>
        private readonly GbmDrawable drawable = new();

        /// <summary>
        /// Largura mínima (em pixels) para alternar o VisualState para "Wide".
        /// Abaixo desse valor o estado "Narrow" é aplicado.
        /// </summary>
        private const double WideThreshold = 1100; // largura mínima para painel lateral

        // Estado de gesto touch
        double initialZoom = 1.0;
        bool pinchInProgress = false;
        double lastPanX; // acumular delta horizontal

#if WINDOWS
        // Estado de gesto mouse (Windows)
        bool isMousePanning = false;
        double lastMouseX;
#endif

        /// <summary>
        /// Ctor Inicializa componentes, injeta o ViewModel, associa o drawable  ao controle de gráfico e registra callback para solicitar redesenho.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Lançada caso o ViewModel não seja resolvido corretamente pela injeção.
        /// </exception>
        public BrownianPage()
        {
            InitializeComponent();
            this.InjectViewModel();

            var vm = (BindingContext as BrownianViewModel) ?? throw new InvalidOperationException("ViewModel não pode ser nulo");

            // Conecta a função de obtenção de paths do ViewModel ao drawable
            drawable.GetPaths = () => vm.Paths;

            // Define o drawable no componente gráfico (ex: GraphicsView)
            Chart.Drawable = drawable;

            // Solicita invalidação/redesenho quando o ViewModel indicar
            vm.RedrawRequested += () => Chart.Invalidate();

            // Adapta layout responsivo 
            SizeChanged += BrownianPage_SizeChanged;

            UpdateZoomLabel();

#if WINDOWS
            // Anexa eventos nativos do Windows para suporte a zoom/pan via mouse + hover
            Chart.HandlerChanged += (_, _) => AttachWindowsEvents();
#endif
        }

        /// <summary>
        /// Manipula o evento de alteração de tamanho da página para alternar entre estados visuais responsivos.
        /// </summary>
        private void BrownianPage_SizeChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            var state = Width >= WideThreshold ? "Wide" : "Narrow";
            VisualStateManager.GoToState(LayoutGrid, state);
#endif
        }

        // Pinch (zoom horizontal) - Touch
        void OnChartPinch(object? sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Started)
            {
                pinchInProgress = true;
                initialZoom = drawable.XZoom;
            }
            else if (e.Status == GestureStatus.Running && pinchInProgress)
            {
                ApplyZoom(e.Scale, e.ScaleOrigin.X);
            }
            else if (e.Status == GestureStatus.Completed || e.Status == GestureStatus.Canceled)
            {
                pinchInProgress = false;
            }
        }

        // Pan (deslocamento horizontal) - Touch
        void OnChartPan(object? sender, PanUpdatedEventArgs e)
        {
            if (pinchInProgress) return; // evita conflito
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    lastPanX = 0;
                    break;
                case GestureStatus.Running:
                    ApplyPanDelta(e.TotalX - lastPanX, Chart.Width);
                    lastPanX = e.TotalX;
                    break;
            }
        }

        // Double tap para resetar zoom
        void OnChartDoubleTap(object? sender, TappedEventArgs e)
        {
            ResetZoom();
        }

        void ApplyZoom(double relativeScale, double focusXFraction)
        {
            double factor = relativeScale;
            double newZoom = initialZoom * factor;
            if (newZoom < 1) newZoom = 1;
            if (newZoom > 200) newZoom = 200; // limite arbitrário

            // Ajusta pan para manter foco
            double oldVisibleFraction = 1.0 / drawable.XZoom;
            double newVisibleFraction = 1.0 / newZoom;
            double currentPan = drawable.XPan;
            double focusGlobal = currentPan + focusXFraction * oldVisibleFraction;
            double newPan = focusGlobal - focusXFraction * newVisibleFraction;

            drawable.XZoom = newZoom;
            drawable.XPan = drawable.XZoom <= 1
                          ? 0
                          : Math.Clamp(newPan, 0, 1); Chart.Invalidate();
            UpdateZoomLabel();
        }

        void ApplyPanDelta(double deltaPixels, double widthPixels)
        {
            if (drawable.XZoom <= 1) return; // nada a fazer
            if (widthPixels <= 0) return;
            double visibleFraction = 1.0 / drawable.XZoom;
            double fracDelta = -deltaPixels / widthPixels * visibleFraction; // sinal invertido
            double newPan = drawable.XPan + fracDelta;
            drawable.XPan = Math.Clamp(newPan, 0, 1);
            Chart.Invalidate();
        }

        void ResetZoom()
        {
            drawable.XZoom = 1;
            drawable.XPan = 0;
            Chart.Invalidate();
            UpdateZoomLabel();
        }

        void OnZoomInClicked(object? sender, EventArgs e)
        {
            initialZoom = drawable.XZoom;
            ApplyZoom(1.25, 0.5); // zoom central
        }

        void OnZoomOutClicked(object? sender, EventArgs e)
        {
            initialZoom = drawable.XZoom;
            ApplyZoom(0.8, 0.5); // zoom out central
        }

        void UpdateZoomLabel()
        {
            var lbl = this.FindByName<Label>("ZoomLabel");
            if (lbl != null)
            {
                double z = drawable.XZoom;
                if (z < 1.0001) z = 1; // mostrar 1x quando resetado
                lbl.Text = z >= 10 ? $"{z:0}x" : z >= 2 ? $"{z:0.#}x" : $"{z:0.##}x";
            }
        }

#if WINDOWS
        void AttachWindowsEvents()
        {
            if (Chart?.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                fe.PointerWheelChanged -= OnPointerWheelChanged;
                fe.PointerWheelChanged += OnPointerWheelChanged;
                fe.PointerPressed -= OnPointerPressed;
                fe.PointerPressed += OnPointerPressed;
                fe.PointerReleased -= OnPointerReleased;
                fe.PointerReleased += OnPointerReleased;
                fe.PointerMoved -= OnPointerMoved;
                fe.PointerMoved += OnPointerMoved;
                fe.PointerExited -= OnPointerExited;
                fe.PointerExited += OnPointerExited;
            }
        }

        void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (Chart.Width <= 0) return;
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
            int delta = pt.Properties.MouseWheelDelta; // positivo para cima
            if (delta == 0) return;

            // Fator incremental pequeno para suavidade
            double zoomFactor = delta > 0 ? 1.15 : 0.87; // ~ +15% / -13%
            initialZoom = drawable.XZoom; // base atual
            double focus = Math.Clamp(pt.Position.X / Chart.Width, 0, 1);
            ApplyZoom(zoomFactor, focus);
            UpdateZoomLabel();
            e.Handled = true;
        }

        void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
            if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsRightButtonPressed || (pt.Properties.IsLeftButtonPressed && drawable.XZoom > 1))
            {
                isMousePanning = true;
                lastMouseX = pt.Position.X;
                e.Handled = true;
            }

            // Atualiza hover imediatamente no click
            drawable.SetHover(new PointF((float)pt.Position.X, (float)pt.Position.Y));
            Chart.Invalidate();
        }

        void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            isMousePanning = false;
        }

        void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);

            // Atualiza posição de hover sempre que o mouse se move sobre o gráfico
            drawable.SetHover(new PointF((float)pt.Position.X, (float)pt.Position.Y));
            Chart.Invalidate();

            // Se estiver em modo pan, aplica deslocamento
            if (isMousePanning)
            {
                double delta = pt.Position.X - lastMouseX;
                lastMouseX = pt.Position.X;
                ApplyPanDelta(delta, Chart.Width);
                e.Handled = true;
            }
        }

        void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Limpa hover ao sair da área
            drawable.SetHover(null);
            Chart.Invalidate();
        }
#endif
    }
}
