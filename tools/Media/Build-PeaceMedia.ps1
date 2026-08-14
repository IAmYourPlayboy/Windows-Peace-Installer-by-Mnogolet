<#
.SYNOPSIS
    Собирает носитель Windows Peace: два раздела, загрузочные файлы, приложение, опись.

.DESCRIPTION
    Прошивка UEFI грузится только с FAT32, а FAT32 не держит файл больше 4 ГБ
    при install.wim в девять. Отсюда два раздела: загрузочный FAT32 и NTFS
    для всего остального. Первому в самом конце ставится тип «системный EFI» —
    Windows таким разделам букву не выдаёт, и в проводнике виден один диск.

    Цель задаётся одним из двух: -VhdxPath (создаётся виртуальный диск)
    или -UsbDiskNumber (переразмечается физический съёмный диск).

    Это черновик того, что переедет в WindowsPeace.Studio на шаге Е.

.EXAMPLE
    powershell -File tools/Media/Build-PeaceMedia.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -AppFolder artifacts\setup -SkipInstallWim

.EXAMPLE
    powershell -File tools/Media/Build-PeaceMedia.ps1 -UsbDiskNumber 2 -ConfirmModel "VendorC ProductCode" -AppFolder artifacts\setup
#>
[CmdletBinding(DefaultParameterSetName = 'Vhdx')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Vhdx')] [string] $VhdxPath,
    [Parameter(ParameterSetName = 'Vhdx')] [uint64] $VhdxSizeBytes = 24GB,

    [Parameter(Mandatory = $true, ParameterSetName = 'Usb')] [int] $UsbDiskNumber,
    [Parameter(Mandatory = $true, ParameterSetName = 'Usb')] [string] $ConfirmModel,

    # Загрузочный раздел. Двух гигабайт хватает: внутри только boot.wim
    # (около 0,7 ГБ) и загрузчик. Вынесено ключом, потому что размер boot.wim
    # зависит от образа, а промах здесь виден только в конце сборки.
    [uint64] $BootPartitionBytes = 2GB,

    [string] $SourceRoot = 'D:\WindowsPeace-Source',
    [Parameter(Mandatory = $true)] [string] $AppFolder,
    [string] $DiskDumpFolder,
    [string] $RecipeFile = 'contract\examples\atlas-25h2-ru.recipe.json',

    # Имя издания внутри install.wim. По умолчанию берётся из самого рецепта
    # (source.windows.imageName); задаётся здесь только для разбора случаев,
    # когда образ собран нестандартно.
    [string] $ImageName,

    [switch] $SkipInstallWim
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceMedia.psm1') -Force

$TypeEsp  = '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'
$TypeData = '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'

function Get-ImageIndex {
    <#
        Номер издания внутри install.wim ищется по имени, а не задаётся числом.
        В образе Windows 11 25H2 под первым номером лежит Домашняя, а Pro —
        под вторым; в другой сборке порядок может оказаться иным. Ошибка здесь
        уводит установку не на то издание, и заметно это станет только на шаге В.
    #>
    param([string] $WimPath, [string] $WantedName)

    $output = & dism /English /Get-WimInfo /WimFile:$WimPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "DISM не смог прочитать '$WimPath'." }

    $index = $null
    $found = @()
    foreach ($line in $output) {
        if ($line -match '^\s*Index\s*:\s*(\d+)\s*$') { $index = [int]$Matches[1]; continue }
        if ($line -match '^\s*Name\s*:\s*(.+?)\s*$' -and $null -ne $index) {
            $name = $Matches[1]
            $found += "$index — $name"
            if ($name -eq $WantedName) { return $index }
            $index = $null
        }
    }

    throw "В '$WimPath' нет издания '$WantedName'. Есть: $($found -join '; ')."
}

Assert-PeaceAdmin

if (-not (Test-Path (Join-Path $SourceRoot 'sources\boot.wim'))) {
    throw "В '$SourceRoot' нет sources\boot.wim. Сначала запусти Save-InstallSource.ps1."
}
if (-not (Test-Path (Join-Path $AppFolder 'WindowsPeace.Setup.exe'))) {
    throw "В '$AppFolder' нет WindowsPeace.Setup.exe. Сначала опубликуй приложение."
}
if (-not (Test-Path $RecipeFile)) {
    throw "Файл рецепта '$RecipeFile' не найден."
}

$sourceInstallWim = Join-Path $SourceRoot 'sources\install.wim'
if (-not (Test-Path $sourceInstallWim)) {
    throw "В '$SourceRoot' нет sources\install.wim."
}

