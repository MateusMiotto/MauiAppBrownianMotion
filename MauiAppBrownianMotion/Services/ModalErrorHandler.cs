using MauiAppBrownianMotion.Utilities;

namespace MauiAppBrownianMotion.Services
{
    /// <summary>
    /// Modal Error Handler.
    /// </summary>
    public class ModalErrorHandler : IErrorHandler
    {
        SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Handle error in UI.
        /// </summary>
        /// <param name="ex">Exception.</param>
        public void HandleError(Exception ex)
        {
            DisplayAlert(ex).FireAndForgetSafeAsync();
        }

        async Task DisplayAlert(Exception ex)
        {
            try
            {
                await _semaphore.WaitAsync();
                var page = Application.Current?.Windows.LastOrDefault()?.Page;
                if (page is not null)
                    await page.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}