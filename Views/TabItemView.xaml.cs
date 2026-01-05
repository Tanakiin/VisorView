using System;
using System.Windows;
using System.Windows.Media;

namespace VisorView
{
    public partial class TabItemView : System.Windows.Controls.UserControl
    {
        public static readonly RoutedEvent CloseRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "CloseRequested",
                RoutingStrategy.Bubble,
                typeof(EventHandler<TabCloseRequestedEventArgs>),
                typeof(TabItemView));

        public event EventHandler<TabCloseRequestedEventArgs> CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

        public TabItemView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not TabState ts) return;
            ColorChip.Background = new SolidColorBrush(GenerateColor(ts.Id));
        }

        void Close_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TabState ts) return;
            RaiseEvent(new TabCloseRequestedEventArgs(CloseRequestedEvent, ts));
        }

        static System.Windows.Media.Color GenerateColor(string id)
        {
            int hash = 17;
            for (int i = 0; i < (id?.Length ?? 0); i++)
                hash = hash * 31 + id[i];

            byte r = (byte)((hash >> 16) & 0xFF);
            byte g = (byte)((hash >> 8) & 0xFF);
            byte b = (byte)(hash & 0xFF);

            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
    }

    public sealed class TabCloseRequestedEventArgs : RoutedEventArgs
    {
        public TabState Tab { get; }

        public TabCloseRequestedEventArgs(RoutedEvent routedEvent, TabState tab) : base(routedEvent)
        {
            Tab = tab;
        }
    }
}