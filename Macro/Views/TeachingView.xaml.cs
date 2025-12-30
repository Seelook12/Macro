using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using Macro.ViewModels;

namespace Macro.Views
{
    /// <summary>
    /// TeachingView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TeachingView : UserControl, IViewFor<TeachingViewModel>
    {
        #region Dependency Properties

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(TeachingViewModel), typeof(TeachingView), new PropertyMetadata(null));

        #endregion

        #region Constructors

        public TeachingView()
        {
            InitializeComponent();

            // ViewModel 바인딩이 변경되면 DataContext도 업데이트하여 XAML 바인딩 지원
            this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);

            // [Fix] ViewModel의 WhenActivated가 실행되도록 View의 Activation을 트리거함
            this.WhenActivated(disposables => 
            {
                // View가 활성화될 때 처리할 로직이 있다면 여기에 작성
                // ViewModel의 WhenActivated는 이 호출이 있어야만 정상 작동함
            });
        }

        #endregion

        #region IViewFor Implementation

        public TeachingViewModel? ViewModel
        {
            get => (TeachingViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TeachingViewModel?)value;
        }

        #endregion
    }
}