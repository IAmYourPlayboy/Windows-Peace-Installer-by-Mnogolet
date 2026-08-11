using System;
using System.Collections.Generic;
using System.Windows;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup;

public partial class App : Application
{
    private JsonLinesOperationLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = new JsonLinesOperationLog(JsonLinesOperationLog.DefaultPath(AppContext.BaseDirectory));

        var probe = new RealFileSystemProbe();

        var diskPicker = new DiskPickerViewModel(
            new WmiDiskEnumerator(_log),
            new FileSystemContentInspector(probe),
            probe);

        var navigator = new WizardNavigator(new List<IWizardPage>
        {
            diskPicker,
            new PlaceholderViewModel(),
        });

        new ShellWindow { DataContext = new ShellViewModel(navigator) }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Dispose();
        base.OnExit(e);
    }
}
