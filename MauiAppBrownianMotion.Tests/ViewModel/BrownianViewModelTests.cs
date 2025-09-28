using MauiAppBrownianMotion.ViewModels;
using System.Reflection;
using Xunit;

namespace MauiAppBrownianMotion.Tests.ViewModel
{
    public class BrownianViewModelTests
    {
        // Verifica que entradas válidas habilitam a geração de simulação.
        [Fact]
        public void CanGerarSimulacao_ReturnsTrue_ForValidInputs()
        {
            // Arrange
            var vm = new BrownianViewModel
            {
                PrecoInicialInput = "100",
                VolatilidadePercentInput = "20",
                RetornoPercentInput = "1",
                TempoDiasInput = "252",
                NumeroSimulacoesInput = "10"
            };

            // Act
            bool canGenerate = vm.CanGerarSimulacao;

            // Assert
            Assert.True(canGenerate);
        }

        // Garante que preço inicial negativo impede geração da simulação.
        [Fact]
        public void CanGerarSimulacao_ReturnsFalse_WhenPrecoInicialNegativo()
        {
            // Arrange
            var vm = new BrownianViewModel
            {
                PrecoInicialInput = "-1",
                VolatilidadePercentInput = "20",
                RetornoPercentInput = "1",
                TempoDiasInput = "252",
                NumeroSimulacoesInput = "10"
            };

            // Act
            bool canGenerate = vm.CanGerarSimulacao;

            // Assert
            Assert.False(canGenerate);
        }

        // Valida que não pode gerar se estiver processando ou em janela de segundo plano.
        [Fact]
        public void CanGerarSimulacao_RetornaFalso_QuandoProcessandoOuEmSegundoPlano()
        {
            var vm = new BrownianViewModel
            {
                PrecoInicialInput = "100",
                VolatilidadePercentInput = "20",
                RetornoPercentInput = "1",
                TempoDiasInput = "252",
                NumeroSimulacoesInput = "10"
            };

            // IsProcessing cancela geração
            vm.IsProcessing = true;
            Assert.False(vm.CanGerarSimulacao);

            // Reset e valida janela em segundo plano
            vm.IsProcessing = false;
            vm.IsBackgroundWindow = true;
            Assert.False(vm.CanGerarSimulacao);
        }

        // Confirma série constante quando sigma e retorno são zero (teste curto).
        [Fact]
        public void GenerateBrownianMotion_MantemPrecoConstante_QuandoSemRetornoENemVolatilidade()
        {
            double precoInicial = 150;
            double[] serie = BrownianViewModel.GenerateBrownianMotion(0, 0, precoInicial, 10, CancellationToken.None);

            Assert.Equal(10, serie.Length);
            Assert.All(serie, valor => Assert.Equal(precoInicial, valor, precision: 5));
        }

        // Garante lançamento de OperationCanceledException quando token cancelado.
        [Fact]
        public void GenerateBrownianMotion_LancaException_QuandoCancelado()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                BrownianViewModel.GenerateBrownianMotion(0.2, 0.1, 100, 5, cts.Token));
        }

        // Verifica que validação sem commit não altera propriedades numéricas internas.
        [Fact]
        public void ValidateInputs_NaoAtualizaNumericos_QuandoCanShowErrorFalse()
        {
            var vm = new BrownianViewModel
            {
                PrecoInicialInput = "123.45",
                VolatilidadePercentInput = "5",
                RetornoPercentInput = "2",
                TempoDiasInput = "20",
                NumeroSimulacoesInput = "3"
            };

            // Captura valores numéricos iniciais
            double precoAntes = vm.PrecoInicial;

            // Força entrada inválida mas sem commit (canShowError=false)
            vm.PrecoInicialInput = "-10";
            var metodo = typeof(BrownianViewModel)
                .GetMethod("ValidateInputs", BindingFlags.Instance | BindingFlags.NonPublic);

            object?[] parametros = { false, null };
            bool ok = (bool)metodo!.Invoke(vm, parametros)!;
            Assert.False(ok);

            // Valor numérico interno não deve ter sido alterado
            Assert.Equal(precoAntes, vm.PrecoInicial);
        }

        // Garante que a simulação gera apenas valores positivos.
        [Fact]
        public void GenerateBrownianMotion_DeveGerarValoresPositivos()
        {
            var serie = BrownianViewModel.GenerateBrownianMotion(0.2, 0.05, 100, 50, CancellationToken.None);
            Assert.Equal(50, serie.Length);
            Assert.All(serie, v => Assert.True(v > 0));
        }

        // Confirma série constante quando sigma e mean são zero (variação de cenário).
        [Fact]
        public void GenerateBrownianMotion_Constante_QuandoSigmaEMeanZero()
        {
            var serie = BrownianViewModel.GenerateBrownianMotion(0, 0, 150, 20, CancellationToken.None);
            Assert.All(serie, v => Assert.Equal(150, v, 5));
        }

        // Verifica novamente cancelamento com parâmetros diferentes.
        [Fact]
        public void GenerateBrownianMotion_Lanca_OperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                BrownianViewModel.GenerateBrownianMotion(0.1, 0.02, 100, 10, cts.Token));
        }

        // Confirma que o limite dinâmico depende da contagem de processadores.
        [Fact]
        public void Downsample_ReduzMantendoExtremos()
        {
            // Acesso via reflection (método privado)
            var mi = typeof(BrownianViewModel)
                .GetMethod("Downsample", BindingFlags.Static | BindingFlags.NonPublic);
            double[] source = Enumerable.Range(0, 10_000).Select(i => (double)i).ToArray();

            var reduzido = (double[])mi!.Invoke(null, new object[] { source, 100 })!;

            Assert.Equal(100, reduzido.Length);
            Assert.Equal(source.First(), reduzido.First());
            Assert.Equal(source.Last(), reduzido.Last());
        }

        // Confirma que o limite de carga pesada é calculado conforme os núcleos.
        [Fact]
        public void HeavyThresholdPoints_DependeDeProcessadores()
        {
            int expected = (Environment.ProcessorCount - 1) * 375_000;
            Assert.Equal(expected, BrownianViewModel.HeavyThresholdPoints);
        }

        // Aceita retorno negativo nas simulações.
        [Fact]
        public void CanGerarSimulacao_AceitaRetornoNegativo()
        {
            var vm = new BrownianViewModel
            {
                PrecoInicialInput = "100",
                VolatilidadePercentInput = "25",
                RetornoPercentInput = "-3", // negativo
                TempoDiasInput = "100",
                NumeroSimulacoesInput = "5"
            };

            Assert.True(vm.CanGerarSimulacao);
        }

        // Garante que simulação com retorno negativo tende a decair.
        [Fact]
        public void GenerateBrownianMotion_ComRetornoNegativo_TendeADecair()
        {
            // Drift negativo pronunciado para aumentar chance de queda média
            double[] serie = BrownianViewModel.GenerateBrownianMotion(0.05, -0.20, 100, 500, CancellationToken.None);
            Assert.Equal(500, serie.Length);
            // Média dos últimos 50 pontos deve ser menor que média dos 50 iniciais (tendência de queda)
            double mediaInicio = serie.Take(50).Average();
            double mediaFim = serie.Skip(serie.Length - 50).Average();
            Assert.True(mediaFim < mediaInicio);
        }
    }
}
