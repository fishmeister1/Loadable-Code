using System.Windows;

namespace Codeful
{
    public partial class CustomConfirmationDialog : Window
    {
        public bool Result { get; private set; } = false;

        public CustomConfirmationDialog()
        {
            InitializeComponent();
        }

        public CustomConfirmationDialog(string title, string message, string detail = "") : this()
        {
            TitleText.Text = title;
            MessageText.Text = message;
            
            if (!string.IsNullOrEmpty(detail))
            {
                DetailText.Text = detail;
                DetailText.Visibility = Visibility.Visible;
            }
            else
            {
                DetailText.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        public static bool ShowDialog(Window owner, string title, string message, string detail = "")
        {
            var dialog = new CustomConfirmationDialog(title, message, detail)
            {
                Owner = owner
            };
            
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}