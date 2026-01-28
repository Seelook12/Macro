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
            var control = (VariableSelector)d;

            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += control.OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 목록이 변경될 때 Text가 날아가는 것을 방지하기 위해
            // 현재 Text 값을 다시 한 번 강제 설정하거나 유지 확인
            if (!_isInternalChange && !string.IsNullOrEmpty(Text))
            {
                // UI 스레드 타이밍 이슈 방지를 위해 Dispatcher 활용
                Dispatcher.InvokeAsync(() =>
                {
                    if (!string.IsNullOrEmpty(Text) && Part_ComboBox.Text != Text)
                    {
                        Part_ComboBox.Text = Text;
                    }
                }, DispatcherPriority.DataBind);
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 외부(ViewModel)에서 Text가 바뀌면 ComboBox에 반영
            // (바인딩으로 자동 처리되지만 명시적 로직이 필요할 경우 추가)
        }
    }
}