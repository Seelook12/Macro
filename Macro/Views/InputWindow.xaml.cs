using System.Windows;

namespace Macro.Views
{
    public partial class InputWindow : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public InputWindow()
        {
            InitializeComponent();
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
