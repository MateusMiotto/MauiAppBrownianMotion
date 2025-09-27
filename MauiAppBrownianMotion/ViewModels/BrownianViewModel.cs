using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace MauiAppBrownianMotion.ViewModels
{
    public partial class BrownianViewModel : ObservableObject, INavigationViewModel
    {
        #region Numeric (parsed) properties
        // Propriedades numéricas internas usadas no cálculo
        [ObservableProperty] double precoInicial = 100;
        [ObservableProperty] double volatilidadePercent = 20; // % a.a.
        [ObservableProperty] double retornoPercent = 1;       // % a.a.
        [ObservableProperty] int tempoDias = 252;
        [ObservableProperty] int numeroSimulacoes = 1;
        #endregion

        #region Input (text) properties bound to Entry (aceitam vazio)
        // Propriedades de entrada (texto) ligadas aos Entry - permitem campo vazio
        [ObservableProperty] string precoInicialInput = "100";
        [ObservableProperty] string volatilidadePercentInput = "20";
        [ObservableProperty] string retornoPercentInput = "1";
        [ObservableProperty] string tempoDiasInput = "252";
        [ObservableProperty] string numeroSimulacoesInput = "1";
        #endregion

        #region Output / UI data
        // paths para desenhar
        [ObservableProperty] List<double[]> paths = new();
        #endregion

        #region Services
        public INavigationService Navigation { get; set; }
        #endregion

        #region Events
        public event Action? RedrawRequested;

        // chamado automaticamente quando Paths muda
        partial void OnPathsChanged(List<double[]> value)
            => RedrawRequested?.Invoke();
        #endregion

        #region Construction / Initialization
        public BrownianViewModel() { }
        public async Task InitializeAsync(IDictionary<string, object>? parameters) { }
        #endregion

        #region Commands (Gerar Simulação)
        // CanExecute property usado pelo RelayCommand
        public bool CanGerarSimulacao => ValidateInputs(false, out _);

        [RelayCommand(CanExecute = nameof(CanGerarSimulacao))]
        public async Task GerarSimulacao()
        {
            try
            {
                if (!ValidateInputs(true, out var validationError))
                {
                    await Application.Current?.MainPage?.DisplayAlert("Validação", validationError, "OK");
                    return;
                }

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
        #endregion

        #region Validation
        // Validação central reutilizada por CanExecute e execução
        bool ValidateInputs(bool canShowError, out string? error)
        {
            error = null;
            var culture = CultureInfo.CurrentCulture;

            bool ParseDouble(string? txt, string field, bool mustBePositive, out double value, out string? err)
            {
                value = 0;
                err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!double.TryParse(txt, NumberStyles.Float, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
                if (value < 0) { err = $"Campo '{field}' não pode ser negativo"; return false; }
                if (mustBePositive && value <= 0) { err = $"Campo '{field}' deve ser maior que 0"; return false; }
                return true;
            }
            bool ParseInt(string? txt, string field, int min, out int value, out string? err)
            {
                value = 0;
                err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!int.TryParse(txt, NumberStyles.Integer, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
                if (value < 0) { err = $"Campo '{field}' não pode ser negativo"; return false; }
                if (value < min) { err = $"Campo '{field}' deve ser >= {min}"; return false; }
                return true;
            }

            if (!ParseDouble(PrecoInicialInput, "Preço inicial", true, out var preco, out var e1)) { if (canShowError) error = e1; return false; }
            if (!ParseDouble(VolatilidadePercentInput, "Volatilidade", false, out var vol, out var e2)) { if (canShowError) error = e2; return false; }
            if (!ParseDouble(RetornoPercentInput, "Retorno", false, out var ret, out var e3)) { if (canShowError) error = e3; return false; }
            if (!ParseInt(TempoDiasInput, "Tempo (dias)", 1, out var dias, out var e4)) { if (canShowError) error = e4; return false; }
            if (!ParseInt(NumeroSimulacoesInput, "Nº simulações", 1, out var sims, out var e5)) { if (canShowError) error = e5; return false; }

            if (canShowError)
            {
                PrecoInicial = preco;
                VolatilidadePercent = vol;
                RetornoPercent = ret;
                TempoDias = dias;
                NumeroSimulacoes = sims;
            }
            return true;
        }
        #endregion

        #region Simulation helpers
        static double AnnualVolToDaily(double sigmaAnnual) => sigmaAnnual / Math.Sqrt(252.0);
        static double AnnualMeanToDaily(double muAnnual) => Math.Pow(1.0 + muAnnual, 1.0 / 252.0) - 1.0;

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
        #endregion

        #region Property change handlers & helpers
        void RaiseCanExecute()
        {
            OnPropertyChanged(nameof(CanGerarSimulacao));
            GerarSimulacaoCommand.NotifyCanExecuteChanged();
        }

        // Dispara reavaliação do CanExecute quando qualquer input muda
        partial void OnPrecoInicialInputChanged(string value) => RaiseCanExecute();
        partial void OnVolatilidadePercentInputChanged(string value) => RaiseCanExecute();
        partial void OnRetornoPercentInputChanged(string value) => RaiseCanExecute();
        partial void OnTempoDiasInputChanged(string value) => RaiseCanExecute();
        partial void OnNumeroSimulacoesInputChanged(string value) => RaiseCanExecute();
        #endregion
    }
}