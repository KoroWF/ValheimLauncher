using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace ValheimLauncher
{
    public class ProgressToClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is double value && values[1] is double maximum && values[2] is double width)
            {
                double progressWidth = (value / maximum) * width; // Berechnet die Breite basierend auf dem Fortschritt
                return new Rect(0, 0, progressWidth, 6); // Höhe = 6, wie die ProgressBar
            }
            return new Rect(0, 0, 0, 6); // Fallback bei 0%
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
