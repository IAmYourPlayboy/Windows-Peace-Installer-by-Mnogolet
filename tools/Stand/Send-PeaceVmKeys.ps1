<#
.SYNOPSIS
    Передаёт нажатия клавиш в виртуалку Hyper-V, не открывая её окно.

.DESCRIPTION
    Нужно, чтобы круг проверки оставался двухминутным и не требовал человека
    у клавиатуры. Hyper-V отдаёт виртуальную клавиатуру гостя объектом
    Msvm_Keyboard, и через него можно нажимать клавиши так же, как руками.

    Коды клавиш — обычные виртуальные коды Windows.

.EXAMPLE
    powershell -File tools/Stand/Send-PeaceVmKeys.ps1 -ShiftF10

.EXAMPLE
    & .\tools\Stand\Send-PeaceVmKeys.ps1 -Send 'diskpart','{Enter}'
    Набирает и нажимает. В фигурных скобках — имя клавиши, всё остальное
    набирается как текст.

    Вызывать надо через «&», а не через «powershell -File»: последний передаёт
    массив одной строкой. Скрипт такое узнаёт и говорит прямо.

.EXAMPLE
    & .\tools\Stand\Send-PeaceVmKeys.ps1 -Send '{Tab}','{Down}','{Space}'
    Ходит по окну клавишами: мыши у стенда нет, и это единственный способ
    нажать что-нибудь в окне гостя.
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',

    # Клавиши и текст вперемешку, по порядку. «{Tab}» — клавиша, «diskpart» — текст.
    # Одним ключом на то и другое: два способа набрать текст разошлись бы.
    [string[]] $Send = @(),

    # Shift+F10 отдельно: это одновременное нажатие двух клавиш, а не последовательность.
    [switch] $ShiftF10,

    [int] $DelayMs = 400
)

$ErrorActionPreference = 'Stop'

# Обычные виртуальные коды Windows. Одной таблицей, а не россыпью переменных:
# добавить клавишу должно быть можно строкой, не трогая ничего вокруг.
$VirtualKeys = [ordered]@{
    Backspace = 0x08; Tab = 0x09; Enter = 0x0D; Shift = 0x10; Ctrl = 0x11; Alt = 0x12
    Escape = 0x1B; Space = 0x20; PageUp = 0x21; PageDown = 0x22; End = 0x23; Home = 0x24
    Left = 0x25; Up = 0x26; Right = 0x27; Down = 0x28; Insert = 0x2D; Delete = 0x2E
    F1 = 0x70; F2 = 0x71; F3 = 0x72; F4 = 0x73; F5 = 0x74; F6 = 0x75
    F7 = 0x76; F8 = 0x77; F9 = 0x78; F10 = 0x79; F11 = 0x7A; F12 = 0x7B
}

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

function Send-Key {
    param([string] $KeyName)

    if (-not $VirtualKeys.Contains($KeyName)) {
        throw "Неизвестная клавиша «$KeyName». Известны: $($VirtualKeys.Keys -join ', ')."
    }

    Invoke-Keyboard -Method 'TypeKey' -Arguments @{ keyCode = [uint32]$VirtualKeys[$KeyName] }
}

function Send-Item {
    <#
        Одно звено последовательности: «{Tab}» — клавиша, всё прочее — текст.
    #>
    param([string] $Item)

    # Скобки внутри скобок означают одно: массив дошёл сюда одной строкой.
    # Это уже случалось и выглядело как выдуманное имя клавиши.
    if ($Item -match '\}.*\{') {
        throw "Клавиши пришли одной строкой: «$Item». Вызывай через «&», а не «powershell -File»: последний склеивает массив."
    }

    if ($Item -match '^\{([^{}]+)\}$') {
        Send-Key -KeyName $Matches[1]
        Write-Host "Клавиша: $Item"
        return
    }

    Invoke-Keyboard -Method 'TypeText' -Arguments @{ asciiText = $Item }
    Write-Host "Набрано: $Item"
}

if ($ShiftF10) {
    Invoke-Keyboard -Method 'PressKey'   -Arguments @{ keyCode = [uint32]$VirtualKeys['Shift'] }
    Send-Key -KeyName 'F10'
    Invoke-Keyboard -Method 'ReleaseKey' -Arguments @{ keyCode = [uint32]$VirtualKeys['Shift'] }

    Write-Host 'Shift+F10 отправлен.'
    Start-Sleep -Milliseconds $DelayMs
}

foreach ($item in $Send) {
    Send-Item -Item $item
    Start-Sleep -Milliseconds $DelayMs
}
