using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VisorView
{
    public sealed class TabColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var id = value as string ?? "";

            int hash = 17;
            for (int i = 0; i < id.Length; i++)
                hash = hash * 31 + id[i];

            var hue = (hash & 0x7FFFFFFF) % 360;

            return HsvToMediaColor(hue, 0.55, 0.85);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        static System.Drawing.Color HsvToMediaColor(double h, double s, double v)
        {
            double c = v * s;
            double hh = h / 60.0;
            double x = c * (1 - Math.Abs(hh % 2 - 1));
            double m = v - c;

            double r1 = 0, g1 = 0, b1 = 0;

            if (hh < 1) { r1 = c; g1 = x; }
            else if (hh < 2) { r1 = x; g1 = c; }
            else if (hh < 3) { g1 = c; b1 = x; }
            else if (hh < 4) { g1 = x; b1 = c; }
            else if (hh < 5) { r1 = x; b1 = c; }
            else { r1 = c; b1 = x; }

            return System.Drawing.Color.FromArgb(
                255,
                (byte)Math.Round((r1 + m) * 255),
                (byte)Math.Round((g1 + m) * 255),
                (byte)Math.Round((b1 + m) * 255)
            );
        }
    }
}
