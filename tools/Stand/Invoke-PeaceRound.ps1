<#
.SYNOPSIS
    Полный круг проверки в WinPE одной командой: собрать, загрузить, запустить,
    снять экран, забрать журнал.

.DESCRIPTION
    Раньше это была простыня из двенадцати команд со слепыми паузами. Порядок
    в ней приходилось держать в голове, а любой сбой посреди оставлял стенд
    в непонятном состоянии — вплоть до того, что на экране оказывалось прошлое
    приложение под видом нового.

    Здесь круг делается целиком и объясняет каждый свой шаг. Три правила:

    1. Ждать по признаку, а не по часам. Признак готовности один и тот же:
       картинка перестала меняться. Медленная машина не ломает круг, быстрая
       не заставляет ждать зря.
    2. Всегда доводить до снимка и журнала. Не дождались окна — это тоже
       результат, и смотреть на него надо на картинке, а не гадать.
    3. Освобождать носитель самому. Занятый виртуалкой диск — обычное начало
       круга, а не повод отказаться.

    По умолчанию обновляется только приложение: полная пересборка перекладывает
    две трети гигабайта загрузочных файлов, которые не менялись. Полный круг
    нужен, когда менялся сам носитель, — ключ -Media Full.

.EXAMPLE
    powershell -File tools/Stand/Invoke-PeaceRound.ps1
    Обычный круг: опубликовать, подменить приложение, загрузиться, снять экран.

.EXAMPLE
    powershell -File tools/Stand/Invoke-PeaceRound.ps1 -Media Full
    То же, но носитель собирается заново — после правок в Build-PeaceMedia.ps1.

.EXAMPLE
    powershell -File tools/Stand/Invoke-PeaceRound.ps1 -Run 'WindowsPeace\DiskDump\DiskDump.exe' -Media Full
    Запустить в WinPE не мастера, а отладочную утилиту.
