<#
.SYNOPSIS
    Вытаскивает драйверы Microsoft из install.wim в отдельную папку, чтобы
    вложить их потом в загрузочный образ (Patch-BootWim.ps1).

.DESCRIPTION
    WinPE (boot.wim) несёт лишь часть драйверов Windows. Полный набор — шины I2C
    для тачпадов, контроллеры дисков, сеть — лежит в install.wim на том же самом
    установочном носителе. Незачем клянчить драйверы у человека или качать
    со стороны: они уже здесь, подписанные Microsoft, — надо только переложить
    их из образа системы в образ загрузки.

    Скрипт монтирует install.wim только для чтения, экспортирует все сторонние
    драйверы (Export-WindowsDriver) в папку и размонтирует. Дальше эту папку
    забирает Patch-BootWim.ps1 ключом -DriversPath.

    Честная граница: набор в install.wim — это то, что Microsoft кладёт в коробку.
    Драйверов шины I2C для новых поколений Intel (Tiger Lake / Alder Lake и новее)
    там нет — они приезжают с Windows Update уже после установки, поэтому на таких
    ноутбуках тачпад не оживёт и отсюда. AMD и старые Intel — заводятся.

.EXAMPLE
    powershell -File tools\Media\Harvest-MediaDrivers.ps1
#>
[CmdletBinding()]
param(
    [string] $WimPath = 'D:\WindowsPeace-Source\sources\install.wim',

    # Издание, из которого берём драйверы. 2 — Windows 11 Pro (её и ставим).
    # Набор драйверов у изданий один; берём Pro ради определённости.
    [int] $Index = 2,

    [string] $Destination = 'D:\WindowsPeace-Drivers',

    [string] $MountPath = 'D:\WindowsPeace-Stand\install-mount'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceMedia.psm1') -Force

Assert-PeaceAdmin

if (-not (Test-Path $WimPath)) {
    throw "Образа системы нет: '$WimPath'. Сначала запусти Save-InstallSource.ps1."
}

# Каталог монтирования должен быть пуст: DISM отказывается монтировать в занятую
# папку и говорит об этом невнятно.
if ((Test-Path $MountPath) -and @(Get-ChildItem -LiteralPath $MountPath -Force).Count -gt 0) {
    throw "Каталог '$MountPath' не пуст. Если там остался смонтированный образ: dism /Unmount-Wim /MountDir:$MountPath /Discard"
}
New-Item -ItemType Directory -Force -Path $MountPath | Out-Null
New-Item -ItemType Directory -Force -Path $Destination | Out-Null

Write-Host "Монтирую индекс $Index из $WimPath (только чтение) ..."
dism /English /Mount-Wim /WimFile:$WimPath /Index:$Index /MountDir:$MountPath /ReadOnly | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Mount-Wim вернул $LASTEXITCODE" }

try {
    $repo = Join-Path $MountPath 'Windows\System32\DriverStore\FileRepository'
    if (-not (Test-Path $repo)) { throw "В образе нет склада драйверов: '$repo'." }

    # robocopy, а не Copy-Item: пакетов сотни, пути длинные — Copy-Item на них
    # спотыкается, robocopy держит. Коды возврата 0..7 — успех, 8 и выше — беда.
    Write-Host "Копирую склад драйверов Microsoft в $Destination ..."
    robocopy $repo $Destination /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy вернул $LASTEXITCODE" }

    $infs = @(Get-ChildItem $Destination -Recurse -Filter *.inf -ErrorAction SilentlyContinue)
    $sizeMb = [math]::Round((Get-ChildItem $Destination -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Host "Вытащено: inf-файлов $($infs.Count), размер $sizeMb МБ. Набор теперь наш." -ForegroundColor Green
}
finally {
    Write-Host 'Размонтирую install.wim ...'
    dism /English /Unmount-Wim /MountDir:$MountPath /Discard | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Unmount-Wim вернул $LASTEXITCODE. Образ мог остаться смонтированным: dism /Cleanup-Wim"
    }
}
