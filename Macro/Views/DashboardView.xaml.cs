using ReactiveUI;
using Macro.ViewModels;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Collections.Specialized;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Reactive.Disposables.Fluent;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views
{
    public partial class DashboardView : UserControl, IViewFor<DashboardViewModel>
    {
        #region Dependency Properties

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(DashboardViewModel), typeof(DashboardView), new PropertyMetadata(null));

        #endregion

        #region IViewFor Implementation

        public DashboardViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty) as DashboardViewModel;
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as DashboardViewModel;
        }

        #endregion

        public DashboardView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                // ViewModel이 바뀔 때마다 DataContext를 안전하게 업데이트
                this.WhenAnyValue(x => x.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => DataContext = vm)
                    .DisposeWith(disposables);

                // 로그 자동 스크롤 로직을 ViewModel 변경에 대응하도록 개선
                this.WhenAnyValue(x => x.ViewModel)
                    .WhereNotNull()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm =>
                    {
                        Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                            h => vm.Logs.CollectionChanged += h,
                            h => vm.Logs.CollectionChanged -= h)
                            .ObserveOn(RxApp.MainThreadScheduler)
                            .Subscribe(_ =>
                            {
                                if (vm.Logs.Count > 0)
                                {
                                    LogListBox.ScrollIntoView(vm.Logs[vm.Logs.Count - 1]);
                                }
                            })
                            .DisposeWith(disposables);
                    })
                    .DisposeWith(disposables);
            });
        }
    }
}
