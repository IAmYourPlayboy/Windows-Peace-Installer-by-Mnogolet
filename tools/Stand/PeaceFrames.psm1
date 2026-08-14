<#
.SYNOPSIS
    Кадры экрана: снять, сравнить, дождаться, пока картинка перестанет меняться.

.DESCRIPTION
    Проверять приходится в двух местах, и оба раза одинаково: снять картинку
    и посмотреть, что на ней. В виртуалке картинку отдаёт сам Hyper-V,
    на обычной Windows — окно приложения через PrintWindow.

    Общее у них — ожидание. Слепое «подожди пятьдесят секунд» плохо дважды:
    если машина в тот день медленнее, круг разваливается, а если быстрее —
    мы зря стоим. Здесь вместо этого ждут, пока картинка перестанет меняться:
    это верный признак того, что загрузка закончилась и ждут человека.

    Сравнение с допуском, а не побайтовое: в командной строке мигает курсор,
    и точное сравнение не совпало бы никогда.
#>

Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

# Сравнение полутора миллионов байт средствами PowerShell занимает секунды.
# Тот же цикл на C# — единицы миллисекунд, а вызывается он в ожидании много раз.
Add-Type -Namespace WindowsPeace.Stand -Name FrameMath -Language CSharp -MemberDefinition @'
/// <summary>Какая доля байт различается. Ноль — картинки совпали.</summary>
public static double DifferenceFraction(byte[] first, byte[] second)
{
    if (first == null || second == null) { return 1.0; }
    if (first.Length != second.Length) { return 1.0; }
    if (first.Length == 0) { return 0.0; }

    long differing = 0;
    for (int i = 0; i < first.Length; i++)
    {
        if (first[i] != second[i]) { differing++; }
    }

    return (double)differing / first.Length;
}

/// <summary>Какая доля байт отличается от первого. Ноль — картинка однотонная.</summary>
public static double NonUniformFraction(byte[] frame)
{
    if (frame == null || frame.Length == 0) { return 0.0; }

    byte background = frame[0];
    long different = 0;
    for (int i = 0; i < frame.Length; i++)
    {
        if (frame[i] != background) { different++; }
    }

    return (double)different / frame.Length;
}
'@

Add-Type -Namespace WindowsPeace.Stand -Name NativeWindow -Language CSharp -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PrintWindow(System.IntPtr window, System.IntPtr deviceContext, uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool ShowWindow(System.IntPtr window, int command);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetWindowRect(System.IntPtr window, out Rect rectangle);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool IsWindowVisible(System.IntPtr window);

public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

/// <summary>Отрисовать всё содержимое окна, включая то, что рисует не GDI.</summary>
public const uint RenderFullContent = 2;

/// <summary>Показать окно, не забирая у человека фокус.</summary>
public const int ShowNoActivate = 4;
'@

function New-PeaceFrame {
    <#
    .SYNOPSIS
        Кадр: точки, их размер и формат. Всё, что нужно, чтобы сравнить и сохранить.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [int] $Width,
        [Parameter(Mandatory = $true)] [int] $Height,
        [Parameter(Mandatory = $true)] [byte[]] $Bytes,
        [Parameter(Mandatory = $true)] [System.Drawing.Imaging.PixelFormat] $PixelFormat,
        [Parameter(Mandatory = $true)] [int] $BytesPerPixel
    )

    [pscustomobject]@{
        Width         = $Width
        Height        = $Height
        Bytes         = $Bytes
        PixelFormat   = $PixelFormat
        BytesPerPixel = $BytesPerPixel
    }
}

