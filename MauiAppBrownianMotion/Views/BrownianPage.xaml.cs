using MauiAppBrownianMotion.Models;
using MauiAppBrownianMotion.Views.Base;

namespace MauiAppBrownianMotion.Pages
{
    public partial class BrownianPage : ContentPage, IMauiView
    {
        /// <summary>
        /// Drawable responsável por desenhar as trajetórias no componente de gráfico.
        /// Sua fonte de dados (<see cref="GbmDrawable.GetPaths"/>) é atribuída dinamicamente a partir do ViewModel.
        /// </summary>
        private readonly GbmDrawable drawable = new();

        /// <summary>
        /// Largura mínima (em pixels) para alternar o VisualState para "Wide".
        /// Abaixo desse valor o estado "Narrow" é aplicado.
        /// </summary>
        private const double WideThreshold = 1100; // largura mínima para painel lateral

        /// <summary>
        /// Ctor Inicializa componentes, injeta o ViewModel, associa o drawable  ao controle de gráfico e registra callback para solicitar redesenho.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Lançada caso o ViewModel não seja resolvido corretamente pela injeção.
        /// </exception>
        public BrownianPage()
        {
            InitializeComponent();
            this.InjectViewModel();

            var vm = (BindingContext as BrownianViewModel) ?? throw new InvalidOperationException("ViewModel não pode ser nulo");

            // Conecta a função de obtenção de paths do ViewModel ao drawable
            drawable.GetPaths = () => vm.Paths;

            // Define o drawable no componente gráfico (ex: GraphicsView)
            Chart.Drawable = drawable;

            // Solicita invalidação/redesenho quando o ViewModel indicar
            vm.RedrawRequested += () => Chart.Invalidate();

            // Adapta layout responsivo 
            SizeChanged += BrownianPage_SizeChanged;
        }

        /// <summary>
        /// Manipula o evento de alteração de tamanho da página para alternar entre estados visuais responsivos.
        /// </summary>
        /// <param name="sender">Origem do evento.</param>
        /// <param name="e">Argumentos de evento.</param>
        private void BrownianPage_SizeChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            var state = Width >= WideThreshold ? "Wide" : "Narrow";
            VisualStateManager.GoToState(LayoutGrid, state);
#endif
        }
    }
}
