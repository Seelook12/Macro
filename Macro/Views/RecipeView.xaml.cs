using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Macro.ViewModels;
using ReactiveUI;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views
{
    public partial class RecipeView : UserControl, IViewFor<RecipeViewModel>
    {
        #region Dependency Properties

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(RecipeViewModel), typeof(RecipeView), new PropertyMetadata(null));

        #endregion

        public RecipeView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                var d1 = this.WhenAnyValue(x => x.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => DataContext = vm);
                disposables.Add(d1);

                if (ViewModel != null)
                {
                    // 이름 입력 팝업 핸들러
                    var d2 = ViewModel.ShowInputName.RegisterHandler(ctx =>
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
                    });
                    disposables.Add(d2);

                    // [New] 변경 확인 팝업 핸들러
                    var d3 = ViewModel.ConfirmChange.RegisterHandler(ctx =>
                    {
                        var result = System.Windows.MessageBox.Show(ctx.Input, "레시피 변경", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        ctx.SetOutput(result == MessageBoxResult.Yes);
                    });
                    disposables.Add(d3);
                }
            });
        }
        
        #region IViewFor Implementation

        public RecipeViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty) as RecipeViewModel;
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as RecipeViewModel;
        }

        #endregion
    }
}