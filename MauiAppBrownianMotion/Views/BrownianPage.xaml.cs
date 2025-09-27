using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        readonly GbmDrawable drawable = new();

        public BrownianPage()
        {
            InitializeComponent();
            this.InjectViewModel();

            var vm = (BindingContext as BrownianViewModel);

            if (vm == null)
                throw new InvalidOperationException("ViewModel não pode ser nulo");

            drawable.GetPaths = () => vm.Paths;
            Chart.Drawable = drawable;

            vm.RedrawRequested += () => Chart.Invalidate();
        }
    }
}
