using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiAppBrownianMotion.Services;
using MauiAppBrownianMotion.ViewModels.Base;
using MauiAppBrownianMotion.Utilities;

namespace MauiAppBrownianMotion.Views.Base
{
    /// <summary>
    /// Provides helper methods for MAUI views to inject their corresponding view model
    /// based on a naming convention where <c>PaginaPage</c> maps to <c>PaginaViewModel</c>.
    /// </summary>
    public interface IMauiView
    {
        /// <summary>
        /// Injects the matching view model using the provided service provider.
        /// </summary>
        /// <param name="serviceProvider">Application service provider.</param>
        void InjectViewModel(IServiceProvider serviceProvider)
        {
            if (this is not Page page)
                return;

            var vmTypeName = page.GetType().FullName!
                .Replace(".Pages.", ".ViewModels.")
                .Replace("Page", "ViewModel");
            var vmType = Type.GetType(vmTypeName);
            if (vmType is null)
                return;

            var vm = ActivatorUtilities.CreateInstance(serviceProvider, vmType);
            if (vm is INavigationViewModel navigationViewModel)
                navigationViewModel.Navigation = serviceProvider.GetRequiredService<INavigationService>();

            page.BindingContext = vm;
        }
    }

    /// <summary>
    /// Convenience extensions for <see cref="IMauiView"/> implementations.
    /// </summary>
    public static class MauiViewExtensions
    {
        /// <summary>
        /// Injects the view model using the application's service provider.
        /// </summary>
        /// <param name="view">View implementing <see cref="IMauiView"/>.</param>
        public static void InjectViewModel(this IMauiView view)
        {
            var services = Application.Current?.Handler?.MauiContext?.Services ?? ServiceHelper.Services;
            if (services is null)
                return;

            view.InjectViewModel(services);

            if (view is Page page && page.BindingContext is INavigationViewModel navigationViewModel)
                navigationViewModel.InitializeAsync(null).FireAndForgetSafeAsync();
        }
    }
}
