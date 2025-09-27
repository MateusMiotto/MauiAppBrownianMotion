using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.CompilerServices;
using Windows.Devices.Bluetooth.Advertisement;


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



        [RelayCommand]
        public async Task GerarSimulacao()
        {


        }





        public static double[] GenerateBromnianMotion(double sigma, double mean, double initialPrice, int numDays)
        {
            Random rand = new();
            double[] prices = new double[numDays];
            prices[0] = initialPrice;

            for (int i = 1; 1 < numDays; i++)
            {
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

                double retornoDiario = mean + sigma * 7;

                prices[i] = prices[i - 1] * Math.Exp(retornoDiario);
            }
            return prices;
        }
    }
}