using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.CompilerServices;
using Windows.Devices.Bluetooth.Advertisement;


namespace MauiAppBrownianMotion.ViewModels
{
    public partial class BrownianViewModel : ObservableObject, INavigationViewModel
    {
        [ObservableProperty] double precoInicial = 100;
        [ObservableProperty] double volatilidadePercent = 20; // a.a.
        [ObservableProperty] double retornoPercent = 1;       // a.a.
        [ObservableProperty] int tempoDias = 252;
        [ObservableProperty] int numeroSimulacoes = 1;

        // paths para desenhar
        [ObservableProperty] List<double[]> paths = new();

        public INavigationService Navigation { get; set; }

        public event Action? RedrawRequested;

        // chamado automaticamente quando Paths muda
        partial void OnPathsChanged(List<double[]> value)
            => RedrawRequested?.Invoke();

        public BrownianViewModel() { }
        public async Task InitializeAsync(IDictionary<string, object>? parameters) { }

        static double AnnualVolToDaily(double sigmaAnnual) => sigmaAnnual / Math.Sqrt(252.0);
        static double AnnualMeanToDaily(double muAnnual) => Math.Pow(1.0 + muAnnual, 1.0 / 252.0) - 1.0;

        [RelayCommand]
        public async Task GerarSimulacao()
        {
            try
            {

                double sigmaA = VolatilidadePercent / 100.0;
                double muA = RetornoPercent / 100.0;

                var sigmaD = AnnualVolToDaily(sigmaA);
                var muD = AnnualMeanToDaily(muA);

                var list = new List<double[]>();
                for (int k = 0; k < Math.Max(1, NumeroSimulacoes); k++)
                    list.Add(GenerateBromnianMotion(sigmaD, muD, PrecoInicial, Math.Max(2, TempoDias)));

                Paths = list;
                RedrawRequested?.Invoke(); // expõe action para a View
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Erro", ex.Message, "OK");
            }
        }


        public static double[] GenerateBromnianMotion(double sigma, double mean, double initialPrice, int numDays)
        {
            Random rand = new();
            double[] prices = new double[numDays];
            prices[0] = initialPrice;

            for (int i = 1; i < numDays; i++)
            {
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

                double retornoDiario = mean + sigma * z;

                prices[i] = prices[i - 1] * Math.Exp(retornoDiario);
            }
            return prices;
        }
    }
}