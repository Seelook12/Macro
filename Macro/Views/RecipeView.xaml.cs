using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Windows;
using System.Windows.Controls;
using Macro.ViewModels;
using ReactiveUI;

namespace Macro.Views
{
    public partial class RecipeView : UserControl, IViewFor<RecipeViewModel>
    {
        // ... (기존 Dependency Properties) ...
        #region Dependency Properties

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(RecipeViewModel), typeof(RecipeView), new PropertyMetadata(null));

        #endregion

        public RecipeView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext).DisposeWith(disposables);

                // 이름 입력 팝업 핸들러
                ViewModel!.ShowInputName.RegisterHandler(ctx =>
                {
                    var inputWindow = new InputWindow();
                    if (inputWindow.ShowDialog() == true)
                    {
                        ctx.SetOutput(inputWindow.InputText);
                    }
                    else
                    {
                        ctx.SetOutput(null);
                    }
                }).DisposeWith(disposables);

                // [New] 변경 확인 팝업 핸들러
                ViewModel!.ConfirmChange.RegisterHandler(ctx =>
                {
                    var result = MessageBox.Show(ctx.Input, "레시피 변경", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    ctx.SetOutput(result == MessageBoxResult.Yes);
                }).DisposeWith(disposables);
            });
        }
        
        // ... (IViewFor 구현) ...
        #region IViewFor Implementation

        public RecipeViewModel? ViewModel
        {
            get => (RecipeViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (RecipeViewModel?)value;
        }

        #endregion
    }
}