# ---------- что мы вообще собираем: спрашиваем у рецепта ----------
# Рецепт — единый контракт проекта, и в его схеме записано прямо: всё, что
# можно выразить там, не должно быть зашито в код. Название и пояснение
# на первом экране мастера человек прочитает отсюда; если писать их здесь,
# опись начнёт врать при первом же другом рецепте.
try {
    $recipe = Get-Content -LiteralPath $RecipeFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "Рецепт '$RecipeFile' не разбирается: $($_.Exception.Message)"
}

$recipeTitle = $recipe.meta.name
if ([string]::IsNullOrWhiteSpace($recipeTitle)) {
    throw "В рецепте '$RecipeFile' нет meta.name — а это название, которое человек увидит на первом экране."
}
$recipeDescription = $recipe.meta.description

if ([string]::IsNullOrWhiteSpace($ImageName)) {
    $ImageName = $recipe.source.windows.imageName
    if ([string]::IsNullOrWhiteSpace($ImageName)) {
        throw "В рецепте '$RecipeFile' нет source.windows.imageName, и ключ -ImageName не задан. Издание внутри install.wim определить нечем."
    }
}

$imageIndex = Get-ImageIndex -WimPath $sourceInstallWim -WantedName $ImageName
Write-Host "Рецепт '$recipeTitle': издание '$ImageName' лежит в install.wim под номером $imageIndex."

# ---------- получаем чистый диск ----------

$usingVhdx = $PSCmdlet.ParameterSetName -eq 'Vhdx'

if ($usingVhdx) {
    # Занятый виртуальный диск не удалить, и сборка развалилась бы на середине,
    # оставив на носителе смесь старого и нового. Проверяем до первого действия.
    Assert-PeaceVhdxFree -VhdxPath $VhdxPath

    $vhdxFolder = Split-Path -Parent $VhdxPath
    if ($vhdxFolder -and -not (Test-Path $vhdxFolder)) {
        New-Item -ItemType Directory -Force -Path $vhdxFolder | Out-Null
    }
    if (Test-Path $VhdxPath) {
        Dismount-VHD -Path $VhdxPath -ErrorAction SilentlyContinue
        Remove-Item $VhdxPath -Force
    }
    New-VHD -Path $VhdxPath -SizeBytes $VhdxSizeBytes -Dynamic | Out-Null
    $diskNumber = (Mount-VHD -Path $VhdxPath -Passthru | Get-Disk).Number
    Initialize-Disk -Number $diskNumber -PartitionStyle GPT | Out-Null
}
else {
    $disk = Get-Disk -Number $UsbDiskNumber
    if ($disk.BusType -ne 'USB') {
        throw "Диск $UsbDiskNumber не съёмный (шина $($disk.BusType)). Отказ."
    }
    if ($disk.FriendlyName.Trim() -ne $ConfirmModel.Trim()) {
        throw "Модель не совпала: на диске '$($disk.FriendlyName)', введено '$ConfirmModel'. Отказ."
    }
    Write-Host ("СТИРАЕМ диск {0}: {1}, {2:N1} ГБ" -f $disk.Number, $disk.FriendlyName, ($disk.Size / 1GB)) -ForegroundColor Yellow
    Clear-Disk -Number $disk.Number -RemoveData -RemoveOEM -Confirm:$false
    try { Initialize-Disk -Number $disk.Number -PartitionStyle GPT -ErrorAction Stop | Out-Null } catch { }
    $diskNumber = $disk.Number
}

