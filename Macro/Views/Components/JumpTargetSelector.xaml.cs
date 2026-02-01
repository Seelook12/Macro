using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Macro.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views.Components
{
    public partial class JumpTargetSelector : UserControl
    {
        private bool _isInternalChange = false;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(JumpTargetSelector),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedIdProperty =
            DependencyProperty.Register(nameof(SelectedId), typeof(string), typeof(JumpTargetSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIdChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string SelectedId
        {
            get => (string)GetValue(SelectedIdProperty);
            set => SetValue(SelectedIdProperty, value);
        }

        public JumpTargetSelector()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (JumpTargetSelector)d;
            
            // Unsubscribe from old collection
            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnCollectionChanged;
            }

            // Subscribe to new collection
            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += control.OnCollectionChanged;
            }

            // [Fix] Defer synchronization to ensure ComboBox.ItemsSource binding is updated
            control.Dispatcher.InvokeAsync(() => control.SynchronizeSelection());
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // If the collection is cleared and refilled, we want to maintain the selection
            SynchronizeSelection();
        }

        private static void OnSelectedIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (JumpTargetSelector)d;
            if (control._isInternalChange) return;

            control.SynchronizeSelection();
        }

        private void Part_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalChange) return;

            // [Protection]
            // ItemsSource 변경 시 ComboBox는 자동으로 선택을 해제하여 SelectedValue를 null로 만듭니다.
            // 하지만 우리 로직상 사용자가 명시적으로 '선택 해제'를 하는 경우는 없습니다.
            // (항상 목록 중 하나를 선택해야 함)
            // 따라서 null이 들어오는 경우는 100% 시스템에 의한 초기화이므로,
            // 이를 ViewModel(SelectedId)에 전파하지 않고 무시하여 데이터를 보호합니다.

            if (Part_ComboBox.SelectedValue == null) return;

            _isInternalChange = true;
            try
            {
                var newValue = Part_ComboBox.SelectedValue as string;
                if (SelectedId != newValue)
                {
                    SelectedId = newValue;
                }
            }
            finally
            {
                _isInternalChange = false;
            }
        }

        private void SynchronizeSelection()
        {
            if (_isInternalChange) return;

            _isInternalChange = true;
            try
            {
                // Ensure the ComboBox reflects the SelectedId if it exists in the list
                if (ItemsSource != null)
                {
                    var items = ItemsSource.Cast<JumpTargetViewModel>().ToList();
                    var exists = items.Any(x => x.Id == SelectedId);
                    
                    if (exists)
                    {
                        Part_ComboBox.SelectedValue = SelectedId;
                    }
                    else if (string.IsNullOrEmpty(SelectedId))
                    {
                        Part_ComboBox.SelectedIndex = -1;
                    }
                    // If it doesn't exist, we DON'T null out SelectedId yet, 
                    // because the list might be still loading/filtering.
                    // This is the core of the protection logic.
                }
            }
            catch
            {
                // Cast might fail if items are not JumpTargetViewModel
            }
            finally
            {
                _isInternalChange = false;
            }
        }
    }
}
