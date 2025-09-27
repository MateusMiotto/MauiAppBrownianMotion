using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace MauiAppBrownianMotion.ViewModels
{
    public partial class BrownianViewModel : ObservableObject, INavigationViewModel
    {
        #region Numeric (parsed) properties
        // Propriedades numéricas internas usadas no cálculo
        [ObservableProperty] double precoInicial = 100;
        [ObservableProperty] double volatilidadePercent = 20; // % (a.a. ou a.m.)
        [ObservableProperty] double retornoPercent = 1;       // % (a.a. ou a.m.)
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

        #region Flags / Options
        private bool usarPeriodicidadeMensal; // false = anual, true = mensal
        public bool UsarPeriodicidadeMensal
        {
            get => usarPeriodicidadeMensal;
            set
            {
                if (SetProperty(ref usarPeriodicidadeMensal, value))
                {
                    OnPropertyChanged(nameof(PeriodicidadeSufixo));
                    RaiseCanExecute();
                }
            }
        }
        public string PeriodicidadeSufixo => UsarPeriodicidadeMensal ? "a.m." : "a.a.";
        #endregion

        #region Output / UI data
        [ObservableProperty] List<double[]> paths = new();
        private bool isProcessing;
        public bool IsProcessing
        {
            get => isProcessing;
            set
            {
                if (SetProperty(ref isProcessing, value))
                {
                    if (value) StartProcessingTimer(); else StopProcessingTimer();
                    RaiseCanExecute(); // Atualiza comandos Gerar / Cancelar
                }
            }
        }

        [ObservableProperty] string processingTime = "00:00"; // mm:ss
        #endregion

        #region Timer infra
        IDispatcherTimer? processingTimer;
        Stopwatch? processingStopwatch;

        void StartProcessingTimer()
        {
            processingStopwatch = Stopwatch.StartNew();
            ProcessingTime = "00:00";
            processingTimer = Application.Current?.Dispatcher.CreateTimer();
            if (processingTimer is null) return;
            processingTimer.Interval = TimeSpan.FromSeconds(1);
            processingTimer.IsRepeating = true;
            processingTimer.Tick += (_, _) =>
            {
                if (processingStopwatch is null) return;
                ProcessingTime = processingStopwatch.Elapsed.ToString("mm':'ss");
            };
            processingTimer.Start();
        }
        void StopProcessingTimer()
        {
            processingTimer?.Stop();
            processingTimer = null;
            if (processingStopwatch != null)
            {
                ProcessingTime = processingStopwatch.Elapsed.ToString("mm':'ss");
                processingStopwatch.Stop();
                processingStopwatch = null;
            }
        }
        #endregion

        #region Services
        public INavigationService Navigation { get; set; }
        #endregion

        #region Events
        public event Action? RedrawRequested;
        partial void OnPathsChanged(List<double[]> value) => RedrawRequested?.Invoke();
        #endregion

        #region Cancellation infra
        CancellationTokenSource? simulationCts;
        #endregion

        #region Construction / Initialization
        public BrownianViewModel()
        {
            CancelarCommand = new RelayCommand(Cancelar, () => CanCancelar);
        }
        public async Task InitializeAsync(IDictionary<string, object>? parameters) { }
        #endregion

        #region Heavy threshold dinâmico
        // Base calibrada para manter ~3.000.000 pontos em uma máquina de 8 cores (375k * 8)
        private const int BasePointsPerCore = 875_000;
        public static int HeavyThresholdPoints => BasePointsPerCore * Environment.ProcessorCount;
        #endregion

        #region Contador de janelas em segundo plano
        static int backgroundWindowSequence;
        static int NextBackgroundSequence() => Interlocked.Increment(ref backgroundWindowSequence);
        public bool IsBackgroundWindow { get; internal set; }
        #endregion

        #region Commands (Gerar / Cancelar)
        public bool CanGerarSimulacao => !IsProcessing && ValidateInputs(false, out _);
        public bool CanCancelar => IsProcessing;

        public IRelayCommand CancelarCommand { get; }

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

                if (!await ValidateHeavy()) return;

                await RunSimulationAsync(heavy: false);
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async Task<bool> ValidateHeavy()
        {
            int points = NumeroSimulacoes * TempoDias;
            if (points > HeavyThresholdPoints)
            {
                bool abrir = await Application.Current!.MainPage.DisplayAlert(
                    "Simulação demorada",
                    $"A simulação possui {points:N0} pontos (limite dinâmico {HeavyThresholdPoints:N0}) e pode demorar. Deseja abrir em uma nova janela e processar em segundo plano?",
                    "Abrir nova janela",
                    "Cancelar");
                if (!abrir) return false;

                var newPage = new MauiAppBrownianMotion.Pages.BrownianPage();
                var vm2 = (newPage.BindingContext as BrownianViewModel)!;
                vm2.PrecoInicialInput = PrecoInicialInput;
                vm2.VolatilidadePercentInput = VolatilidadePercentInput;
                vm2.RetornoPercentInput = RetornoPercentInput;
                vm2.TempoDiasInput = TempoDiasInput;
                vm2.NumeroSimulacoesInput = NumeroSimulacoesInput;
                vm2.UsarPeriodicidadeMensal = UsarPeriodicidadeMensal;
                vm2.IsBackgroundWindow = true;

                int seq = NextBackgroundSequence();
                string title = $"Solicitação de Execução em Segundo plano #{seq}";
                newPage.Title = title; // mantém coerência interna
                var window = new Window(newPage)
                {
                    Title = title // garante título da janela no Windows
                };

                Application.Current!.OpenWindow(window);
                _ = vm2.RunSimulationAsync(heavy: true);
                return false;
            }
            return true;
        }

        void Cancelar()
        {
            simulationCts?.Cancel();
            if (IsBackgroundWindow)
            {
                var win = Application.Current?.Windows.FirstOrDefault(w => w.Page?.BindingContext == this);
                if (win != null)
                {
                    Application.Current?.CloseWindow(win);
                }
            }
        }
        #endregion

        #region Simulation execution
        async Task RunSimulationAsync(bool heavy)
        {
            // Cancela execução anterior (se houver)
            simulationCts?.Cancel();
            simulationCts?.Dispose();
            var cts = new CancellationTokenSource();
            simulationCts = cts;
            var token = cts.Token;

            try
            {
                IsProcessing = true;
                if (!ValidateInputs(true, out var validationError))
                {
                    await Application.Current?.MainPage?.DisplayAlert("Validação", validationError, "OK");
                    return;
                }

                double sigmaInput = VolatilidadePercent / 100.0;
                double muInput = RetornoPercent / 100.0;

                double sigmaD = UsarPeriodicidadeMensal ? MonthlyVolToDaily(sigmaInput) : AnnualVolToDaily(sigmaInput);
                double muD = UsarPeriodicidadeMensal ? MonthlyMeanToDaily(muInput) : AnnualMeanToDaily(muInput);

                int sims = Math.Max(1, NumeroSimulacoes);
                int dias = Math.Max(2, TempoDias);

                List<double[]> result = heavy
                    ? await Task.Run(() => GeneratePaths(sims, dias, sigmaD, muD, PrecoInicial, token), token)
                    : GeneratePaths(sims, dias, sigmaD, muD, PrecoInicial, token);

                if (!token.IsCancellationRequested)
                    Paths = result;
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                if (simulationCts == cts)
                {
                    simulationCts.Dispose();
                    simulationCts = null;
                }
                IsProcessing = false;
            }
        }

        static List<double[]> GeneratePaths(int sims, int dias, double sigmaD, double muD, double precoInicial, CancellationToken token)
        {
            var list = new List<double[]>(sims);
            for (int k = 0; k < sims; k++)
            {
                token.ThrowIfCancellationRequested();
                list.Add(GenerateBromnianMotion(sigmaD, muD, precoInicial, dias, token));
            }
            return list;
        }
        #endregion

        #region Validation
        bool ValidateInputs(bool canShowError, out string? error)
        {
            error = null;
            var culture = CultureInfo.CurrentCulture;

            bool ParseDouble(string? txt, string field, bool mustBePositive, out double value, out string? err)
            {
                value = 0;
                err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!double.TryParse(txt, System.Globalization.NumberStyles.Float, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
                if (value < 0) { err = $"Campo '{field}' não pode ser negativo"; return false; }
                if (mustBePositive && value <= 0) { err = $"Campo '{field}' deve ser maior que 0"; return false; }
                return true;
            }
            bool ParseInt(string? txt, string field, int min, out int value, out string? err)
            {
                value = 0;
                err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!int.TryParse(txt, System.Globalization.NumberStyles.Integer, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
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
        static double MonthlyVolToDaily(double sigmaMonthly) => sigmaMonthly / Math.Sqrt(21.0);
        static double MonthlyMeanToDaily(double muMonthly) => Math.Pow(1.0 + muMonthly, 1.0 / 21.0) - 1.0;

        public static double[] GenerateBromnianMotion(double sigma, double mean, double initialPrice, int numDays, CancellationToken token)
        {
            Random rand = new();
            double[] prices = new double[numDays];
            prices[0] = initialPrice;

            for (int i = 1; i < numDays; i++)
            {
                token.ThrowIfCancellationRequested();
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
            OnPropertyChanged(nameof(CanCancelar));
            GerarSimulacaoCommand.NotifyCanExecuteChanged();
            CancelarCommand.NotifyCanExecuteChanged();
        }
        partial void OnPrecoInicialInputChanged(string value) => RaiseCanExecute();
        partial void OnVolatilidadePercentInputChanged(string value) => RaiseCanExecute();
        partial void OnRetornoPercentInputChanged(string value) => RaiseCanExecute();
        partial void OnTempoDiasInputChanged(string value) => RaiseCanExecute();
        partial void OnNumeroSimulacoesInputChanged(string value) => RaiseCanExecute();
        #endregion
    }
}