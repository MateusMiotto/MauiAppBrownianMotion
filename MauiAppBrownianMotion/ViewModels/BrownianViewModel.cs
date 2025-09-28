using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;

namespace MauiAppBrownianMotion.ViewModels
{
    public partial class BrownianViewModel : ObservableObject, INavigationViewModel
    {
        public const double PriceFloor = 0.01; // piso mínimo para preço simulado

        #region Numeric (parsed) properties
        [ObservableProperty] double precoInicial = 100;
        [ObservableProperty] double volatilidadePercent = 20;
        [ObservableProperty] double retornoPercent = 1;
        [ObservableProperty] int tempoDias = 252;
        [ObservableProperty] int numeroSimulacoes = 1;
        #endregion

        #region Input (text) properties
        [ObservableProperty] string precoInicialInput = "100";
        [ObservableProperty] string volatilidadePercentInput = "20";
        [ObservableProperty] string retornoPercentInput = "1";
        [ObservableProperty] string tempoDiasInput = "252";
        [ObservableProperty] string numeroSimulacoesInput = "1";
        #endregion

        #region Output / UI
        private const string ProcessingTimeFormat = @"mm\:ss\.fff";
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
                    RaiseCanExecute();
                }
            }
        }
        [ObservableProperty] string processingTime = "00:00.000";
        #endregion

        #region Timer
        IDispatcherTimer? processingTimer;
        Stopwatch? processingStopwatch;
        void StartProcessingTimer()
        {
            processingStopwatch = Stopwatch.StartNew();
            ProcessingTime = TimeSpan.Zero.ToString(ProcessingTimeFormat);
            processingTimer = Application.Current?.Dispatcher.CreateTimer();
            if (processingTimer is null) return;
            processingTimer.Interval = TimeSpan.FromMilliseconds(100);
            processingTimer.IsRepeating = true;
            processingTimer.Tick += (_, _) =>
            {
                if (processingStopwatch is null) return;
                ProcessingTime = processingStopwatch.Elapsed.ToString(ProcessingTimeFormat);
            };
            processingTimer.Start();
        }
        void StopProcessingTimer()
        {
            processingTimer?.Stop();
            processingTimer = null;
            if (processingStopwatch != null)
            {
                ProcessingTime = processingStopwatch.Elapsed.ToString(ProcessingTimeFormat);
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

        #region Cancellation
        CancellationTokenSource? simulationCts;
        #endregion

        #region Construction
        public BrownianViewModel() { }
        public async Task InitializeAsync(IDictionary<string, object>? parameters) { }
        #endregion

        #region Heavy threshold
        private const int BasePointsPerCore = 375_000;
        public static int HeavyThresholdPoints => BasePointsPerCore * (Environment.ProcessorCount - 1);
        #endregion

        #region Background windows
        static int backgroundWindowSequence;
        static int NextBackgroundSequence() => Interlocked.Increment(ref backgroundWindowSequence);

        private bool isBackgroundWindow;
        public bool IsBackgroundWindow
        {
            get => isBackgroundWindow;
            set
            {
                if (SetProperty(ref isBackgroundWindow, value))
                {
                    OnPropertyChanged(nameof(MostrarBotaoGerar));
                    RaiseCanExecute();
                }
            }
        }
        public bool MostrarBotaoGerar => !IsBackgroundWindow;
        #endregion

        #region Commands
        public bool CanGerarSimulacao => !IsProcessing && !IsBackgroundWindow && ValidateInputs(false, out _);
        public bool CanCancelar => IsProcessing;

        [RelayCommand(CanExecute = nameof(CanCancelar))]
        void Cancelar()
        {
            simulationCts?.Cancel();
            if (IsBackgroundWindow)
            {
                var win = Application.Current?.Windows.FirstOrDefault(w => w.Page?.BindingContext == this);
                if (win != null) Application.Current?.CloseWindow(win);
            }
        }

        [RelayCommand(CanExecute = nameof(CanGerarSimulacao))]
        public async Task GerarSimulacao()
        {
            try
            {
                if (!ValidateInputs(true, out var validationError))
                {
                    await (Application.Current?.MainPage?.DisplayAlert("Validação", validationError, "OK") ?? Task.CompletedTask);
                    return;
                }
                if (!await ValidateHeavy()) return;
                await RunSimulationAsync();
            }
            catch (Exception ex)
            {
                await (Application.Current?.MainPage?.DisplayAlert("Erro", ex.Message, "OK") ?? Task.CompletedTask);
            }
        }

        // Verifica se a simulação é considerada "pesada" (muitos pontos) e oferece abrir em nova janela para processamento paralelo sem bloquear a UI principal.
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
                vm2.IsBackgroundWindow = true;

                int seq = NextBackgroundSequence();
                string title = $"Execução em Segundo Plano #{seq}";
                newPage.Title = title;
                var window = new Window(newPage) { Title = title };
                Application.Current!.OpenWindow(window);
                _ = vm2.RunSimulationAsync(); // fire & forget protegido internamente
                return false;
            }
            return true;
        }
        #endregion

        #region Downsampling config
        public const int MaxChartPoints = 10_000;
        // Reduz o número de pontos de uma série via amostragem uniforme com interpolação linear para preservar tendência e minimizar aliasing gráfico.
        static double[] Downsample(double[] source, int maxPoints)
        {
            if (source.Length <= maxPoints) return source;
            if (maxPoints < 2) return new[] { source[^1] };
            double[] target = new double[maxPoints];
            int lastIndex = source.Length - 1;
            double scale = lastIndex / (double)(maxPoints - 1);
            target[0] = source[0];
            for (int i = 1; i < maxPoints - 1; i++)
            {
                double pos = i * scale;
                int idx = (int)pos;
                double frac = pos - idx;
                if (idx >= lastIndex)
                {
                    target[i] = source[lastIndex];
                }
                else
                {
                    double v0 = source[idx];
                    double v1 = source[idx + 1];
                    target[i] = v0 + (v1 - v0) * frac;
                }
            }
            target[^1] = source[^1];
            return target;
        }
        #endregion

        #region Simulation execution
        // Orquestra a simulação: prepara cancelamento, valida entradas, executa geração em thread pool, faz downsampling, atualiza Paths e estado de processamento com segurança para a UI.
        async Task RunSimulationAsync()
        {
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
                    if (!IsBackgroundWindow)
                        await (Application.Current?.MainPage?.DisplayAlert("Validação", validationError, "OK") ?? Task.CompletedTask);
                    return;
                }

                double sigmaInput = VolatilidadePercent / 100.0;
                double muInput = RetornoPercent / 100.0;
                double sigmaD = sigmaInput;
                double muD = muInput;
                int sims = Math.Max(1, NumeroSimulacoes);
                int dias = Math.Max(2, TempoDias);

                var result = await Task.Run(() => GeneratePaths(sims, dias, sigmaD, muD, PrecoInicial, token), token);

                if (token.IsCancellationRequested) return;

                // Downsampling antes de atribuir à UI
                if (MaxChartPoints > 0)
                {
                    for (int i = 0; i < result.Count; i++)
                        if (result[i].Length > MaxChartPoints)
                            result[i] = Downsample(result[i], MaxChartPoints);
                }

                await Task.Yield();

                if (!token.IsCancellationRequested)
                    Paths = result;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsBackgroundWindow)
                    await (Application.Current?.MainPage?.DisplayAlert("Erro", ex.Message, "OK") ?? Task.CompletedTask);
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

        // Gera múltiplos caminhos: escolhe execução sequencial para lotes pequenos e paraleliza (Parallel.For) para grandes volumes, reutilizando placeholders para evitar realloc.
        static List<double[]> GeneratePaths(int sims, int dias, double sigmaD, double muD, double precoInicial, CancellationToken token)
        {
            long totalPoints = (long)sims * dias;
            if (totalPoints < 50_000 || sims == 1)
            {
                var listSeq = new List<double[]>(sims);
                for (int k = 0; k < sims; k++)
                {
                    token.ThrowIfCancellationRequested();
                    listSeq.Add(GenerateBrownianMotion(sigmaD, muD, precoInicial, dias, token));
                }
                return listSeq;
            }

            var list = new List<double[]>(sims);
            for (int i = 0; i < sims; i++) list.Add(Array.Empty<double>());

            int maxParallel = Math.Max(1, Environment.ProcessorCount - 1);
            Parallel.For(0, sims, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = maxParallel }, i =>
            {
                list[i] = GenerateBrownianMotion(sigmaD, muD, precoInicial, dias, token);
            });
            return list;
        }
        #endregion

        #region Validation
        // Faz parsing e validação dos campos de entrada (strings) convertendo para propriedades numéricas apenas quando solicitado (canShowError = true) para evitar travar digitação.
        bool ValidateInputs(bool canShowError, out string? error)
        {
            error = null;
            var culture = CultureInfo.CurrentCulture;

            bool ParseDouble(string? txt, string field, bool mustBePositive, bool allowNegative, out double value, out string? err)
            {
                value = 0; err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!double.TryParse(txt, NumberStyles.Float, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
                if (!allowNegative && value < 0) { err = $"Campo '{field}' não pode ser negativo"; return false; }
                if (mustBePositive && value <= 0) { err = $"Campo '{field}' deve ser maior que 0"; return false; }
                return true;
            }
            bool ParseInt(string? txt, string field, int min, out int value, out string? err)
            {
                value = 0; err = null;
                if (string.IsNullOrWhiteSpace(txt)) { err = $"Campo '{field}' está vazio"; return false; }
                if (!int.TryParse(txt, NumberStyles.Integer, culture, out value)) { err = $"Campo '{field}' inválido"; return false; }
                if (value < 0) { err = $"Campo '{field}' não pode ser negativo"; return false; }
                if (value < min) { err = $"Campo '{field}' deve ser >= {min}"; return false; }
                return true;
            }

            if (!ParseDouble(PrecoInicialInput, "Preço inicial", mustBePositive: true, allowNegative: false, out var preco, out var e1)) { if (canShowError) error = e1; return false; }
            if (!ParseDouble(VolatilidadePercentInput, "Volatilidade", mustBePositive: false, allowNegative: false, out var vol, out var e2)) { if (canShowError) error = e2; return false; }
            // Retorno pode ser negativo (ex: drift negativo)
            if (!ParseDouble(RetornoPercentInput, "Retorno", mustBePositive: false, allowNegative: true, out var ret, out var e3)) { if (canShowError) error = e3; return false; }
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

        static readonly ThreadLocal<Random> s_random = new(() => new Random(Random.Shared.Next()));
        // Gera um caminho de Movimento Browniano Geométrico usando Box-Muller para normal padrão e evolução multiplicativa (preço >= PriceFloor).
        // TODO Talvez Marsaglia seja mais performatico
        public static double[] GenerateBrownianMotion(double sigma, double mean, double initialPrice, int numDays, CancellationToken token)
        {
            var rand = s_random.Value!;
            double[] prices = new double[numDays];
            prices[0] = Math.Max(initialPrice, PriceFloor);
            for (int i = 1; i < numDays; i++)
            {
                token.ThrowIfCancellationRequested();
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double retornoDiario = mean + sigma * z;
                double next = prices[i - 1] * Math.Exp(retornoDiario);
                if (next < PriceFloor) next = PriceFloor;
                prices[i] = next;
            }
            return prices;
        }
        #endregion

        #region Helpers
        // Força atualização de estados de CanExecute e notifica a UI após mudanças que afetam comandos.
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