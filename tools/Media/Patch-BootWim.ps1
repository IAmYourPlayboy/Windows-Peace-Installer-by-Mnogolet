<#
.SYNOPSIS
    Кладёт внутрь загрузочного образа подмену запуска: вместо установщика
    Windows стартует мастер Windows Peace.

.DESCRIPTION
    Внутрь образа кладётся только это — два маленьких файла и увеличенный
    оперативный диск. Само приложение живёт рядом с образом, на разделе данных:
    так его можно подменить, не пересобирая образ.

    Исходный образ у нас в единственном экземпляре: своего ISO на машине нет,
    а установочная флешка на шаге Б переразмечается. Поэтому перед первой
    правкой рядом кладётся нетронутая копия, и вернуться к ней можно ключом
    -Restore. Без этой копии одна неудачная правка стоила бы всего шага.

.EXAMPLE
    powershell -File tools\Media\Patch-BootWim.ps1

.EXAMPLE
    powershell -File tools\Media\Patch-BootWim.ps1 -Restore
    Вернуть образ в исходное состояние.
#>
[CmdletBinding()]
param(
    [string] $WimPath = 'D:\WindowsPeace-Source\sources\boot.wim',

    # Индекс 1 — чистый WinPE, индекс 2 — установщик Windows. Носитель грузится
    # во второй, его и правим.
    [int] $Index = 2,

    [string] $MountPath = 'D:\WindowsPeace-Stand\mount',

    # Сколько места отвести под оперативный диск X:. По умолчанию WinPE даёт
    # 32 МБ — этого мало всему, что пишет на диск само приложение.
    [uint32] $ScratchSpaceMb = 512,

    [switch] $Restore
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceMedia.psm1') -Force

Assert-PeaceAdmin

if (-not (Test-Path $WimPath)) {
    throw "Образа нет: '$WimPath'. Сначала запусти Save-InstallSource.ps1."
}

$pristine = "$WimPath.pristine"

if ($Restore) {
    if (-not (Test-Path $pristine)) {
        throw "Нетронутой копии нет: '$pristine'. Возвращать нечего."
    }

    Copy-Item $pristine $WimPath -Force
    Write-Host "Образ возвращён в исходное состояние из $pristine." -ForegroundColor Green
    return
}

if (-not (Test-Path $pristine)) {
    Write-Host "Кладу нетронутую копию образа: $pristine"
    Copy-Item $WimPath $pristine -Force
}

# Каталог монтирования должен быть пуст: DISM отказывается монтировать в папку,
# где что-то лежит, и говорит об этом невнятно.
if (Test-Path $MountPath) {
    if (@(Get-ChildItem -LiteralPath $MountPath -Force).Count -gt 0) {
        throw "Каталог '$MountPath' не пуст. Если там остался смонтированный образ: dism /Unmount-Wim /MountDir:$MountPath /Discard"
    }
}
else {
    New-Item -ItemType Directory -Force -Path $MountPath | Out-Null
}

Write-Host "Монтирую индекс $Index из $WimPath ..."
dism /English /Mount-Wim /WimFile:$WimPath /Index:$Index /MountDir:$MountPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Mount-Wim вернул $LASTEXITCODE" }

$committed = $false
try {
    $system32 = Join-Path $MountPath 'Windows\System32'

    foreach ($name in @('winpeshl.ini', 'peace-launch.cmd')) {
        Copy-Item (Join-Path $PSScriptRoot $name) (Join-Path $system32 $name) -Force
        Write-Host "Положено в образ: $name"
    }

    dism /English /Image:$MountPath /Set-ScratchSpace:$ScratchSpaceMb | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Set-ScratchSpace вернул $LASTEXITCODE" }

    $committed = $true
}
finally {
    $mode = if ($committed) { '/Commit' } else { '/Discard' }
    Write-Host "Размонтирую с ключом $mode ..."
    dism /English /Unmount-Wim /MountDir:$MountPath $mode | Out-Null

    if ($LASTEXITCODE -ne 0) {
        # Оставленный смонтированным образ ломает и следующую правку, и сборку
        # носителя, а само сообщение DISM об этом не говорит.
        Write-Warning "Unmount-Wim вернул $LASTEXITCODE. Образ мог остаться смонтированным: dism /Cleanup-Wim"
    }
}

Write-Host "Образ правлен: мастер запускается сам, оперативный диск $ScratchSpaceMb МБ." -ForegroundColor Green
