using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;
#if WINDOWS
using Microsoft.UI.Xaml.Input;
using System.Text;
using System.Globalization;
using MauiAppBrownianMotion.Utilities;
#endif

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        private readonly GbmDrawable drawable = new();
        private const double WideThreshold = 1100; // largura mínima para painel lateral
        double initialZoom = 1.0;
        bool pinchInProgress = false;
        double lastPanX;
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
            vm.PropertyChanged += Vm_PropertyChanged;
            ApplyInteractionMode();
            UpdateZoomLabel();
            UpdateNavSlider();
#if WINDOWS
            Chart.HandlerChanged += (_, _) => AttachWindowsEvents();
#endif
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

            // Desabilita hover pesado
            drawable.HoverEnabled = !disableInteractions;

            // Esconde overlay de zoom
            var overlay = this.FindByName<Border>("ZoomOverlay");
            if (overlay != null) overlay.IsVisible = !disableInteractions;
            var navSlider = this.FindByName<Slider>("NavSlider");
            if (navSlider != null) navSlider.IsEnabled = !disableInteractions;

            // Reseta zoom/pan se desabilitando
            if (disableInteractions)
            {
                drawable.XZoom = 1;
                drawable.XPan = 0;
                drawable.SetHover(null);
            }
            UpdateNavSlider();
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
            if (ViewModel.IsBackgroundWindow) return; // desativado
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
            if (ViewModel.IsBackgroundWindow) return; // desativado
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
            if (ViewModel.IsBackgroundWindow) return; // desativado
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
            UpdateNavSlider();
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
            UpdateNavSliderPositionOnly();
        }

        void ResetZoom()
        {
            drawable.XZoom = 1;
            drawable.XPan = 0;
            Chart.Invalidate();
            UpdateZoomLabel();
            UpdateNavSlider();
        }

        void OnZoomInClicked(object? sender, EventArgs e)
        {
            if (ViewModel.IsBackgroundWindow) return; // desativado
            initialZoom = drawable.XZoom;
            ApplyZoom(1.25, 0.5);
        }

        void OnZoomOutClicked(object? sender, EventArgs e)
        {
            if (ViewModel.IsBackgroundWindow) return; // desativado
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

        void UpdateNavSlider()
        {
            var slider = this.FindByName<Slider>("NavSlider");
            if (slider == null) return;
            bool show = drawable.XZoom > 1.0001 && !ViewModel.IsBackgroundWindow;
            slider.IsVisible = show;
            if (!show) return;
            slider.ValueChanged -= OnNavSliderValueChanged;
            slider.Value = drawable.XPan;
            slider.ValueChanged += OnNavSliderValueChanged;
        }

        void UpdateNavSliderPositionOnly()
        {
            var slider = this.FindByName<Slider>("NavSlider");
            if (slider == null) return;
            if (!slider.IsVisible) return;
            slider.ValueChanged -= OnNavSliderValueChanged;
            slider.Value = drawable.XPan;
            slider.ValueChanged += OnNavSliderValueChanged;
        }

        void OnNavSliderValueChanged(object? sender, ValueChangedEventArgs e)
        {
            if (drawable.XZoom <= 1) return; // ignorar se não está em zoom
            if (Math.Abs(drawable.XPan - e.NewValue) < 1e-6) return;
            drawable.XPan = Math.Clamp(e.NewValue, 0, 1);
            Chart.Invalidate();
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
            if (ViewModel.IsBackgroundWindow) return; // desativado
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
            if (ViewModel.IsBackgroundWindow) return;

            // Clique direito: mostrar médias por série
            if (pt.Properties.IsRightButtonPressed)
            {
                ShowSeriesMeans();
                e.Handled = true;
                return; // não inicia pan com botão direito
            }

            // Pan com botão do meio ou (botão esquerdo + zoom ativo)
            if (pt.Properties.IsMiddleButtonPressed || (pt.Properties.IsLeftButtonPressed && drawable.XZoom > 1))
            {
                isMousePanning = true;
                lastMouseX = pt.Position.X;
                e.Handled = true;
            }

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
            if (ViewModel.IsBackgroundWindow) return;

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

        void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            drawable.SetHover(null);
            Chart.Invalidate();
        }

        void ShowSeriesMeans()
        {
            try
            {
                var paths = ViewModel.Paths;
                if (paths == null || paths.Count == 0)
                {
                    _ = MainThread.InvokeOnMainThreadAsync(() => DisplayAlert("Médias", "Nenhuma série carregada.", "OK"));
                    return;
                }
                int maxLines = 100;
                var sb = new StringBuilder();
                double globalSum = 0; long globalCount = 0;
                for (int i = 0; i < paths.Count; i++)
                {
                    var arr = paths[i];
                    if (arr == null || arr.Length == 0) continue;
                    double sum = 0;
                    for (int k = 0; k < arr.Length; k++) sum += arr[k];
                    double avg = sum / arr.Length;
                    globalSum += sum; globalCount += arr.Length;
                    if (sb.Length < 6000)
                        sb.AppendLine($"Série {i + 1}: média = {NumberFormatUtils.FormatPriceCompact(avg)}");
                    if (i + 1 >= maxLines && i + 1 < paths.Count)
                    {
                        sb.AppendLine($"... (+{paths.Count - maxLines} séries)");
                        break;
                    }
                }
                if (globalCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Média agregada: {NumberFormatUtils.FormatPriceCompact(globalSum / globalCount)}");
                }
                string text = sb.ToString();
                if (string.IsNullOrWhiteSpace(text)) text = "Sem dados válidos.";
                _ = MainThread.InvokeOnMainThreadAsync(() => DisplayAlert("Média por Série", text, "OK"));
            }
            catch (Exception ex)
            {
                _ = MainThread.InvokeOnMainThreadAsync(() => DisplayAlert("Erro", ex.Message, "OK"));
            }
        }
#endif
    }
}
