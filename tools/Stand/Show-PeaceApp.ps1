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

.EXAMPLE
    & .\tools\Stand\Show-PeaceApp.ps1 -OutPath app.png -AppArgs @('--media', 'D:\проба')
    Показывает мастеру опись из указанной папки.

    Вызывать здесь надо через «&», а не через «powershell -File»: последний
    передаёт массив одной строкой, ключ до мастера не доходит, и выглядит это
    как будто носитель просто не нашёлся.

.EXAMPLE
    & .\tools\Stand\Show-PeaceApp.ps1 -OutPath app.png -Advance 2 -AppArgs @('--media', 'D:\проба')
    Доходит до третьего экрана: на каждом выбирает первую доступную строку
    и нажимает «Далее». Снимает то, что вышло.
#>
[CmdletBinding()]
param(
    [string] $AppPath = 'artifacts\setup\WindowsPeace.Setup.exe',
    [Parameter(Mandatory = $true)] [string] $OutPath,
    [double] $TimeoutSeconds = 40,
    [double] $StableSeconds = 1.2,

    # Что передать самому мастеру. Например: -AppArgs '--media','D:\проба'
    # На обычной Windows описи нет нигде, и без этого ключа половину работы
    # над экранами пришлось бы делать в WinPE.
    [string[]] $AppArgs = @(),

    # Сколько экранов пройти вперёд, прежде чем снимать. На каждом выбирается
    # первая доступная строка и нажимается «Далее». Без этого работа над
    # третьим экраном требовала бы человека с мышью.
    [int] $Advance = 0,

    # Что вписать в поле ввода на последнем экране. Для сводки это модель диска:
    # подтверждение вводом иначе не проверить.
    [string] $TypeText,

    [switch] $KeepOpen,
    [switch] $NoLog
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceFrames.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PeaceUia.psm1') -Force
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'Media\PeaceMedia.psm1') -Force

if (-not (Test-Path $AppPath)) {
    throw "Приложения нет: '$AppPath'. Сначала опубликуй его: dotnet publish src\WindowsPeace.Setup -c Release -r win-x64 --self-contained true -o artifacts\setup"
}
$AppPath = (Resolve-Path $AppPath).Path
$appFolder = Split-Path -Parent $AppPath

# Журналы прошлого запуска убираются всегда: иначе записи разных заходов
# идут вперемешку и выдают себя за нынешние. Убирается вся папка, а не один
# файл: занятое имя мастер обходит соседним, «windows-peace-2.jsonl».
$logFolder = Join-Path $appFolder $PeaceMediaLayout.Logs
if (Test-Path $logFolder) {
    Remove-Item (Join-Path $logFolder '*.jsonl') -Force -ErrorAction SilentlyContinue
}

$start = @{ FilePath = $AppPath; WorkingDirectory = $appFolder; PassThru = $true }
if ($AppArgs.Count -gt 0) { $start.ArgumentList = $AppArgs }

$process = Start-Process @start
Write-Host "Мастер запущен, номер процесса $($process.Id)."

try {
    # ---------- дождаться окна ----------
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $handle = [IntPtr]::Zero
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "Мастер завершился сам, код $($process.ExitCode). Окна не было. Смотри журнал: $logFolder"
        }

        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $handle = $process.MainWindowHandle
            break
        }
        Start-Sleep -Milliseconds 200
    }

    if ($handle -eq [IntPtr]::Zero) {
        throw "Окно не появилось за $TimeoutSeconds с. Мастер жив, но ничего не нарисовал. Смотри журнал: $logFolder"
    }

    # Запущенное из фоновой оболочки окно приходит свёрнутым.
    [void][WindowsPeace.Stand.NativeWindow]::ShowWindow($handle, [WindowsPeace.Stand.NativeWindow]::ShowNoActivate)

    $window = Get-PeaceWindowElement -WindowHandle $handle

    # ---------- пройти вперёд по экранам ----------
    if ($Advance -gt 0 -or $TypeText) {
        for ($step = 1; $step -le $Advance; $step++) {
            # Список есть не на каждом экране — на сводке выбирать нечего.
            # А там, где он есть, он наполняется не сразу: диски опрашиваются
            # в стороннем потоке. Оба случая закрыты одним ожиданием.
            $ready = Wait-PeaceScreenReady -Root $window -TimeoutSeconds $TimeoutSeconds
            if (-not $ready) {
                throw "Экран $step не ожил за $TimeoutSeconds с: ни строки для выбора, ни поля ввода, ни доступной кнопки перехода."
            }

            if ($ready.Item) {
                $chosen = Select-PeaceItem -Item $ready.Item
                Write-Host "Экран $step`: выбрано «$chosen»."
            }

            if ($ready.Field -and $TypeText) {
                Set-PeaceText -Root $window -Text $TypeText -TimeoutSeconds $TimeoutSeconds
                Write-Host "Экран $step`: вписано «$TypeText»."
            }

            # Ожидание, пока кнопка перехода оживёт, — половина проверки: она
            # оживает тогда и только тогда, когда выбор дошёл до модели экрана.
            # Ищется по неизменной метке, а не по слову: слово даёт страница.
            $pressed = Invoke-PeaceButton -Root $window -AutomationId 'Next' -TimeoutSeconds $TimeoutSeconds
            Write-Host "Экран $step`: нажато «$pressed»."
        }

        if ($TypeText -and $Advance -eq 0) {
            Set-PeaceText -Root $window -Text $TypeText -TimeoutSeconds $TimeoutSeconds
            Write-Host "Вписано в поле: «$TypeText»."
        }
    }

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

    # Что написано на экране словами. Пустая строка там, где ждали значение, —
    # самый частый след опечатки в привязке: тесты при этом зелёные.
    Write-Host ''
    Write-Host 'Что написано на экране:' -ForegroundColor Cyan
    foreach ($line in (Get-PeaceScreenText -Root $window)) { Write-Host "  $line" }

    # Кнопки оболочки перечисляются по меткам, а не по надписям: надпись
    # на кнопке перехода меняется от страницы, и список слов пришлось бы
    # дописывать при каждом новом экране.
    foreach ($id in @('Back', 'Next', 'Close')) {
        $button = Get-PeaceButton -Root $window -AutomationId $id
        if ($button) {
            $state = if ($button.Current.IsEnabled) { 'доступна' } else { 'выключена' }
            Write-Host "  [кнопка «$($button.Current.Name)» — $state]"
        }
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
    $written = @(Get-ChildItem -LiteralPath $logFolder -Filter '*.jsonl' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)

    if ($written.Count -gt 0) {
        Write-Host ''
        Write-Host "Журнал запуска ($($written[0].Name)):" -ForegroundColor Cyan
        Get-Content $written[0].FullName -Encoding UTF8 | Select-Object -Last 25 | ForEach-Object { Write-Host "  $_" }
    }
    else {
        Write-Warning "Журнала нет: $logFolder"
    }
}