#>
[CmdletBinding()]
param(
    [ValidateSet('App', 'Full')] [string] $Media = 'App',
    [switch] $SkipPublish,

    [string] $VhdxPath = 'D:\WindowsPeace-Stand\peace.vhdx',
    [string] $AppFolder = 'artifacts\setup',
    [string] $DiskDumpFolder,
    [string] $OutFolder = 'D:\WindowsPeace-Stand\round',
    [string] $VmName = 'Windows Peace Stand',

    # Что запустить на носителе. Путь от корня раздела данных; по умолчанию —
    # сам мастер. Значение подставляется ниже: раскладка носителя живёт в модуле,
    # а он к моменту разбора ключей ещё не подключён.
    [string] $Run,
    [switch] $NoRun,
    [switch] $KeepRunning,

    # Где искать носитель в WinPE. Буквы там непредсказуемы: раздел данных
    # однажды оказался C:, и опираться на них нельзя — только перебор.
    [string] $SearchLetters = 'C D E F G H I J',

    [double] $BootTimeoutSeconds = 240,
    [double] $RunTimeoutSeconds = 150
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Import-Module (Join-Path $PSScriptRoot 'PeaceFrames.psm1') -Force
Import-Module (Join-Path $repoRoot 'tools\Media\PeaceMedia.psm1') -Force

Assert-PeaceAdmin

function Resolve-RepoPath {
    <#
        Относительный путь считается от корня репозитория, абсолютный берётся
        как есть. Без этого Join-Path склеивает «D:\repo» и «D:\setup»
        в «D:\repo\D:\setup», а ошибка потом приходит совсем из другого места.
    #>
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    Join-Path $repoRoot $Path
}

if ([string]::IsNullOrWhiteSpace($Run)) {
    $Run = Join-Path $PeaceMediaLayout.App 'WindowsPeace.Setup.exe'
}

# Насколько сильно должна измениться картинка, чтобы считаться новой.
# Окно командной строки занимает треть экрана, окно мастера — почти весь;
# перевод строки и мигающий курсор не дотягивают ни до того, ни до другого.
$CmdAppearedChange = 0.03
$AppAppearedChange = 0.2

$started = Get-Date
$step = 0
function Write-Step {
    param([string] $Text)
    $script:step++
    $elapsed = ((Get-Date) - $started).TotalSeconds
    Write-Host ("[{0}] {1,5:N0} с  {2}" -f $script:step, $elapsed, $Text) -ForegroundColor Cyan
}

if (-not (Test-Path $OutFolder)) { New-Item -ItemType Directory -Force -Path $OutFolder | Out-Null }
$bootShot = Join-Path $OutFolder '01-boot.png'
$cmdShot  = Join-Path $OutFolder '02-cmd.png'
$runShot  = Join-Path $OutFolder '03-run.png'
$logCopy  = Join-Path $OutFolder $PeaceMediaLayout.LogFile

# ---------- 1. приложение ----------

if ($SkipPublish) {
    Write-Step 'Публикация пропущена по ключу.'
}
else {
    Write-Step 'Публикую приложение...'
    $publish = & dotnet publish (Join-Path $repoRoot 'src\WindowsPeace.Setup') `
        -c Release -r win-x64 --self-contained true -o (Resolve-RepoPath $AppFolder) --nologo -v q 2>&1
    if ($LASTEXITCODE -ne 0) {
        $publish | ForEach-Object { Write-Host $_ }
        throw 'Публикация не прошла. Круг дальше не идёт: на носитель нечего класть.'
    }
}

# ---------- 2. освободить носитель ----------

$holders = @(Get-PeaceVhdxHolder -VhdxPath $VhdxPath)
if ($holders.Count -gt 0) {
    Write-Step "Освобождаю носитель: его держит $($holders -join ', ')."
    foreach ($holder in $holders) {
        $vm = Get-VM -Name $holder -ErrorAction SilentlyContinue
        if ($vm -and $vm.State -ne 'Off') {
            Stop-VM -Name $holder -TurnOff -Force -ErrorAction SilentlyContinue
        }
        Remove-VM -Name $holder -Force -ErrorAction SilentlyContinue
    }
}

# ---------- 3. носитель ----------

$mediaMode = $Media
if ($mediaMode -eq 'App' -and -not (Test-Path $VhdxPath)) {
    Write-Step 'Носителя ещё нет — собираю целиком, хотя просили только приложение.'
    $mediaMode = 'Full'
}

if ($mediaMode -eq 'Full') {
    Write-Step 'Собираю носитель целиком...'
    $buildArgs = @{
        VhdxPath       = $VhdxPath
        AppFolder      = (Resolve-RepoPath $AppFolder)
        SkipInstallWim = $true
    }
    if ($DiskDumpFolder) { $buildArgs.DiskDumpFolder = (Resolve-RepoPath $DiskDumpFolder) }
    & (Join-Path $repoRoot 'tools\Media\Build-PeaceMedia.ps1') @buildArgs
}
else {
    Write-Step 'Подменяю приложение на носителе...'
    $updateArgs = @{
        VhdxPath  = $VhdxPath
        AppFolder = (Resolve-RepoPath $AppFolder)
        ResetLog  = $true
    }
    if ($DiskDumpFolder) { $updateArgs.DiskDumpFolder = (Resolve-RepoPath $DiskDumpFolder) }
    Update-PeaceMediaApp @updateArgs
}

# ---------- 4. виртуалка ----------

Write-Step 'Создаю виртуалку и включаю её...'
& (Join-Path $PSScriptRoot 'New-PeaceVm.ps1') -Name $VmName -VhdxPath $VhdxPath | Out-Null
Start-VM -Name $VmName | Out-Null

$settled = $true
$trouble = @()

try {
    # ---------- 5. загрузка ----------

    Write-Step 'Жду, пока WinPE загрузится (по картинке, а не по часам)...'
    $boot = Wait-PeaceStableFrame -Capture { Get-PeaceVmFrame -Name $VmName } `
        -TimeoutSeconds $BootTimeoutSeconds -MinWaitSeconds 12 -StableSeconds 3 `
        -What 'экран загрузки'

    if ($boot.Frame) { Save-PeaceFrame -Frame $boot.Frame -Path $bootShot }
    if (-not $boot.Settled) {
        $settled = $false
        $trouble += $boot.Reason
        Write-Warning $boot.Reason
    }
    else {
        Write-Step "Среда загрузилась. Экран: $bootShot"
    }

    if ($NoRun) {
        Write-Step 'Запуск пропущен по ключу -NoRun.'
    }
    elseif ($boot.Settled) {
        # ---------- 6. командная строка ----------

        Write-Step 'Открываю командную строку (Shift+F10)...'
        & (Join-Path $PSScriptRoot 'Send-PeaceVmKeys.ps1') -Name $VmName -ShiftF10 | Out-Null

        $cmd = Wait-PeaceStableFrame -Capture { Get-PeaceVmFrame -Name $VmName } `
            -TimeoutSeconds 45 -StableSeconds 1.5 -PollSeconds 0.5 `
            -DifferentFrom $boot.Frame -MinDifference $CmdAppearedChange `
            -What 'окно командной строки'

        if ($cmd.Frame) { Save-PeaceFrame -Frame $cmd.Frame -Path $cmdShot }
        if (-not $cmd.Settled) {
            $settled = $false
            $trouble += 'Командная строка не открылась: ' + $cmd.Reason
            Write-Warning $cmd.Reason
        }
        else {
            # ---------- 7. запуск ----------

            # Носитель ищется по описи в корне — тем же признаком, каким его
            # находит сам мастер. Поиск и запуск одной строкой: лишний шаг —
            # лишние полторы секунды и лишнее место, где круг может сбиться.
            $line = "for %d in ($SearchLetters) do @if exist %d:\$($PeaceMediaLayout.Manifest) %d:\$Run"

            Write-Step "Набираю: $line"
            & (Join-Path $PSScriptRoot 'Send-PeaceVmKeys.ps1') -Name $VmName -Text $line | Out-Null

            # Кадр снимается после набора, но до Enter: иначе появившийся
            # на экране текст сам по себе сойдёт за «что-то изменилось».
            Start-Sleep -Milliseconds 700
            $typed = Get-PeaceVmFrame -Name $VmName

            & (Join-Path $PSScriptRoot 'Send-PeaceVmKeys.ps1') -Name $VmName -Enter | Out-Null

            Write-Step 'Жду, пока на экране появится приложение...'
            # Порог в пятую часть экрана отделяет окно во весь экран от того,
            # что командная строка просто перевела строку или напечатала отказ.
            $app = Wait-PeaceStableFrame -Capture { Get-PeaceVmFrame -Name $VmName } `
                -TimeoutSeconds $RunTimeoutSeconds -StableSeconds 2.5 `
                -DifferentFrom $typed -MinDifference $AppAppearedChange `
                -What 'окно приложения'

            if ($app.Frame) { Save-PeaceFrame -Frame $app.Frame -Path $runShot }
            if (-not $app.Settled) {
                $settled = $false
                $trouble += 'Приложение не показалось: ' + $app.Reason
                Write-Warning $app.Reason
                Write-Warning "Последний кадр всё равно снят: $runShot. Смотри на него — там может быть отказ в командной строке."
            }
            else {
                Write-Step "Приложение на экране. Снимок: $runShot"
            }
        }
    }
}
finally {
    # ---------- 8. журнал ----------
    # Забирается всегда, даже когда круг не удался: именно тогда он и нужен.

    if (-not $KeepRunning) {
        # WinPE не отвечает на кнопку питания: службы, которая её слушает, там нет.
        # Гость обесточивается, и всё, что мастер не успел сбросить на диск, пропадает.
        # Поэтому журнал пишется со сбросом на диск после каждой записи.
        Write-Step 'Выключаю виртуалку и забираю журнал с носителя...'
        $vm = Get-VM -Name $VmName -ErrorAction SilentlyContinue
        if ($vm -and $vm.State -ne 'Off') {
            Stop-VM -Name $VmName -TurnOff -Force -ErrorAction SilentlyContinue
        }
        Remove-VM -Name $VmName -Force -ErrorAction SilentlyContinue

        $log = @(Get-PeaceMediaLog -VhdxPath $VhdxPath -OutPath $logCopy)
        if ($log.Count -gt 0) {
            Write-Host ''
            Write-Host "Журнал мастера ($($log.Count) записей, копия в $logCopy):" -ForegroundColor Cyan
            $log | ForEach-Object { Write-Host "  $_" }
        }
    }
    else {
        Write-Step "Виртуалка оставлена работать. Журнал не забрать, пока она жива."
    }
}

# ---------- итог ----------

Write-Host ''
$total = ((Get-Date) - $started).TotalSeconds
if ($settled) {
    Write-Host ("Круг пройден за {0:N0} с. Смотреть: {1}" -f $total, $runShot) -ForegroundColor Green
}
else {
    Write-Host ("Круг пройден не до конца, {0:N0} с." -f $total) -ForegroundColor Yellow
    $trouble | ForEach-Object { Write-Host "  — $_" -ForegroundColor Yellow }
    Write-Host "Снимки: $OutFolder" -ForegroundColor Yellow
    exit 1
}
