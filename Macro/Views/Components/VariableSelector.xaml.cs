using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views.Components
{
    public partial class VariableSelector : UserControl
    {
        private bool _isInternalChange = false;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(VariableSelector), 
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(VariableSelector), 
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(VariableSelector), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SelectedValuePathProperty =
            DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(VariableSelector), new PropertyMetadata(string.Empty));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public VariableSelector()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 목록 변경 감지 로직 불필요 (바인딩이 끊어졌으므로 영향 없음)
            // 다만, 목록이 로드된 후 현재 Text가 목록에 있다면 선택 상태를 맞춰줄 필요는 있음.
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // [ViewModel -> View]
            var control = (VariableSelector)d;
            var newText = (string)e.NewValue;

            if (control.Part_ComboBox.Text != newText)
            {
                control._isInternalChange = true;
                control.Part_ComboBox.Text = newText ?? string.Empty;
                control._isInternalChange = false;
            }
        }

        private void Part_ComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // [View -> ViewModel]
            if (_isInternalChange) return;

            // 사용자가 직접 입력한 경우에만 ViewModel 업데이트
            // ComboBox가 ItemsSource 변경 등으로 인해 자동으로 텍스트를 변경하는 경우,
            // 보통 Focus가 없거나 드롭다운이 닫혀있는 상태일 수 있음.
            // 하지만 여기서는 간단히 '현재 값과 다르면 업데이트' 하되,
            // 빈 문자열로의 변경이 '시스템에 의한 것'인지 '사용자에 의한 것'인지 구분하기 어려움.
            
            // 핵심 방어 로직:
            // ItemsSource가 변경되는 순간에 WPF ComboBox는 Text를 ""로 밀어버림.
            // 이때 우리는 이 변경을 ViewModel로 전파하지 말아야 함.
            
            // 어떻게 구분하나? 
            // 1. 단순하게: 텍스트가 비어있지 않다면 무조건 반영.
            // 2. 텍스트가 비어있다면? 사용자가 지운 건지 시스템이 지운 건지 모름.
            
            // 전략: 이 이벤트는 TextChanged다. 사용자가 키보드로 칠 때도 발생하고 시스템이 바꿀 때도 발생함.
            // 하지만 우리는 XAML에서 바인딩을 끊었다. 즉, ItemsSource가 바뀐다고 해서 Text 속성이 자동으로 바뀌진 않음.
            // *그러나* ComboBox 내부적으로 ItemsSource가 바뀌면 자기 자신의 Text 프로퍼티를 초기화할 수 있음.
            // 그리고 그게 이 이벤트를 트리거함.
            
            // 만약 들어온 값이 빈 문자열인데, ViewModel의 값(Text)이 비어있지 않다면?
            // -> 시스템이 지운 것으로 간주하고 무시 + 복구!
            
            string currentComboText = Part_ComboBox.Text;
            string viewModelText = Text;

            if (string.IsNullOrEmpty(currentComboText) && !string.IsNullOrEmpty(viewModelText))
            {
                // 시스템에 의한 삭제 감지! -> 복구 시도
                _isInternalChange = true;
                Part_ComboBox.Text = viewModelText; // 원래 값으로 되돌림
                _isInternalChange = false;
                
                // ViewModel에는 알리지 않음 (데이터 보존)
                return;
            }

            // 그 외의 경우 (사용자가 입력, 혹은 정상적인 변경)
            Text = currentComboText;
        }
    }
}