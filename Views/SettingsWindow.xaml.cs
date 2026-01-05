using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace VisorView
{
    public partial class SettingsWindow : Window
    {
        public AppState Result { get; private set; }

        public SettingsWindow(AppState current)
        {
            InitializeComponent();
            Result = current;

            EngineCombo.SelectedIndex = (int)current.DefaultSearchEngine;
            AlwaysOnTopCheck.IsChecked = current.AlwaysOnTop;
            BlockAdsCheck.IsChecked = current.BlockAds;
            StartEditModeCheck.IsChecked = current.StartInEditMode;
            ClickThroughCheck.IsChecked = current.ClickThroughInNormalMode;
            RememberLayoutCheck.IsChecked = current.RememberLayout;

            CloseButton.Click += (_, __) => { DialogResult = false; Close(); };
            CancelButton.Click += (_, __) => { DialogResult = false; Close(); };

            SaveButton.Click += (_, __) =>
            {
                current.DefaultSearchEngine = (SearchEngine)EngineCombo.SelectedIndex;
                current.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
                current.BlockAds = BlockAdsCheck.IsChecked == true;
                current.StartInEditMode = StartEditModeCheck.IsChecked == true;
                current.ClickThroughInNormalMode = ClickThroughCheck.IsChecked == true;
                current.RememberLayout = RememberLayoutCheck.IsChecked == true;

                Result = current;
                DialogResult = true;
                Close();
            };
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }

        void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src is System.Windows.Controls.Button ||
                    src is System.Windows.Controls.ComboBox ||
                    src is System.Windows.Controls.TextBox ||
                    src is System.Windows.Controls.Primitives.ToggleButton)
                    return;

                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }

            try { DragMove(); } catch { }
        }
    }
}
