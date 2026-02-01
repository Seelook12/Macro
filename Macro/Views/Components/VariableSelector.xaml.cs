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

        private bool _isItemsSourceChanging = false;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VariableSelector)d;
            control._isItemsSourceChanging = true;

            // ItemsSource 변경으로 인한 콤보박스 초기화가 완료될 때까지 플래그 유지
            control.Dispatcher.BeginInvoke(new Action(() =>
            {
                control._isItemsSourceChanging = false;
            }), DispatcherPriority.Input);
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

            string currentComboText = Part_ComboBox.Text;
            string viewModelText = Text;

            // ItemsSource 변경으로 인해 텍스트가 날아간 경우에만 복구
            if (_isItemsSourceChanging && string.IsNullOrEmpty(currentComboText) && !string.IsNullOrEmpty(viewModelText))
            {
                _isInternalChange = true;
                Part_ComboBox.Text = viewModelText; // 원래 값으로 되돌림
                _isInternalChange = false;
                return;
            }

            // 그 외의 경우 (사용자가 입력, 삭제 등)
            Text = currentComboText;
        }
    }
}