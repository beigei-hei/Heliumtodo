using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace Heliumtodo.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }



        private void 摄氏度_TextChanged(object? sender, TextChangedEventArgs e)
        {



            {
                if (double.TryParse(摄氏度.Text, out double C))
                {
                    var F = C * (9d / 5d) + 32;
                    华氏度.Text = F.ToString("0.0");
                }

                else
                {
                    摄氏度.Text = "0";
                    华氏度.Text = "0";
                }
            }
            Debug.WriteLine($"Click! Celsius={摄氏度.Text}");
        }

        private void TextBlock_ContextCanceled(object? sender, RoutedEventArgs e)
        {
        }
    }

}