function Get-PeaceVmFrame {
    <#
    .SYNOPSIS
        Кадр экрана гостя Hyper-V. В WinPE снять экран изнутри нечем.
    #>
    [CmdletBinding()]
    param(
        [string] $Name = 'Windows Peace Stand'
    )

    $namespace = 'root\virtualization\v2'

    $vm = Get-CimInstance -Namespace $namespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
    if (-not $vm) { throw "Виртуалка '$Name' не найдена." }

    $settings = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VirtualSystemSettingData |
        Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' }
    if (-not $settings) { throw "У виртуалки '$Name' не нашлось действующих настроек." }

    $head = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VideoHead
    if (-not $head) { throw "Виртуалка '$Name' не отдаёт видеовыход — она выключена." }

    $width = [int][uint16]$head.CurrentHorizontalResolution
    $height = [int][uint16]$head.CurrentVerticalResolution
    if ($width -le 0 -or $height -le 0) {
        throw "Виртуалка '$Name' не отдаёт разрешение экрана — она ещё не дошла до вывода картинки."
    }

    $service = Get-CimInstance -Namespace $namespace -ClassName Msvm_VirtualSystemManagementService
    $result = Invoke-CimMethod -InputObject $service -MethodName GetVirtualSystemThumbnailImage -Arguments @{
        TargetSystem = [CimInstance]$settings
        WidthPixels  = [uint16]$width
        HeightPixels = [uint16]$height
    }
    if ($result.ReturnValue -ne 0) {
        throw "GetVirtualSystemThumbnailImage вернул $($result.ReturnValue). Запасной путь — окно vmconnect и PrintWindow."
    }

    # Hyper-V отдаёт RGB565: два байта на точку, без заголовка и без выравнивания строк.
    $bytes = [byte[]]$result.ImageData
    $expected = $width * $height * 2
    if ($bytes.Length -lt $expected) {
        throw "Hyper-V отдал $($bytes.Length) байт вместо $expected на ${width}×${height}."
    }

    New-PeaceFrame -Width $width -Height $height -Bytes $bytes `
        -PixelFormat ([System.Drawing.Imaging.PixelFormat]::Format16bppRgb565) -BytesPerPixel 2
}

function Get-PeaceWindowFrame {
    <#
    .SYNOPSIS
        Кадр окна на обычной Windows. Приложение не установлено в системе,
        и общие средства управления экраном его не видят; PrintWindow видит.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [IntPtr] $WindowHandle
    )

    $rectangle = New-Object WindowsPeace.Stand.NativeWindow+Rect
    if (-not [WindowsPeace.Stand.NativeWindow]::GetWindowRect($WindowHandle, [ref]$rectangle)) {
        throw 'Окно не отдало свои размеры — вероятно, оно уже закрыто.'
    }

    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Размеры окна бессмысленны: ${width}×${height}. Окно свёрнуто?"
    }

    $bitmap = New-Object System.Drawing.Bitmap($width, $height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $deviceContext = $graphics.GetHdc()
            try {
                # Без PW_RENDERFULLCONTENT WPF отдаёт пустой прямоугольник:
                # он рисует не через GDI, и обычный снимок его не застаёт.
                $printed = [WindowsPeace.Stand.NativeWindow]::PrintWindow(
                    $WindowHandle, $deviceContext, [WindowsPeace.Stand.NativeWindow]::RenderFullContent)
                if (-not $printed) { throw 'PrintWindow отказался рисовать окно.' }
            }
            finally { $graphics.ReleaseHdc($deviceContext) }
        }
        finally { $graphics.Dispose() }

        $area = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
        $data = $bitmap.LockBits($area, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, $bitmap.PixelFormat)
        try {
            $bytes = New-Object byte[] ($width * $height * 4)
            for ($y = 0; $y -lt $height; $y++) {
                $source = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
                [System.Runtime.InteropServices.Marshal]::Copy($source, $bytes, $y * $width * 4, $width * 4)
            }
        }
        finally { $bitmap.UnlockBits($data) }
    }
    finally { $bitmap.Dispose() }

    New-PeaceFrame -Width $width -Height $height -Bytes $bytes `
        -PixelFormat ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) -BytesPerPixel 4
}

function Save-PeaceFrame {
    <#
    .SYNOPSIS
        Записать кадр в PNG.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [psobject] $Frame,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $folder = Split-Path -Parent $Path
    if ($folder -and -not (Test-Path $folder)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    $bitmap = New-Object System.Drawing.Bitmap($Frame.Width, $Frame.Height, $Frame.PixelFormat)
    try {
        $area = New-Object System.Drawing.Rectangle(0, 0, $Frame.Width, $Frame.Height)
        $data = $bitmap.LockBits($area, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $Frame.PixelFormat)
        try {
            # Строки растра выровнены по четыре байта, а в кадре выравнивания нет.
            # Копирование одним куском вылезает за буфер и роняет процесс.
            $rowBytes = $Frame.Width * $Frame.BytesPerPixel
            for ($y = 0; $y -lt $Frame.Height; $y++) {
                $target = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
                [System.Runtime.InteropServices.Marshal]::Copy($Frame.Bytes, $y * $rowBytes, $target, $rowBytes)
            }
        }
        finally { $bitmap.UnlockBits($data) }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }

    Write-Verbose "Кадр записан: $Path ($($Frame.Width)×$($Frame.Height))"
}

function Measure-PeaceFrameDifference {
    <#
    .SYNOPSIS
        Насколько два кадра различаются: доля от нуля до единицы.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [AllowNull()] [psobject] $Reference,
        [Parameter(Mandatory = $true)] [AllowNull()] [psobject] $Current
    )

    if ($null -eq $Reference -or $null -eq $Current) { return 1.0 }
    [WindowsPeace.Stand.FrameMath]::DifferenceFraction($Reference.Bytes, $Current.Bytes)
}

