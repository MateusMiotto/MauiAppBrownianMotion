using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;
#if WINDOWS
using Microsoft.UI.Xaml.Input;
#endif

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        private readonly GbmDrawable drawable = new();
        private const double WideThreshold = 1100; // largura mínima para painel lateral

        // Estado de gesto touch
        double initialZoom = 1.0;
        bool pinchInProgress = false;
        double lastPanX; // acumular delta horizontal

#if WINDOWS
        bool isMousePanning = false;
        double lastMouseX;
#endif

        BrownianViewModel ViewModel => (BindingContext as BrownianViewModel)!;

        public BrownianPage()
        {
            InitializeComponent();
            this.InjectViewModel();
            var vm = ViewModel ?? throw new InvalidOperationException("ViewModel não pode ser nulo");

            drawable.GetPaths = () => vm.Paths;
            Chart.Drawable = drawable;
            vm.RedrawRequested += () => Chart.Invalidate();
            SizeChanged += BrownianPage_SizeChanged;
            vm.PropertyChanged += Vm_PropertyChanged; // observar IsBackgroundWindow
            ApplyInteractionMode();
            ConfigureZoomButtons();
            UpdateZoomLabel();
#if WINDOWS
            Chart.HandlerChanged += (_, _) => AttachWindowsEvents();
#endif
        }

        void ConfigureZoomButtons()
        {
            // Usa alguns ícones do FluentUI (fallback para texto se classe não estiver completa em runtime)
            try
            {
                var fontFamily = FluentUI.FontFamily; // assumindo classe gerada
                if (ZoomOutButton != null)
                {
                    ZoomOutButton.Text = FluentUI.subtract_20_regular;
                    ZoomOutButton.FontFamily = fontFamily;
                    ZoomOutButton.FontSize = 18;
                }
                if (ZoomInButton != null)
                {
                    ZoomInButton.Text = FluentUI.add_20_regular;
                    ZoomInButton.FontFamily = fontFamily;
                    ZoomInButton.FontSize = 18;
                }
                if (ZoomResetButton != null)
                {
                    // ícone de reset
                    ZoomResetButton.Text = FluentUI.arrow_reset_20_regular;
                    ZoomResetButton.FontFamily = fontFamily;
                    ZoomResetButton.FontSize = 18;
                }
            }
            catch
            {
                // fallback simples
                if (ZoomOutButton != null && string.IsNullOrWhiteSpace(ZoomOutButton.Text)) ZoomOutButton.Text = "-";
                if (ZoomInButton != null && string.IsNullOrWhiteSpace(ZoomInButton.Text)) ZoomInButton.Text = "+";
                if (ZoomResetButton != null && string.IsNullOrWhiteSpace(ZoomResetButton.Text)) ZoomResetButton.Text = "R";
            }
        }

        void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrownianViewModel.IsBackgroundWindow))
            {
                ApplyInteractionMode();
            }
        }

        void ApplyInteractionMode()
        {
            bool disableInteractions = ViewModel.IsBackgroundWindow; // janela pesada
            drawable.HoverEnabled = !disableInteractions;
            var overlay = this.FindByName<Border>("ZoomOverlay");
            if (overlay != null) overlay.IsVisible = !disableInteractions;
            if (disableInteractions)
            {
                drawable.XZoom = 1;
                drawable.XPan = 0;
                drawable.SetHover(null);
            }
        }

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
            if (ViewModel.IsBackgroundWindow) return;
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
            if (ViewModel.IsBackgroundWindow) return;
            if (pinchInProgress) return;
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

        void OnChartDoubleTap(object? sender, TappedEventArgs e)
        {
            if (ViewModel.IsBackgroundWindow) return;
            ResetZoom();
        }

        void ApplyZoom(double relativeScale, double focusXFraction)
        {
            double factor = relativeScale;
            double newZoom = initialZoom * factor;
            if (newZoom < 1) newZoom = 1;
            if (newZoom > 200) newZoom = 200;
            double oldVisibleFraction = 1.0 / drawable.XZoom;
            double newVisibleFraction = 1.0 / newZoom;
            double currentPan = drawable.XPan;
            double focusGlobal = currentPan + focusXFraction * oldVisibleFraction;
            double newPan = focusGlobal - focusXFraction * newVisibleFraction;
            drawable.XZoom = newZoom;
            drawable.XPan = drawable.XZoom <= 1 ? 0 : Math.Clamp(newPan, 0, 1);
            Chart.Invalidate();
            UpdateZoomLabel();
        }

        void ApplyPanDelta(double deltaPixels, double widthPixels)
        {
            if (drawable.XZoom <= 1) return;
            if (widthPixels <= 0) return;
            double visibleFraction = 1.0 / drawable.XZoom;
            double fracDelta = -deltaPixels / widthPixels * visibleFraction;
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
            if (ViewModel.IsBackgroundWindow) return;
            initialZoom = drawable.XZoom;
            ApplyZoom(1.25, 0.5);
        }

        void OnZoomOutClicked(object? sender, EventArgs e)
        {
            if (ViewModel.IsBackgroundWindow) return;
            initialZoom = drawable.XZoom;
            ApplyZoom(0.8, 0.5);
        }

        void OnZoomResetClicked(object? sender, EventArgs e)
        {
            if (ViewModel.IsBackgroundWindow) return;
            ResetZoom();
        }

        void UpdateZoomLabel()
        {
            var lbl = this.FindByName<Label>("ZoomLabel");
            if (lbl != null)
            {
                double z = drawable.XZoom;
                if (z < 1.0001) z = 1;
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
            if (ViewModel.IsBackgroundWindow) return;
            if (Chart.Width <= 0) return;
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
            int delta = pt.Properties.MouseWheelDelta;
            if (delta == 0) return;
            double zoomFactor = delta > 0 ? 1.15 : 0.87;
            initialZoom = drawable.XZoom;
            double focus = Math.Clamp(pt.Position.X / Chart.Width, 0, 1);
            ApplyZoom(zoomFactor, focus);
            UpdateZoomLabel();
            e.Handled = true;
        }

        void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
            if (!ViewModel.IsBackgroundWindow)
            {
                if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsRightButtonPressed || (pt.Properties.IsLeftButtonPressed && drawable.XZoom > 1))
                {
                    isMousePanning = true;
                    lastMouseX = pt.Position.X;
                    e.Handled = true;
                }
                drawable.SetHover(new PointF((float)pt.Position.X, (float)pt.Position.Y));
                Chart.Invalidate();
            }
        }

        void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            isMousePanning = false;
        }

        void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
            if (!ViewModel.IsBackgroundWindow)
            {
                drawable.SetHover(new PointF((float)pt.Position.X, (float)pt.Position.Y));
                Chart.Invalidate();
                if (isMousePanning)
                {
                    double delta = pt.Position.X - lastMouseX;
                    lastMouseX = pt.Position.X;
                    ApplyPanDelta(delta, Chart.Width);
                    e.Handled = true;
                }
            }
        }

        void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            drawable.SetHover(null);
            Chart.Invalidate();
        }
#endif
    }
}
