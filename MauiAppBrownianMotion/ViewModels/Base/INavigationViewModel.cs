namespace MauiAppBrownianMotion.ViewModels.Base
{
    /// <summary>
    /// Provides navigation service access for view models.
    /// </summary>
    public interface INavigationViewModel
    {
        /// <summary>
        /// Navigation service.
        /// </summary>
        INavigationService Navigation { get; set; }

        /// <summary>
        /// Asynchronously initializes the view model when navigation occurs.
        /// </summary>
        /// <param name="parameters">Query parameters passed during navigation.</param>
        Task InitializeAsync(IDictionary<string, object>? parameters);
    }
}