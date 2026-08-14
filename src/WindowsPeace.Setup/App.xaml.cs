using System;
using System.Collections.Generic;
using System.Windows;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;
using WindowsPeace.Core.Storage.Native;
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

        // Прямой разговор с Windows вместо WMI: библиотека System.Management
        // в WinPE не работает, она подгружает модуль из .NET Framework, которого
        // там нет. Проверено опытом, см. docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md.
        var diskPicker = new DiskPickerViewModel(
            new NativeDiskEnumerator(new Win32StorageSource(), _log),
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
