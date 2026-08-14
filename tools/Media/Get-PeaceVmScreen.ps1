<#
.SYNOPSIS
    Снимает экран гостя Hyper-V в PNG, не заходя в виртуалку.

.DESCRIPTION
    В WinPE нет ни PowerShell, ни буфера обмена, ни средств снятия экрана.
    Единственный способ увидеть, что там нарисовалось, — попросить картинку
    у самого Hyper-V. Он отдаёт её в формате RGB565, два байта на точку,
    и здесь она переводится в PNG.

    Это то, ради чего на шаге Б появился стенд: круг проверки становится
    двухминутным и не требует, чтобы автор перезагружал свою машину.

.EXAMPLE
    powershell -File tools/Media/Get-PeaceVmScreen.ps1 -OutPath D:\WindowsPeace-Stand\screen.png
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $OutPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$namespace = 'root\virtualization\v2'

$vm = Get-CimInstance -Namespace $namespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
if (-not $vm) { throw "Виртуалка '$Name' не найдена." }

$settings = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VirtualSystemSettingData |
    Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' }
if (-not $settings) { throw "У виртуалки '$Name' не нашлось действующих настроек." }

$head = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VideoHead
if (-not $head) { throw 'Гость не отдаёт видеовыход — вероятно, он выключен.' }

$width = [uint16]$head.CurrentHorizontalResolution
$height = [uint16]$head.CurrentVerticalResolution
if (-not $width -or -not $height) { throw 'Гость не отдаёт разрешение экрана — вероятно, он ещё не загрузился.' }

$service = Get-CimInstance -Namespace $namespace -ClassName Msvm_VirtualSystemManagementService
$result = Invoke-CimMethod -InputObject $service -MethodName GetVirtualSystemThumbnailImage -Arguments @{
    TargetSystem = [CimInstance]$settings
    WidthPixels  = $width
    HeightPixels = $height
}
if ($result.ReturnValue -ne 0) {
    throw "GetVirtualSystemThumbnailImage вернул $($result.ReturnValue). Запасной путь — окно vmconnect и PrintWindow."
}

# Hyper-V отдаёт картинку в RGB565: два байта на точку, без заголовка.
$bytes = [byte[]]$result.ImageData
$bitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
$rectangle = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
$data = $bitmap.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bitmap.PixelFormat)
try {
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
}
finally {
    $bitmap.UnlockBits($data)
}

$outFolder = Split-Path -Parent $OutPath
if ($outFolder -and -not (Test-Path $outFolder)) {
    New-Item -ItemType Directory -Force -Path $outFolder | Out-Null
}

$bitmap.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

Write-Host "Экран снят: $OutPath (${width}×${height})"
