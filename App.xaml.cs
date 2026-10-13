using System;
using System.Threading;
using System.Windows;

namespace CrossbarChaos;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 1 &&
            string.Equals(
                e.Args[0],
                "--restore-stats-api",
                StringComparison.OrdinalIgnoreCase))
        {
            string errors = new StatsApiConfiguration().Restore();

            if (!string.IsNullOrWhiteSpace(errors))
            {
                MessageBox.Show(
                    errors,
                    "Stats API Restoration Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Shutdown(1);
                return;
            }

            Shutdown(0);
            return;
        }

        _singleInstanceMutex = new Mutex(
            true,
            @"Local\CrossbarChaos.SingleInstance",
            out bool createdNew);

        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;

            MessageBox.Show(
                "Crossbar Chaos is already running.",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex != null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}

