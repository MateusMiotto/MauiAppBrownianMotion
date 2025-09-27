using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        readonly GbmDrawable drawable = new();
        const double WideThreshold = 1100; // largura mínima para painel lateral

        public BrownianPage()
        {
            InitializeComponent();
            this.InjectViewModel();

            var vm = (BindingContext as BrownianViewModel) ?? throw new InvalidOperationException("ViewModel não pode ser nulo");

            drawable.GetPaths = () => vm.Paths;
            Chart.Drawable = drawable;
            vm.RedrawRequested += () => Chart.Invalidate();

            SizeChanged += BrownianPage_SizeChanged;
        }

        void BrownianPage_SizeChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            var state = Width >= WideThreshold ? "Wide" : "Narrow";
            VisualStateManager.GoToState(LayoutGrid, state);
#endif
        }
    }
}
