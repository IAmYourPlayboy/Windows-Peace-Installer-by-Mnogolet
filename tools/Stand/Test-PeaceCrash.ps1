<#
.SYNOPSIS
    Проверяет, что неожиданная ошибка не выходит человеку системным окном.

.DESCRIPTION
    Путь обработки ошибки — тоже путь, и работать он обязан проверенно.
    Настоящую неожиданную ошибку по заказу не устроишь, поэтому мастер принимает
    отладочный ключ --crash: он роняет его отдельным действием окна, ровно так,
    как ошибки случаются на самом деле.

    Проверяется три вещи разом: человек читает объяснение по-русски, а не
    трассировку стека; мастер закрывается сам; в журнале остаётся всё, включая
    саму ошибку с трассировкой.

    Модальное окно ищется перебором окон процесса по классу #32770, а не через
    UI Automation: свежесозданное окно UIA среди детей рабочего стола показывает
    не сразу, а внутрь такого окна в это время не заглядывает вовсе.

.EXAMPLE
    powershell -File tools\Stand\Test-PeaceCrash.ps1
#>
[CmdletBinding()]
param(
    [string] $AppPath = 'artifacts\setup\WindowsPeace.Setup.exe',
    [string] $Media = 'D:\WindowsPeace-Stand\fake-media',
    [double] $TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'Media\PeaceMedia.psm1') -Force

Add-Type -Language CSharp -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PeaceDialog {
  delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetDlgItemText(IntPtr dlg, int id, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

  /// <summary>Первое стандартное окно процесса, если оно есть.</summary>
  public static IntPtr Find(uint processId) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, p) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == processId) {
        var name = new StringBuilder(64); GetClassName(h, name, 64);
        if (name.ToString() == "#32770") { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }

  /// <summary>Текст сообщения: у него в стандартном окне постоянный номер 0xFFFF.</summary>
  public static string Text(IntPtr dialog) {
    var text = new StringBuilder(2048);
    GetDlgItemText(dialog, 0xFFFF, text, text.Capacity);
    return text.ToString();
  }

  /// <summary>
  /// Закрыть окно так же, как это делает человек. WM_COMMAND с номером «ОК»
  /// стандартное окно не закрывает — проверено, оно остаётся на экране.
  /// </summary>
  public static void Close(IntPtr dialog) {
    SendMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
  }
}
"@

if (-not (Test-Path $AppPath)) {
    throw "Приложения нет: '$AppPath'. Сначала опубликуй его: dotnet publish src\WindowsPeace.Setup -c Release -r win-x64 --self-contained true -o artifacts\setup"
}
$AppPath = (Resolve-Path $AppPath).Path
$appFolder = Split-Path -Parent $AppPath

$logFolder = Join-Path $appFolder $PeaceMediaLayout.Logs
if (Test-Path $logFolder) {
    Remove-Item (Join-Path $logFolder '*.jsonl') -Force -ErrorAction SilentlyContinue
}

$process = Start-Process -FilePath $AppPath -WorkingDirectory $appFolder `
    -ArgumentList @('--media', $Media, '--crash') -PassThru
Write-Host "Мастер запущен с ключом --crash, номер процесса $($process.Id)."

$failures = @()
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $dialog = [IntPtr]::Zero
    while ((Get-Date) -lt $deadline -and $dialog -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 300
        if ($process.HasExited) { break }
        $dialog = [PeaceDialog]::Find([uint32]$process.Id)
    }

    if ($dialog -eq [IntPtr]::Zero) {
        throw 'Объяснения человеку не было: мастер упал молча или показал системное окно .NET.'
    }

    $text = [PeaceDialog]::Text($dialog)
    Write-Host ''
    Write-Host 'Что прочитает человек:' -ForegroundColor Cyan
    $text -split "`r`n" | ForEach-Object { Write-Host "  $_" }
    Write-Host ''

    # Трассировка стека, слово Exception и английские извинения человеку
    # не показываются никогда: это наша беда, а не его.
    foreach ($forbidden in @('Exception', '   at ', 'Stack')) {
        if ($text -like "*$forbidden*") { $failures += "В объяснении человеку есть «$forbidden»." }
    }
    if ($text -notmatch '[А-Яа-я]') { $failures += 'Объяснение не по-русски.' }

    [PeaceDialog]::Close($dialog)

    if (-not $process.WaitForExit(10000)) {
        $failures += 'Окно закрыто, а мастер остался на экране.'
    }
    elseif ($process.ExitCode -ne 0) {
        $failures += "Мастер вышел с кодом $($process.ExitCode), а должен закрыться по-хорошему."
    }
}
finally {
    if (-not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
}

$written = @(Get-ChildItem -LiteralPath $logFolder -Filter '*.jsonl' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)

if ($written.Count -eq 0) {
    $failures += 'Журнала нет вовсе, а падение обязано в нём остаться.'
}
else {
    $lines = Get-Content $written[0].FullName -Encoding UTF8
    Write-Host 'Хвост журнала:' -ForegroundColor Cyan
    $lines | Select-Object -Last 4 | ForEach-Object {
        Write-Host "  $($_.Substring(0, [Math]::Min(200, $_.Length)))"
    }

    if (-not ($lines -match 'Необработанная ошибка')) {
        $failures += 'В журнале нет записи о необработанной ошибке.'
    }
    if (-not ($lines -match 'InvalidOperationException')) {
        $failures += 'В журнале нет самой ошибки с трассировкой — разбирать будет нечего.'
    }
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'Путь ошибки в порядке: человеку объяснение по-русски, нам — журнал целиком.' -ForegroundColor Green
}
else {
    $failures | ForEach-Object { Write-Warning $_ }
    throw "Путь ошибки сломан: $($failures.Count) замечаний."
}
