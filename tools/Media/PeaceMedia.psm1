<#
.SYNOPSIS
    Носитель Windows Peace: примонтировать, найти раздел данных, обновить
    приложение, забрать журнал.

.DESCRIPTION
    Всё, что делает с носителем и сборка, и стенд. Собрано в одном месте,
    чтобы проверка занятости диска и поиск раздела данных не расходились
    между скриптами.

    Раздел данных опознаётся по описи в корне — тем же признаком, каким его
    находит сам мастер. Букв в WinPE ждать нельзя, и здесь мы приучаем себя
    к тому же порядку.
#>

Set-StrictMode -Version Latest

<#
    Раскладка носителя: что и под каким именем на нём лежит.

    Те же имена знает ядро — src/WindowsPeace.Core/Media/MediaLayout.cs.
    Разойтись им нельзя: мастер ищет опись по имени, и промах означает,
    что носитель не опознан, то есть предложен под форматирование.
    Языка два, поэтому и мест два, но внутри каждого — одно.
#>
$PeaceMediaLayout = @{
    Manifest = 'windows-peace-media.json'
    App      = 'WindowsPeace'
    Recipes  = 'recipes'
    Images   = 'sources'
    Logs     = 'logs'
    LogFile  = 'windows-peace.jsonl'
}

function Assert-PeaceAdmin {
    <#
    .SYNOPSIS
        Разметка и монтирование без прав администратора не выполняются.
        Проверяется первой строкой, чтобы отказ пришёл до, а не посреди дела.
    #>
    [CmdletBinding()]
    param()

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw @'
Нужны права администратора: разметка и монтирование без них не выполняются.
Приложение Claude надо завершить полностью через значок у часов, а не закрыть
окно, и запустить правой кнопкой → «Запуск от имени администратора».
'@
    }
}

function Get-PeaceVhdxHolder {
    <#
    .SYNOPSIS
        Какие виртуалки держат этот виртуальный диск.

    .DESCRIPTION
        Занятый диск не удалить и не пересобрать. Без этой проверки сборка
        разваливается на середине и оставляет на носителе смесь старого
        с новым, а на экране — прошлое приложение под видом нового.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string] $VhdxPath
    )

    $full = if (Test-Path $VhdxPath) { (Resolve-Path $VhdxPath).Path } else { $VhdxPath }

    @(Get-VM -ErrorAction SilentlyContinue |
        Get-VMHardDiskDrive -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $full } |
        ForEach-Object { $_.VMName } |
        Select-Object -Unique)
}

function Assert-PeaceVhdxFree {
    <#
    .SYNOPSIS
        Отказаться сразу, если диск занят, и сказать, как его освободить.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string] $VhdxPath
    )

    $holders = @(Get-PeaceVhdxHolder -VhdxPath $VhdxPath)
    if ($holders.Count -gt 0) {
        $names = $holders -join ', '
        throw "Виртуальный диск занят виртуалкой: $names. Освободить: Stop-VM -Name '$($holders[0])' -TurnOff -Force; Remove-VM -Name '$($holders[0])' -Force"
    }
}

function Get-PeaceMediaDataRoot {
    <#
    .SYNOPSIS
        Корень раздела данных на уже примонтированном диске.

    .DESCRIPTION
        Опознаётся по описи в корне, а не по букве и не по номеру раздела:
        буква у одного и того же раздела на разных машинах разная.

        Буквы Windows раздаёт не в тот же миг, когда подключился диск. Поэтому
        поиск повторяется несколько раз: иначе изредка вылезал бы отказ «носитель
        собран не до конца» на совершенно исправном носителе, и искать причину
        пришлось бы долго.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [int] $DiskNumber,
        [int] $Attempts = 10,
        [double] $PauseSeconds = 0.5
    )

    $partitions = @()
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $partitions = @(Get-Partition -DiskNumber $DiskNumber -ErrorAction Stop | Where-Object { $_.DriveLetter })
        foreach ($partition in $partitions) {
            $root = "$($partition.DriveLetter):\"
            if (Test-Path (Join-Path $root $PeaceMediaLayout.Manifest)) {
                return $root
            }
        }

        if ($attempt -lt $Attempts) { Start-Sleep -Seconds $PauseSeconds }
    }

    $letters = ($partitions | ForEach-Object { "$($_.DriveLetter):" }) -join ', '
    $seen = if ($letters) { "Разделы с буквами: $letters." } else { 'Ни один раздел не получил буквы.' }
    throw "На диске $DiskNumber нет раздела с описью $($PeaceMediaLayout.Manifest). $seen Носитель собран не до конца?"
}

