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

        public DashboardView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                // ViewModel과 DataContext를 명시적으로 바인딩
                this.WhenAnyValue(x => x.ViewModel)
                    .BindTo(this, x => x.DataContext)
                    .DisposeWith(disposables);

                if (ViewModel != null)
                {
                    // 로그 컬렉션 변경 감지하여 자동 스크롤
                    Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                        h => ViewModel.Logs.CollectionChanged += h,
                        h => ViewModel.Logs.CollectionChanged -= h)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(_ =>
                        {
                            if (ViewModel.Logs.Count > 0)
                            {
                                LogListBox.ScrollIntoView(ViewModel.Logs[ViewModel.Logs.Count - 1]);
                            }
                        })
                        .DisposeWith(disposables);
                }
            });
        }

        #region IViewFor Implementation

        public DashboardViewModel? ViewModel
        {
            get => (DashboardViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DashboardViewModel?)value;
        }

        #endregion
    }
}
