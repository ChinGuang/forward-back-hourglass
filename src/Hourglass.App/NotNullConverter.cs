using System.Globalization;
using System.Windows.Data;

namespace Hourglass.App;

/// <summary>True when the bound value isn't null (e.g. enable "Add" once something is selected).</summary>
public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