try {
    # Initialize-Disk на GPT заводит служебный раздел MSR. Носителю он не нужен.
    Get-Partition -DiskNumber $diskNumber -ErrorAction SilentlyContinue |
        Where-Object { $_.Type -eq 'Reserved' } |
        Remove-Partition -Confirm:$false

    # ---------- раздел 1: загрузочный ----------
    # Создаётся обычным, чтобы получить букву и дать себя наполнить.
    # Тип «системный EFI» ставится в самом конце: после него буква пропадает.
    $bootPart = New-Partition -DiskNumber $diskNumber -Size $BootPartitionBytes -GptType $TypeData -AssignDriveLetter
    $bootNumber = $bootPart.PartitionNumber
    Format-Volume -Partition (Get-Partition -DiskNumber $diskNumber -PartitionNumber $bootNumber) `
        -FileSystem FAT32 -NewFileSystemLabel 'PEACEBOOT' -Confirm:$false | Out-Null
    $bootLetter = (Get-Partition -DiskNumber $diskNumber -PartitionNumber $bootNumber).DriveLetter
    $bootRoot = "${bootLetter}:\"

    # ---------- раздел 2: данные ----------
    $dataPart = New-Partition -DiskNumber $diskNumber -UseMaximumSize -GptType $TypeData -AssignDriveLetter
    $dataNumber = $dataPart.PartitionNumber
    Format-Volume -Partition (Get-Partition -DiskNumber $diskNumber -PartitionNumber $dataNumber) `
        -FileSystem NTFS -NewFileSystemLabel 'Windows Peace' -Confirm:$false | Out-Null
    $dataLetter = (Get-Partition -DiskNumber $diskNumber -PartitionNumber $dataNumber).DriveLetter
    $dataRoot = "${dataLetter}:\"

    Write-Host "Загрузочный раздел $bootRoot, раздел данных $dataRoot"

    # ---------- загрузочные файлы ----------
    foreach ($item in @('boot', 'efi', 'bootmgr', 'bootmgr.efi')) {
        $source = Join-Path $SourceRoot $item
        if (Test-Path $source) {
            Copy-Item $source -Destination $bootRoot -Recurse -Force
        }
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $bootRoot 'sources') | Out-Null
    Copy-Item (Join-Path $SourceRoot 'sources\boot.wim') (Join-Path $bootRoot 'sources\boot.wim') -Force

    # ---------- данные ----------
    $appTarget = Join-Path $dataRoot $PeaceMediaLayout.App
    $imagesTarget = Join-Path $dataRoot $PeaceMediaLayout.Images
    $recipesTarget = Join-Path $dataRoot $PeaceMediaLayout.Recipes

    New-Item -ItemType Directory -Force -Path $imagesTarget, $recipesTarget, $appTarget | Out-Null

    $installWimRelative = Join-Path $PeaceMediaLayout.Images 'install.wim'
    if (-not $SkipInstallWim) {
        Copy-Item $sourceInstallWim (Join-Path $dataRoot $installWimRelative) -Force
    }
    else {
        Write-Host 'install.wim пропущен: до шага В образ Windows не нужен.'
    }

    # Папка logs остаётся на хозяйской машине: журнал здешних запусков на носителе
    # выдаёт себя за журнал из WinPE, и разбор идёт по ложному следу.
    robocopy $AppFolder $appTarget /E /R:2 /W:2 /NFL /NDL /NP /XD $($PeaceMediaLayout.Logs) | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy приложения завершился с кодом $LASTEXITCODE" }

    if ($DiskDumpFolder) {
        robocopy $DiskDumpFolder (Join-Path $appTarget 'DiskDump') /E /R:2 /W:2 /NFL /NDL /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy DiskDump завершился с кодом $LASTEXITCODE" }
    }

    $recipeFileName = Split-Path $RecipeFile -Leaf
    $recipeRelative = Join-Path $PeaceMediaLayout.Recipes $recipeFileName
    Copy-Item $RecipeFile (Join-Path $dataRoot $recipeRelative) -Force

    # ---------- опись ----------
    $recipeId = [IO.Path]::GetFileNameWithoutExtension($recipeFileName) -replace '\.recipe$', ''
    $manifest = [ordered]@{
        schemaVersion = 1
        buildId       = [guid]::NewGuid().ToString()
        createdUtc    = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        tool          = [ordered]@{ name = 'tools/Media/Build-PeaceMedia.ps1'; version = '0.2.0' }
        recipes       = @(
            [ordered]@{
                id          = $recipeId
                name        = $recipeTitle
                description = $recipeDescription
                recipeFile  = $recipeRelative
                image       = [ordered]@{
                    file      = $installWimRelative
                    index     = $imageIndex
                    imageName = $ImageName
                }
            }
        )
    }
    $json = $manifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $dataRoot $PeaceMediaLayout.Manifest), $json, (New-Object Text.UTF8Encoding($false)))

    # ---------- загрузочный раздел прячется от человека ----------
    # Смены типа мало: букву Windows запомнила при разметке и сама её не отзывает.
    # Без явного снятия в проводнике появятся два диска вместо одного.
    Remove-PartitionAccessPath -DiskNumber $diskNumber -PartitionNumber $bootNumber -AccessPath "${bootLetter}:\"
    Set-Partition -DiskNumber $diskNumber -PartitionNumber $bootNumber -GptType $TypeEsp

    $check = Get-Partition -DiskNumber $diskNumber -PartitionNumber $bootNumber
    if ($check.DriveLetter) {
        throw "Загрузочный раздел остался под буквой $($check.DriveLetter): — в проводнике будет два диска."
    }

    Write-Host 'Носитель собран.' -ForegroundColor Green
}
finally {
    if ($usingVhdx) {
        Dismount-VHD -Path $VhdxPath -ErrorAction SilentlyContinue
    }
}
