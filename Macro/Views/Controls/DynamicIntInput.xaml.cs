using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Macro.Models;

namespace Macro.Views.Controls
{
    public partial class DynamicIntInput : System.Windows.Controls.UserControl
    {
        public DynamicIntInput()
        {
            InitializeComponent();
        }

        #region Value (int)

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(DynamicIntInput),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        #endregion

        #region VariableName (string)

        public static readonly DependencyProperty VariableNameProperty =
            DependencyProperty.Register(nameof(VariableName), typeof(string), typeof(DynamicIntInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string VariableName
        {
            get => (string)GetValue(VariableNameProperty);
            set => SetValue(VariableNameProperty, value);
        }

        #endregion

        #region SourceType (ValueSourceType)

        public static readonly DependencyProperty SourceTypeProperty =
            DependencyProperty.Register(nameof(SourceType), typeof(ValueSourceType), typeof(DynamicIntInput),
                new FrameworkPropertyMetadata(ValueSourceType.Constant, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ValueSourceType SourceType
        {
            get => (ValueSourceType)GetValue(SourceTypeProperty);
            set => SetValue(SourceTypeProperty, value);
        }

        #endregion

        #region VariableSource (IEnumerable)

        public static readonly DependencyProperty VariableSourceProperty =
            DependencyProperty.Register(nameof(VariableSource), typeof(IEnumerable), typeof(DynamicIntInput),
                new PropertyMetadata(null));

        public IEnumerable VariableSource
        {
            get => (IEnumerable)GetValue(VariableSourceProperty);
            set => SetValue(VariableSourceProperty, value);
        }

        #endregion

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (SourceType == ValueSourceType.Constant)
            {
                SourceType = ValueSourceType.Variable;
            }
            else
            {
                SourceType = ValueSourceType.Constant;
            }
        }
    }
}
