<#
.SYNOPSIS
    Запускает мастер на обычной Windows, снимает его окно в PNG и закрывает.

.DESCRIPTION
    Быстрый круг проверки: пять секунд против минуты в виртуалке. Годится
    для всего, что не зависит от WinPE, — а это почти вся работа над экранами.
    В WinPE после этого достаточно сходить на контрольных точках.

    Приложение не установлено в системе, и общие средства управления экраном
    его не видят. Работает PrintWindow с флагом PW_RENDERFULLCONTENT: без него
    WPF отдаёт пустой прямоугольник, потому что рисует не через GDI.

    Окно, запущенное из фоновой оболочки, приходит свёрнутым, поэтому его
    разворачивают SW_SHOWNOACTIVATE — так у человека за клавиатурой не отбирают
    то, чем он занят.

.EXAMPLE
    powershell -File tools/Stand/Show-PeaceApp.ps1 -OutPath D:\WindowsPeace-Stand\app.png

.EXAMPLE
    powershell -File tools/Stand/Show-PeaceApp.ps1 -OutPath app.png -KeepOpen
    Оставляет окно открытым — для проверок через UI Automation.
#>
[CmdletBinding()]
param(
    [string] $AppPath = 'artifacts\setup\WindowsPeace.Setup.exe',
    [Parameter(Mandatory = $true)] [string] $OutPath,
    [double] $TimeoutSeconds = 40,
    [double] $StableSeconds = 1.2,
    [switch] $KeepOpen,
    [switch] $NoLog
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceFrames.psm1') -Force
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'Media\PeaceMedia.psm1') -Force

if (-not (Test-Path $AppPath)) {
    throw "Приложения нет: '$AppPath'. Сначала опубликуй его: dotnet publish src\WindowsPeace.Setup -c Release -r win-x64 --self-contained true -o artifacts\setup"
}
$AppPath = (Resolve-Path $AppPath).Path
$appFolder = Split-Path -Parent $AppPath

# Журнал прошлого запуска убирается всегда: иначе записи разных заходов
# идут вперемешку и выдают себя за нынешние.
$logPath = Join-Path $appFolder (Join-Path $PeaceMediaLayout.Logs $PeaceMediaLayout.LogFile)
if (Test-Path $logPath) {
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
}

$process = Start-Process -FilePath $AppPath -WorkingDirectory $appFolder -PassThru
Write-Host "Мастер запущен, номер процесса $($process.Id)."

try {
    # ---------- дождаться окна ----------
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $handle = [IntPtr]::Zero
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "Мастер завершился сам, код $($process.ExitCode). Окна не было. Смотри журнал: $logPath"
        }

        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $handle = $process.MainWindowHandle
            break
        }
        Start-Sleep -Milliseconds 200
    }

    if ($handle -eq [IntPtr]::Zero) {
        throw "Окно не появилось за $TimeoutSeconds с. Мастер жив, но ничего не нарисовал. Смотри журнал: $logPath"
    }

    # Запущенное из фоновой оболочки окно приходит свёрнутым.
    [void][WindowsPeace.Stand.NativeWindow]::ShowWindow($handle, [WindowsPeace.Stand.NativeWindow]::ShowNoActivate)

    # ---------- дождаться, пока оно дорисуется ----------
    $wait = Wait-PeaceStableFrame -Capture { Get-PeaceWindowFrame -WindowHandle $handle } `
        -StableSeconds $StableSeconds -TimeoutSeconds $TimeoutSeconds -PollSeconds 0.25 `
        -What 'окно мастера'

    # Снимок нужен в обоих случаях: недорисованное окно объясняет больше, чем отказ.
    if ($wait.Frame) {
        Save-PeaceFrame -Frame $wait.Frame -Path $OutPath
    }

    if ($wait.Settled) {
        Write-Host "Окно снято: $OutPath ($($wait.Frame.Width)×$($wait.Frame.Height))" -ForegroundColor Green
    }
    else {
        Write-Warning $wait.Reason
        if ($wait.Frame) { Write-Warning "Снят последний кадр: $OutPath" }
    }
}
finally {
    if (-not $KeepOpen) {
        if (-not $process.HasExited) {
            [void]$process.CloseMainWindow()
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
                Write-Warning 'Мастер не закрылся по-хорошему, пришлось снять процесс.'
            }
        }
    }
    else {
        Write-Host "Окно оставлено открытым, номер процесса $($process.Id)."
    }
}

if (-not $NoLog) {
    if (Test-Path $logPath) {
        Write-Host ''
        Write-Host 'Журнал запуска:' -ForegroundColor Cyan
        Get-Content $logPath -Encoding UTF8 | Select-Object -Last 25 | ForEach-Object { Write-Host "  $_" }
    }
    else {
        Write-Warning "Журнала нет: $logPath"
    }
}
