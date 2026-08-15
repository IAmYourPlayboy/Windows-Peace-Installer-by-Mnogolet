<#
.SYNOPSIS
    Разговор с окном мастера: выбрать строку, нажать кнопку, вписать текст.

.DESCRIPTION
    Тесты проверяют модели экранов, но привязку разметки к модели они не ловят:
    опечатка в имени свойства оставляет поле пустым, а все тесты при этом зелёные.
    Ловится это только разговором с настоящим окном.

    Окно опрашивается средствами доступности — теми же, какими его читает
    экранный диктор. То есть ровно так, как окно увидит человек, который
    не пользуется мышью: если что-то нельзя нащупать отсюда, ему тоже нельзя.

    Элементы ищутся не по виду, а по тому, что они умеют: «выбирается»,
    «нажимается», «принимает текст». Вид меняется от разметки — умение нет.
    Список с колонками, например, называет себя таблицей, а простой список —
    списком, и поиск по виду сломался бы на первой же правке разметки.
#>

Set-StrictMode -Version Latest
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$script:Automation = [System.Windows.Automation.AutomationElement]
$script:Descendants = [System.Windows.Automation.TreeScope]::Descendants

function Get-PeaceWindowElement {
    <#
    .SYNOPSIS
        Окно приложения глазами средств доступности.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [IntPtr] $WindowHandle
    )

    $element = $script:Automation::FromHandle($WindowHandle)
    if (-not $element) { throw 'Окно не отвечает средствам доступности.' }
    $element
}

function Get-PeaceElements {
    <#
    .SYNOPSIS
        Все элементы окна, умеющие то, что просят.

    .PARAMETER Ability
        Умение: Selectable — выбирается, Invokable — нажимается,
        Editable — принимает текст.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root,
        [Parameter(Mandatory = $true)] [ValidateSet('Selectable', 'Invokable', 'Editable')] [string] $Ability,
        [switch] $IncludeDisabled
    )

    $property = switch ($Ability) {
        'Selectable' { $script:Automation::IsSelectionItemPatternAvailableProperty }
        'Invokable' { $script:Automation::IsInvokePatternAvailableProperty }
        'Editable' { $script:Automation::IsValuePatternAvailableProperty }
    }

    $condition = New-Object System.Windows.Automation.PropertyCondition($property, $true)
    $found = $Root.FindAll($script:Descendants, $condition)

    $result = @()
    foreach ($element in $found) {
        if ($IncludeDisabled -or $element.Current.IsEnabled) { $result += $element }
    }

    # Запятая обязательна: без неё пустой список превращается в $null,
    # а список из одного элемента — в сам элемент.
    , $result
}

function Get-PeaceButton {
    <#
    .SYNOPSIS
        Кнопка по надписи. Отсутствие кнопки — не отказ, а ответ: $null.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    foreach ($element in (Get-PeaceElements -Root $Root -Ability Invokable -IncludeDisabled)) {
        if ($element.Current.Name -eq $Name) { return $element }
    }

    $null
}

function Wait-PeaceUiaCondition {
    <#
    .SYNOPSIS
        Ждать, пока окно не ответит на вопрос тем, чего ждут.

    .DESCRIPTION
        Возвращает то, что вернул блок, или $null, если не дождались.
        Не дождаться — обычный исход: он значит, что привязка не сработала,
        и говорить об этом надо словами, а не отказом посреди круга.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [scriptblock] $Probe,
        [double] $TimeoutSeconds = 20,
        [double] $PollSeconds = 0.3
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $answer = & $Probe
        if ($answer) { return $answer }
        Start-Sleep -Seconds $PollSeconds
    }

    $null
}

function Select-PeaceFirstItem {
    <#
    .SYNOPSIS
        Выбрать первую доступную строку списка — так же, как это сделал бы человек.

    .DESCRIPTION
        Недоступные строки пропускаются: в списке дисков они есть намеренно —
        загрузочный носитель и диск текущей системы видны, но выбрать их нельзя.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root,
        [double] $TimeoutSeconds = 20
    )

    $item = Wait-PeaceUiaCondition -TimeoutSeconds $TimeoutSeconds -Probe {
        $items = Get-PeaceElements -Root $Root -Ability Selectable
        if ($items.Count -gt 0) { $items[0] } else { $null }
    }

    if (-not $item) { throw "За $TimeoutSeconds с в списке не появилось ни одной доступной строки." }

    $pattern = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    $item.Current.Name
}

function Invoke-PeaceButton {
    <#
    .SYNOPSIS
        Нажать кнопку, дождавшись, пока она оживёт.

    .DESCRIPTION
        Ожидание здесь — половина проверки: кнопка оживает тогда и только тогда,
        когда модель экрана приняла выбор и сказала об этом оболочке.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root,
        [Parameter(Mandatory = $true)] [string] $Name,
        [double] $TimeoutSeconds = 20
    )

    $button = Wait-PeaceUiaCondition -TimeoutSeconds $TimeoutSeconds -Probe {
        $candidate = Get-PeaceButton -Root $Root -Name $Name
        if ($candidate -and $candidate.Current.IsEnabled) { $candidate } else { $null }
    }

    if (-not $button) {
        throw "Кнопка «$Name» не ожила за $TimeoutSeconds с. Выбор до модели экрана не дошёл."
    }

    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Set-PeaceText {
    <#
    .SYNOPSIS
        Вписать текст в первое поле ввода окна.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root,
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Text,
        [double] $TimeoutSeconds = 20
    )

    $field = Wait-PeaceUiaCondition -TimeoutSeconds $TimeoutSeconds -Probe {
        $fields = Get-PeaceElements -Root $Root -Ability Editable
        if ($fields.Count -gt 0) { $fields[0] } else { $null }
    }

    if (-not $field) { throw "За $TimeoutSeconds с на экране не нашлось поля ввода." }

    $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
}

function Get-PeaceScreenText {
    <#
    .SYNOPSIS
        Всё, что написано на экране, сверху вниз.

    .DESCRIPTION
        Пустая строка там, где должно стоять значение, — самый частый след
        опечатки в привязке: тесты при этом зелёные, а человек читает пустоту.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [System.Windows.Automation.AutomationElement] $Root
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        $script:Automation::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)

    $lines = @()
    foreach ($element in $Root.FindAll($script:Descendants, $condition)) {
        $text = $element.Current.Name
        if (-not [string]::IsNullOrWhiteSpace($text)) { $lines += $text }
    }

    , $lines
}

Export-ModuleMember -Function Get-PeaceWindowElement, Get-PeaceElements, Get-PeaceButton,
    Wait-PeaceUiaCondition, Select-PeaceFirstItem, Invoke-PeaceButton, Set-PeaceText, Get-PeaceScreenText
