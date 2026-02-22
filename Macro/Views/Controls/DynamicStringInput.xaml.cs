using System.Collections;
using System.Windows;
using Macro.Models;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views.Controls
{
    public partial class DynamicStringInput : UserControl
    {
        public DynamicStringInput()
        {
            InitializeComponent();
        }

        #region Value (string)

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(DynamicStringInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        #endregion

        #region VariableName (string)

        public static readonly DependencyProperty VariableNameProperty =
            DependencyProperty.Register(nameof(VariableName), typeof(string), typeof(DynamicStringInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string VariableName
        {
            get => (string)GetValue(VariableNameProperty);
            set => SetValue(VariableNameProperty, value);
        }

        #endregion

        #region SourceType (ValueSourceType)

        public static readonly DependencyProperty SourceTypeProperty =
            DependencyProperty.Register(nameof(SourceType), typeof(ValueSourceType), typeof(DynamicStringInput),
                new FrameworkPropertyMetadata(ValueSourceType.Constant, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ValueSourceType SourceType
        {
            get => (ValueSourceType)GetValue(SourceTypeProperty);
            set => SetValue(SourceTypeProperty, value);
        }

        #endregion

        #region VariableSource (IEnumerable)

        public static readonly DependencyProperty VariableSourceProperty =
            DependencyProperty.Register(nameof(VariableSource), typeof(IEnumerable), typeof(DynamicStringInput),
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
