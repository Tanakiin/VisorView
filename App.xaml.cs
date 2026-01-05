using System.Windows;

namespace VisorView
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            DispatcherUnhandledException += (_, e) =>
            {
                System.Windows.MessageBox.Show(e.Exception.ToString(), "Startup crash");
                e.Handled = true;
            };
        }

    }
}
