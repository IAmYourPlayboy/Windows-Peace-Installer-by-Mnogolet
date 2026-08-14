<#
.SYNOPSIS
    Передаёт нажатия клавиш в виртуалку Hyper-V, не открывая её окно.

.DESCRIPTION
    Нужно, чтобы круг проверки оставался двухминутным и не требовал человека
    у клавиатуры. Hyper-V отдаёт виртуальную клавиатуру гостя объектом
    Msvm_Keyboard, и через него можно нажимать клавиши так же, как руками.

    Коды клавиш — обычные виртуальные коды Windows.

.EXAMPLE
    powershell -File tools/Media/Send-PeaceVmKeys.ps1 -ShiftF10

.EXAMPLE
    powershell -File tools/Media/Send-PeaceVmKeys.ps1 -Text 'diskpart' -Enter
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [string] $Text,
    [switch] $Enter,
    [switch] $ShiftF10,
    [int] $DelayMs = 400
)

$ErrorActionPreference = 'Stop'

$VkShift  = 0x10
$VkReturn = 0x0D
$VkF10    = 0x79

$namespace = 'root\virtualization\v2'
$vm = Get-CimInstance -Namespace $namespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
if (-not $vm) { throw "Виртуалка '$Name' не найдена." }

$keyboard = Get-CimAssociatedInstance $vm -ResultClassName Msvm_Keyboard
if (-not $keyboard) { throw "У виртуалки '$Name' нет виртуальной клавиатуры." }

function Invoke-Keyboard {
    param([string] $Method, [hashtable] $Arguments)

    $result = Invoke-CimMethod -InputObject $keyboard -MethodName $Method -Arguments $Arguments
    if ($result.ReturnValue -ne 0) {
        throw "$Method вернул $($result.ReturnValue)."
    }
}

if ($ShiftF10) {
    Invoke-Keyboard -Method 'PressKey'   -Arguments @{ keyCode = [uint32]$VkShift }
    Invoke-Keyboard -Method 'TypeKey'    -Arguments @{ keyCode = [uint32]$VkF10 }
    Invoke-Keyboard -Method 'ReleaseKey' -Arguments @{ keyCode = [uint32]$VkShift }
    Write-Host 'Shift+F10 отправлен.'
    Start-Sleep -Milliseconds $DelayMs
}

if ($Text) {
    Invoke-Keyboard -Method 'TypeText' -Arguments @{ asciiText = $Text }
    Write-Host "Набрано: $Text"
    Start-Sleep -Milliseconds $DelayMs
}

if ($Enter) {
    Invoke-Keyboard -Method 'TypeKey' -Arguments @{ keyCode = [uint32]$VkReturn }
    Write-Host 'Enter отправлен.'
}
