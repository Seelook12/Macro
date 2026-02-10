using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Macro.ViewModels;

namespace Macro.Views
{
    public partial class VariableManagerView : ReactiveUserControl<VariableManagerViewModel>
    {
        public VariableManagerView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                // ViewModel 바인딩 (누락된 핵심 로직 추가)
                var d = this.WhenAnyValue(x => x.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => DataContext = vm);
                disposables.Add(d);
            });
        }
    }
}