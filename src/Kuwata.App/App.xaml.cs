using System.Globalization;
using System.Windows;

namespace Kuwata.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Числа в полях вводятся как с точкой, так и с запятой — см. MainWindow.ParseDouble.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