function Test-PeaceFrameBlank {
    <#
    .SYNOPSIS
        Однотонный ли кадр. Чёрный экран во время загрузки тоже неподвижен,
        и без этой проверки ожидание закончилось бы на нём.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [psobject] $Frame,
        [double] $InkThreshold = 0.01
    )

    [WindowsPeace.Stand.FrameMath]::NonUniformFraction($Frame.Bytes) -lt $InkThreshold
}

function Wait-PeaceStableFrame {
    <#
    .SYNOPSIS
        Ждать, пока картинка перестанет меняться.

    .DESCRIPTION
        Признак «дело сделано» один и тот же и для загрузки среды, и для
        появления окна: картинка какое-то время подряд остаётся прежней,
        не однотонная и отличается от того, что было до нашего действия.

        Не дождаться — обычный исход, а не поломка: именно тогда снимок нужнее
        всего. Поэтому возвращается объект с признаком Settled и последним
        снятым кадром, а не исключение. Решает вызывающий.

    .PARAMETER Capture
        Блок, отдающий кадр. Ему разрешено бросать исключение, пока идёт
        разогрев: пока не вышло предельное время, отказ считается «ещё рано».

    .PARAMETER DifferentFrom
        Кадр «как было до нашего действия». Пока картинка на него похожа,
        ожидание не заканчивается.

    .PARAMETER MinDifference
        Насколько сильно картинка должна отличаться от DifferentFrom, чтобы
        считаться новой. Перевод строки в командной строке меняет проценту
        картинки, а нарисовавшееся окно — большую её часть. Разделяет их этот порог.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [scriptblock] $Capture,
        [double] $StableSeconds = 2.5,
        [double] $TimeoutSeconds = 180,
        [double] $PollSeconds = 0.7,
        [double] $Tolerance = 0.002,
        [psobject] $DifferentFrom,
        [double] $MinDifference = 0.002,
        [double] $MinWaitSeconds = 0,
        [switch] $AllowBlank,
        [string] $What = 'картинка'
    )

    $started = Get-Date
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $previous = $null
    $stableSince = $null
    $lastError = $null

    while ((Get-Date) -lt $deadline) {
        $frame = $null
        try {
            $frame = & $Capture
        }
        catch {
            # Пока среда не дошла до вывода картинки, снимок невозможен —
            # это нормальное состояние, а не поломка. Причину запоминаем:
            # если так и не дождёмся, человеку нужно будет её увидеть.
            $lastError = $_.Exception.Message
            $previous = $null
            $stableSince = $null
            Start-Sleep -Seconds $PollSeconds
            continue
        }

        $blank = (-not $AllowBlank) -and (Test-PeaceFrameBlank -Frame $frame)
        $stillOld = $false
        if ($DifferentFrom) {
            $stillOld = (Measure-PeaceFrameDifference -Reference $DifferentFrom -Current $frame) -lt $MinDifference
        }

        if ($blank -or $stillOld) {
            $previous = $frame
            $stableSince = $null
            Start-Sleep -Seconds $PollSeconds
            continue
        }

        $change = Measure-PeaceFrameDifference -Reference $previous -Current $frame
        if ($change -le $Tolerance) {
            if ($null -eq $stableSince) { $stableSince = Get-Date }

            # Заставка загрузки тоже бывает неподвижной. Ранняя часть ожидания
            # отдаётся ей, чтобы не принять её за готовую среду.
            $waitedEnough = ((Get-Date) - $started).TotalSeconds -ge $MinWaitSeconds
            if ($waitedEnough -and ((Get-Date) - $stableSince).TotalSeconds -ge $StableSeconds) {
                return [pscustomobject]@{
                    Settled = $true
                    Frame   = $frame
                    Reason  = "Картинка устоялась: $What."
                }
            }
        }
        else {
            $stableSince = $null
        }

        $previous = $frame
        Start-Sleep -Seconds $PollSeconds
    }

    $tail = if ($lastError) { " Последняя причина: $lastError" } else { '' }
    [pscustomobject]@{
        Settled = $false
        Frame   = $previous
        Reason  = "Не дождались, пока устоится $What. Прошло $TimeoutSeconds с.$tail"
    }
}

Export-ModuleMember -Function New-PeaceFrame, Get-PeaceVmFrame, Get-PeaceWindowFrame,
    Save-PeaceFrame, Measure-PeaceFrameDifference, Test-PeaceFrameBlank, Wait-PeaceStableFrame
