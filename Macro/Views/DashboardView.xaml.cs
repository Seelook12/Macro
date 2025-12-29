using ReactiveUI;
using Macro.ViewModels;
using System.Windows.Controls;

namespace Macro.Views
{
    public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
    {
        public DashboardView()
        {
            InitializeComponent();
            // ViewModel 바인딩은 필요 시 이곳에서 this.WhenActivated 등을 통해 처리
        }
    }
}