function Use-PeaceMedia {
    <#
    .SYNOPSIS
        Примонтировать носитель, дать поработать с разделом данных, размонтировать.

    .DESCRIPTION
        Размонтирование в finally: иначе после любой ошибки виртуальный диск
        остаётся висеть в системе, и следующая сборка спотыкается о него.

    .PARAMETER Action
        Блок, которому передаётся корень раздела данных.

    .EXAMPLE
        Use-PeaceMedia -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -Action { param($root) Get-ChildItem $root }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string] $VhdxPath,
        [Parameter(Mandatory = $true)] [scriptblock] $Action
    )

    Assert-PeaceAdmin
    if (-not (Test-Path $VhdxPath)) {
        throw "Носителя нет: '$VhdxPath'. Сначала собери его: tools\Media\Build-PeaceMedia.ps1"
    }
    Assert-PeaceVhdxFree -VhdxPath $VhdxPath

    # Диск мог остаться примонтированным с прошлого раза — тогда Mount-VHD откажет.
    $disk = Get-VHD -Path $VhdxPath
    if ($disk.Attached) {
        $diskNumber = $disk.DiskNumber
    }
    else {
        $diskNumber = (Mount-VHD -Path $VhdxPath -Passthru | Get-Disk).Number
    }
    $mountedHere = -not $disk.Attached

    try {
        $root = Get-PeaceMediaDataRoot -DiskNumber $diskNumber
        & $Action $root
    }
    finally {
        if ($mountedHere) {
            Dismount-VHD -Path $VhdxPath -ErrorAction SilentlyContinue
        }
    }
}

function Update-PeaceMediaApp {
    <#
    .SYNOPSIS
        Подменить приложение на носителе, не пересобирая его целиком.

    .DESCRIPTION
        Полная сборка перекладывает загрузочные файлы и boot.wim — две трети
        гигабайта, около минуты. Когда менялось только приложение, всё это
        лишнее: достаточно обновить одну папку. Круг проверки становится
        быстрее в разы, а быстрый круг проходят чаще.

        Разметка при этом не трогается, поэтому носитель остаётся загрузочным.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string] $VhdxPath,
        [Parameter(Mandatory = $true)] [string] $AppFolder,
        [string] $DiskDumpFolder,
        [switch] $ResetLog
    )

    if (-not (Test-Path (Join-Path $AppFolder 'WindowsPeace.Setup.exe'))) {
        throw "В '$AppFolder' нет WindowsPeace.Setup.exe. Сначала опубликуй приложение."
    }
    $appFull = (Resolve-Path $AppFolder).Path
    $dumpFull = if ($DiskDumpFolder -and (Test-Path $DiskDumpFolder)) { (Resolve-Path $DiskDumpFolder).Path } else { $null }

    Use-PeaceMedia -VhdxPath $VhdxPath -Action {
        param($root)

        $target = Join-Path $root $PeaceMediaLayout.App

        # /MIR, а не /E: иначе файлы, исчезнувшие из публикации, остались бы
        # на носителе и продолжали запускаться.
        #
        # Папка logs не переносится ни в ту, ни в другую сторону. Иначе журнал
        # запусков на хозяйской машине уезжает на носитель и выдаёт себя
        # за журнал из WinPE — однажды это уже сбило с толку.
        robocopy $appFull $target /MIR /R:2 /W:2 /NFL /NDL /NP /XD DiskDump $($PeaceMediaLayout.Logs) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy приложения завершился с кодом $LASTEXITCODE." }

        if ($dumpFull) {
            robocopy $dumpFull (Join-Path $target 'DiskDump') /MIR /R:2 /W:2 /NFL /NDL /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy DiskDump завершился с кодом $LASTEXITCODE." }
        }

        # Папка logs исключена из зеркалирования, а значит и не стирается им.
        # Без явной уборки записи прошлых заходов копятся на носителе и выдают
        # себя за нынешние — разбирать такой журнал невозможно.
        if ($ResetLog) {
            $logFolder = Join-Path $target $PeaceMediaLayout.Logs
            if (Test-Path $logFolder) {
                Remove-Item (Join-Path $logFolder '*') -Force -Recurse -ErrorAction SilentlyContinue
            }
        }

        Write-Host "Приложение на носителе обновлено: $target" -ForegroundColor Green
    }
}

function Get-PeaceMediaLog {
    <#
    .SYNOPSIS
        Забрать журнал мастера с носителя.

    .DESCRIPTION
        В WinPE это единственное, что остаётся после перезагрузки. Виртуалку
        к этому моменту надо уже выключить и удалить: пока она жива, диск занят.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string] $VhdxPath,
        [string] $OutPath
    )

    Use-PeaceMedia -VhdxPath $VhdxPath -Action {
        param($root)

        $logPath = Join-Path $root (Join-Path $PeaceMediaLayout.App (Join-Path $PeaceMediaLayout.Logs $PeaceMediaLayout.LogFile))
        if (-not (Test-Path $logPath)) {
            Write-Warning "Журнала на носителе нет: $logPath. Мастер не дошёл до записи или писать было некуда."
            return @()
        }

        $lines = Get-Content $logPath -Encoding UTF8
        if ($OutPath) {
            $folder = Split-Path -Parent $OutPath
            if ($folder -and -not (Test-Path $folder)) {
                New-Item -ItemType Directory -Force -Path $folder | Out-Null
            }
            Set-Content -Path $OutPath -Value $lines -Encoding UTF8
        }

        $lines
    }
}

Export-ModuleMember -Variable PeaceMediaLayout -Function Assert-PeaceAdmin, Get-PeaceVhdxHolder,
    Assert-PeaceVhdxFree, Get-PeaceMediaDataRoot, Use-PeaceMedia, Update-PeaceMediaApp, Get-PeaceMediaLog
