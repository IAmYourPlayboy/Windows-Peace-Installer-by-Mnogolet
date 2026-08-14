<#
.SYNOPSIS
    Создаёт виртуалку стенда для проверки мастера в WinPE.

.DESCRIPTION
    Второе поколение — потому что нам нужна прошивка UEFI, а не BIOS.
    Четыре гигабайта памяти — потому что WinPE загружает весь boot.wim
    в оперативную память целиком: 680 МБ образа, полгигабайта оперативного
    диска и среда выполнения с окном сверху.

    Безопасная загрузка включена намеренно: на настоящей машине она включена,
    и проверка с выключенной была бы мягче действительности.

    Сеть отключена: на шаге Б она не нужна, а лишнее устройство — лишние
    секунды на загрузке.

.EXAMPLE
    powershell -File tools/Media/New-PeaceVm.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $VhdxPath,
    [uint64] $MemoryBytes = 4GB
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Нужны права администратора: Hyper-V без них не отвечает.'
}

if (-not (Test-Path $VhdxPath)) {
    throw "Виртуального диска '$VhdxPath' нет. Сначала собери носитель через Build-PeaceMedia.ps1."
}

if (Get-VM -Name $Name -ErrorAction SilentlyContinue) {
    Write-Host "Прежняя виртуалка '$Name' удаляется."
    Stop-VM -Name $Name -TurnOff -Force -ErrorAction SilentlyContinue
    Remove-VM -Name $Name -Force
}

New-VM -Name $Name -Generation 2 -MemoryStartupBytes $MemoryBytes -VHDPath $VhdxPath | Out-Null
Set-VMProcessor -VMName $Name -Count 2
Set-VMMemory -VMName $Name -DynamicMemoryEnabled $false
Get-VMNetworkAdapter -VMName $Name | Remove-VMNetworkAdapter -ErrorAction SilentlyContinue
Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate 'MicrosoftWindows'
Set-VMFirmware -VMName $Name -FirstBootDevice (Get-VMHardDiskDrive -VMName $Name)

Write-Host ("Виртуалка '{0}' создана: 2 ядра, {1:N0} ГБ, безопасная загрузка включена." -f $Name, ($MemoryBytes / 1GB)) -ForegroundColor Green
