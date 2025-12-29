using System.Reactive.Disposables;
using Macro.ViewModels;
using ReactiveUI;

namespace Macro.Views
{
    // XAML 파서 오류 방지를 위한 중간 클래스
    public class RecipeViewBase : ReactiveUserControl<RecipeViewModel> { }

    public partial class RecipeView : RecipeViewBase
    {
        public RecipeView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                                // Interaction 핸들러 등록
                                disposables(ViewModel!.ShowInputName.RegisterHandler(interaction =>
                                {
                                    var inputWindow = new InputWindow();
                                    inputWindow.Owner = System.Windows.Window.GetWindow(this);
                                    
                                    if (inputWindow.ShowDialog() == true)
                                    {
                                        interaction.SetOutput(inputWindow.InputText);
                                    }
                                    else
                                    {
                                        interaction.SetOutput(null);
                                    }
                                }));            });
        }
    }
}
