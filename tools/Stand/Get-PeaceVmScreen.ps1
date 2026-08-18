<#
.SYNOPSIS
    Снимает экран гостя Hyper-V в PNG, не заходя в виртуалку.

.DESCRIPTION
    В WinPE нет ни PowerShell, ни буфера обмена, ни средств снятия экрана.
    Единственный способ увидеть, что там нарисовалось, — попросить картинку
    у самого Hyper-V.

    Это то, ради чего на шаге Б появился стенд: круг проверки не требует,
    чтобы автор перезагружал свою машину и смотрел на экран сам.

.EXAMPLE
    powershell -File tools/Stand/Get-PeaceVmScreen.ps1 -OutPath D:\WindowsPeace-Stand\screen.png
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $OutPath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PeaceFrames.psm1') -Force

$frame = Get-PeaceVmFrame -Name $Name
Save-PeaceFrame -Frame $frame -Path $OutPath

Write-Host "Экран снят: $OutPath ($($frame.Width)×$($frame.Height))"
