<#
.SYNOPSIS
    Спасает содержимое установочного носителя Windows на диск.

.DESCRIPTION
    Запускается один раз, до любой разметки. Единственный экземпляр install.wim
    лежит на флешке, которую шаг Б переразметит, а своего ISO на машине нет.
    Пока копия не сделана, ни одна команда разметки запускаться не должна.

.EXAMPLE
    powershell -File tools/Media/Save-InstallSource.ps1 -SourceRoot E:\
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $SourceRoot,
    [string] $Destination = 'D:\WindowsPeace-Source'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $SourceRoot 'sources\boot.wim'))) {
    throw "В '$SourceRoot' нет sources\boot.wim — это не установочный носитель Windows."
}

$destinationDrive = $Destination.Substring(0, 1)
$free = (Get-PSDrive -Name $destinationDrive).Free
$need = (Get-ChildItem $SourceRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
         Measure-Object -Property Length -Sum).Sum

Write-Host ("Копируем {0:N1} ГБ, свободно {1:N1} ГБ" -f ($need / 1GB), ($free / 1GB))
if ($free -lt $need * 1.1) { throw 'Мало места на диске назначения.' }

robocopy $SourceRoot $Destination /MIR /R:2 /W:2 /NFL /NDL /NP /XD 'System Volume Information' '$RECYCLE.BIN'
if ($LASTEXITCODE -ge 8) { throw "robocopy завершился с кодом $LASTEXITCODE" }

$bootWim = Join-Path $Destination 'sources\boot.wim'
$installWim = Join-Path $Destination 'sources\install.wim'
foreach ($file in @($bootWim, $installWim)) {
    if (-not (Test-Path $file)) { throw "После копирования не найден '$file'" }
    Write-Host ("{0}  {1:N2} ГБ" -f $file, ((Get-Item $file).Length / 1GB))
}

Write-Host 'Исходный материал спасён.' -ForegroundColor Green
