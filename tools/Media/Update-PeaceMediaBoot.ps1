<#
.SYNOPSIS
    Обновляет boot.wim на настоящей флешке, не пересобирая носитель целиком.

.DESCRIPTION
    boot.wim лежит на загрузочном разделе (FAT32, тип «системный EFI», без буквы).
    Полная пересборка ради одного образа перекладывает и install.wim в девять
    гигабайт — лишнее, когда поменялся только boot.wim (правка запуска, драйверы).

    Носитель опознаётся дважды, чтобы не задеть чужой диск: шина обязана быть USB,
    модель — совпасть с -ConfirmModel, а на разделе данных должна лежать опись
    Windows Peace (тем же признаком носитель находит и сам мастер). Только после
    этого загрузочному разделу временно даётся буква, образ перезаписывается,
    кэш сбрасывается на флешку и буква снимается — чтобы в проводнике снова был
    один диск.

.EXAMPLE
    powershell -File tools\Media\Update-PeaceMediaBoot.ps1 -DiskNumber 2 -ConfirmModel "VendorC ProductCode"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [int] $DiskNumber,
    [Parameter(Mandatory = $true)] [string] $ConfirmModel,
    [string] $BootWim = 'D:\WindowsPeace-Source\sources\boot.wim'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceMedia.psm1') -Force

# Тот же идентификатор «системного EFI», каким его метит сборка носителя.
$TypeEsp = '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'

Assert-PeaceAdmin

if (-not (Test-Path $BootWim)) {
    throw "Образа нет: '$BootWim'. Сначала собери его: tools\Media\Patch-BootWim.ps1"
}

$disk = Get-Disk -Number $DiskNumber
if ($disk.BusType -ne 'USB') {
    throw "Диск $DiskNumber не съёмный (шина $($disk.BusType)). Отказ: на всякий чужой диск boot.wim не пишем."
}
if ($disk.FriendlyName.Trim() -ne $ConfirmModel.Trim()) {
    throw "Модель не совпала: на диске '$($disk.FriendlyName)', введено '$ConfirmModel'. Отказ."
}

# Опись на разделе данных — третья проверка, что это именно носитель Windows Peace,
# а не другая USB-флешка той же модели.
$dataRoot = Get-PeaceMediaDataRoot -DiskNumber $DiskNumber
Write-Host "Носитель опознан: диск $DiskNumber, $($disk.FriendlyName), опись в $dataRoot." -ForegroundColor Green

# Загрузочный раздел — тот, что помечен «системным EFI». Он без буквы.
$bootPart = Get-Partition -DiskNumber $DiskNumber | Where-Object { $_.GptType -eq $TypeEsp }
if (-not $bootPart) {
    throw "На диске $DiskNumber нет загрузочного раздела (тип «системный EFI»). Носитель собран не так?"
}
if ($bootPart -is [array]) {
    throw "На диске $DiskNumber несколько разделов «системный EFI» — не понимаю, в какой писать. Разбор вручную."
}

$sizeMb = [math]::Round((Get-Item $BootWim).Length / 1MB, 0)
Write-Host "Даю загрузочному разделу временную букву и кладу boot.wim ($sizeMb МБ)..."

Add-PartitionAccessPath -DiskNumber $DiskNumber -PartitionNumber $bootPart.PartitionNumber -AssignDriveLetter
$letter = (Get-Partition -DiskNumber $DiskNumber -PartitionNumber $bootPart.PartitionNumber).DriveLetter
if (-not $letter) { throw 'Не удалось дать загрузочному разделу букву.' }

try {
    $dest = "${letter}:\sources\boot.wim"
    if (-not (Test-Path "${letter}:\sources")) {
        throw "На загрузочном разделе нет папки sources — это точно носитель Windows Peace?"
    }

    # Старый образ убираем до записи: на FAT32 в 2 ГБ старый и новый разом
    # могут не поместиться.
    Remove-Item $dest -Force -ErrorAction SilentlyContinue
    Copy-Item $BootWim $dest -Force

    # Сброс кэша на саму флешку. Иначе несброшенное пропадёт при извлечении —
    # эти грабли в проекте уже наступали (WinPE обесточивается, кэш гибнет).
    Write-VolumeCache -DriveLetter $letter
    Write-Host "boot.wim на флешке обновлён: $dest" -ForegroundColor Green
}
finally {
    # Букву снять обязательно, иначе в проводнике появится второй диск.
    Remove-PartitionAccessPath -DiskNumber $DiskNumber -PartitionNumber $bootPart.PartitionNumber `
        -AccessPath "${letter}:\" -ErrorAction SilentlyContinue
}

Write-Host 'Готово. Можно извлекать флешку и грузиться.' -ForegroundColor Green
