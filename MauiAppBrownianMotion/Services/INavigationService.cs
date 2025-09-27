using Microsoft.Maui.Controls;

namespace MauiAppBrownianMotion.Services
{
    /// <summary>
    /// Provides navigation capabilities.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigate to a route.
        /// </summary>
        /// <param name="route">Route string to navigate to.</param>
        /// <returns>A task representing the navigation.</returns>
        Task NavigateToAsync(string route);

        /// <summary>
        /// Initialize the view model for a page and inject navigation service.
        /// </summary>
        /// <param name="page">Page to initialize.</param>
        void InitializeViewModel(Page page);
    }
}