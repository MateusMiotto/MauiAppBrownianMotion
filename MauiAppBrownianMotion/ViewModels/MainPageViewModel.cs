using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiAppBrownianMotion.ViewModels
{
    public partial class MainPageViewModel : ObservableObject, INavigationViewModel
    {
        public INavigationService Navigation { get; set; }



        public MainPageViewModel()
        {
        }

        public async Task InitializeAsync(IDictionary<string, object>? parameters)
        {



        }


    }
}