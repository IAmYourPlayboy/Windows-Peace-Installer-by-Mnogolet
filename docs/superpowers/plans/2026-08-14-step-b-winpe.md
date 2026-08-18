# План шага Б: мастер в WinPE

> **Для исполнителя:** используй `superpowers:subagent-driven-development` или `superpowers:executing-plans`. Шаги отмечаются галочками.

**Цель:** загрузиться с флешки Windows Peace, увидеть, как мастер сам открылся, показал список рецептов и настоящие диски машины, — и записать, какой из трёх путей запуска интерфейса сработал.

**Устройство:** носитель из двух разделов (FAT32 «системный EFI» для загрузки, NTFS для всего остального), приложение лежит рядом с `boot.wim`, а не внутри него. Внутрь образа кладётся только подмена запуска. Риск снимается по частям: сначала консольная утилита, потом окно, потом самозапуск.

**Технологии:** C# 12, .NET 8 (самодостаточная публикация, **не в один файл**), WPF, `System.Text.Json`, PowerShell для сборки носителя, DISM, Hyper-V.

**Спека:** [2026-08-14-step-b-winpe-design.md](../specs/2026-08-14-step-b-winpe-design.md). При расхождении плана со спекой прав тот, кто заметил, — но менять надо оба файла, а не один.

## Общие ограничения

Действуют в каждой задаче, повторно не оговариваются.

- Ветка `step-b-winpe`. Слияние в `main` в конце шага, отправка на GitHub в конце.
- `Directory.Build.props` объявляет предупреждения ошибками. Собираться должно без единого предупреждения.
- `WindowsPeace.Core` собирается под `net48;net8.0-windows`. Всё, что попадает в `Core`, обязано компилироваться под обе цели.
- В `WindowsPeace.Core` не должно появиться ни одной ссылки на `PresentationFramework`. Это проверяется тестом шага А.
- Тесты пишутся до реализации. Модели экранов проверяются без запуска окна.
- Пустой `catch` запрещён. Каждый перехват либо объясняет человеку, что случилось, либо поднимает исключение выше.
- Ни одной операции наружу без предельного времени и признака отмены.
- Публикация приложения: `--self-contained true -r win-x64`, **без** `PublishSingleFile`. Сборка в один файл распаковывает себя во временную папку, а временная папка в WinPE лежит на оперативном диске `X:`.
- Нижняя граница памяти для WinPE — 4 ГБ. Виртуалка создаётся с четырьмя.
- Безопасная загрузка включена и в виртуалке, и на живом железе. Ничего своего в цепочку загрузки не подкладывается.
- Работа с дисками (`diskpart`, `Clear-Disk`, `Mount-VHD`, `Dism /Mount-Wim`) требует прав администратора. Скрипты проверяют права первой строкой.
- Комментарии и тексты интерфейса — по-русски, как в остальном проекте.

## Что где будет лежать

| Файл | За что отвечает |
|---|---|
| `contract/media.schema.json` | контракт описи носителя |
| `contract/examples/one-recipe.media.json` | пример описи |
| `src/WindowsPeace.Core/Media/MediaManifest.cs` | модель описи: `MediaManifest`, `MediaRecipe`, `MediaImage` |
| `src/WindowsPeace.Core/Media/MediaManifestReader.cs` | разбор JSON и четыре исхода чтения |
| `src/WindowsPeace.Core/Media/ITextFileReader.cs` | чтение текстового файла: интерфейс и настоящая реализация |
| `src/WindowsPeace.Core/Media/MediaLocation.cs` | найденный носитель: корень раздела и путь к описи |
| `src/WindowsPeace.Core/Storage/BootMediaLocator.cs` | к пометке дисков добавляется поиск, возвращающий носитель |
| `src/WindowsPeace.Core/Environment/EnvironmentSnapshot.cs` | снимок среды |
| `src/WindowsPeace.Core/Environment/IEnvironmentReader.cs` | откуда снимок берётся: интерфейс и настоящая реализация |
| `src/WindowsPeace.Core/Environment/HostEnvironment.cs` | сборка снимка и признак WinPE |
| `src/WindowsPeace.Core/Diagnostics/LogLocationResolver.cs` | выбор места для журнала с откатом |
| `src/WindowsPeace.Core/Diagnostics/NullOperationLog.cs` | журнал, которому некуда писать |
| `src/WindowsPeace.Setup/Pages/RecipePickerViewModel.cs` + `.xaml` | экран 1 «Что ставим» |
| `src/WindowsPeace.Setup/Pages/ConfirmViewModel.cs` + `.xaml` | экран 3 «Проверьте и подтвердите» |
| `src/WindowsPeace.Setup/Pages/ProgressViewModel.cs` + `.xaml` | экран 4, каркас |
| `src/WindowsPeace.Setup/Pages/DoneViewModel.cs` + `.xaml` | экран 5, каркас |
| `tools/Media/Save-InstallSource.ps1` | спасти содержимое флешки на диск |
| `tools/Media/Build-PeaceMedia.ps1` | собрать носитель: виртуальный диск или настоящая флешка |
| `tools/Media/Patch-BootWim.ps1` | подменить запуск внутри образа |
| `tools/Media/New-PeaceVm.ps1` | создать виртуалку стенда |
| `tools/Media/Get-PeaceVmScreen.ps1` | снять экран гостя в PNG |
| `docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md` | что показали три опыта |

---

## Задача 1: Спасти исходный материал

Единственный экземпляр `install.wim` лежит на флешке, которую этот шаг переразметит. Своего ISO на машине нет. Пока копия не сделана, ни одна команда разметки не запускается.

**Файлы:**
- Создать: `tools/Media/Save-InstallSource.ps1`

**Отдаёт дальше:** папку `D:\WindowsPeace-Source\` с полным содержимым установочного носителя Windows.

- [ ] **Шаг 1: Написать скрипт копирования**

```powershell
<#
.SYNOPSIS
    Спасает содержимое установочного носителя Windows на диск.
    Запускается один раз, до любой разметки: дальше шаг Б переразметит флешку.
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

$free = (Get-PSDrive -Name ($Destination.Substring(0,1))).Free
$need = (Get-ChildItem $SourceRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
         Measure-Object -Property Length -Sum).Sum
Write-Host ("Копируем {0:N1} ГБ, свободно {1:N1} ГБ" -f ($need/1GB), ($free/1GB))
if ($free -lt $need * 1.1) { throw 'Мало места на диске назначения.' }

robocopy $SourceRoot $Destination /MIR /R:2 /W:2 /NFL /NDL /NP /XD 'System Volume Information' '$RECYCLE.BIN'
if ($LASTEXITCODE -ge 8) { throw "robocopy завершился с кодом $LASTEXITCODE" }

$bootWim = Join-Path $Destination 'sources\boot.wim'
$installWim = Join-Path $Destination 'sources\install.wim'
foreach ($f in @($bootWim, $installWim)) {
    if (-not (Test-Path $f)) { throw "После копирования не найден '$f'" }
    Write-Host ("{0}  {1:N2} ГБ" -f $f, ((Get-Item $f).Length/1GB))
}
Write-Host 'Исходный материал спасён.' -ForegroundColor Green
```

- [ ] **Шаг 2: Запустить и убедиться, что копия полная**

```bash
powershell -File tools/Media/Save-InstallSource.ps1 -SourceRoot E:\
```

Ожидается: `boot.wim` около 0,66 ГБ и `install.wim` около 9,04 ГБ в `D:\WindowsPeace-Source\sources\`, надпись «Исходный материал спасён».

- [ ] **Шаг 3: Сверить, что файлы читаются, а не просто существуют**

```bash
powershell -Command "dism /English /Get-WimInfo /WimFile:D:\WindowsPeace-Source\sources\install.wim | Select-String 'Index|Name'"
```

Ожидается: перечень изданий Windows. Если DISM ругается на файл — копия испорчена, повторить.

- [ ] **Шаг 4: Зафиксировать**

```bash
git add tools/Media/Save-InstallSource.ps1
git commit -m "Оснастка: спасение установочного носителя на диск до разметки"
```

---

## Задача 2: Сборка носителя

Один скрипт собирает и виртуальный диск, и настоящую флешку. Разница — в параметре. Это черновик того, что переедет в Studio на шаге Е.

**Файлы:**
- Создать: `tools/Media/Build-PeaceMedia.ps1`

**Берёт:** папку из задачи 1.
**Отдаёт дальше:** размеченный носитель с загрузочными файлами, приложением, рецептами и описью.

- [ ] **Шаг 1: Написать скрипт**

```powershell
<#
.SYNOPSIS
    Собирает носитель Windows Peace: два раздела, загрузочные файлы, приложение, опись.
.DESCRIPTION
    Цель задаётся одним из двух: -VhdxPath (создаётся виртуальный диск)
    или -UsbDiskNumber (переразмечается физический диск, только съёмный).
#>
[CmdletBinding(DefaultParameterSetName = 'Vhdx')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Vhdx')] [string] $VhdxPath,
    [Parameter(ParameterSetName = 'Vhdx')] [uint64] $VhdxSizeBytes = 24GB,
    [Parameter(Mandatory = $true, ParameterSetName = 'Usb')] [int] $UsbDiskNumber,
    [Parameter(Mandatory = $true, ParameterSetName = 'Usb')] [string] $ConfirmModel,

    [string] $SourceRoot = 'D:\WindowsPeace-Source',
    [Parameter(Mandatory = $true)] [string] $AppFolder,
    [string] $RecipeFile = 'contract/examples/atlas-25h2-ru.recipe.json',
    [switch] $SkipInstallWim
)

$ErrorActionPreference = 'Stop'
$ESP  = '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'
$DATA = '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'

function Assert-Admin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Нужны права администратора: разметка и монтирование без них не выполняются.'
    }
}
Assert-Admin

if (-not (Test-Path (Join-Path $SourceRoot 'sources\boot.wim'))) {
    throw "В '$SourceRoot' нет sources\boot.wim. Сначала запусти Save-InstallSource.ps1."
}
if (-not (Test-Path (Join-Path $AppFolder 'WindowsPeace.Setup.exe'))) {
    throw "В '$AppFolder' нет WindowsPeace.Setup.exe. Сначала опубликуй приложение."
}

# ---- получаем чистый диск ----
if ($PSCmdlet.ParameterSetName -eq 'Vhdx') {
    if (Test-Path $VhdxPath) { Remove-Item $VhdxPath -Force }
    New-VHD -Path $VhdxPath -SizeBytes $VhdxSizeBytes -Dynamic | Out-Null
    $disk = Mount-VHD -Path $VhdxPath -Passthru | Get-Disk
    Initialize-Disk -Number $disk.Number -PartitionStyle GPT | Out-Null
} else {
    $disk = Get-Disk -Number $UsbDiskNumber
    if ($disk.BusType -ne 'USB') { throw "Диск $UsbDiskNumber не съёмный (шина $($disk.BusType)). Отказ." }
    if ($disk.FriendlyName.Trim() -ne $ConfirmModel.Trim()) {
        throw "Модель не совпала: на диске '$($disk.FriendlyName)', введено '$ConfirmModel'. Отказ."
    }
    Write-Host "СТИРАЕМ диск $($disk.Number): $($disk.FriendlyName), $([math]::Round($disk.Size/1GB,1)) ГБ" -ForegroundColor Yellow
    Clear-Disk -Number $disk.Number -RemoveData -RemoveOEM -Confirm:$false
    Initialize-Disk -Number $disk.Number -PartitionStyle GPT -ErrorAction SilentlyContinue | Out-Null
}
$diskNumber = $disk.Number

try {
    # ---- раздел 1: загрузочный. Создаётся обычным, чтобы получить букву,
    #      и только в самом конце помечается системным EFI ----
    $bootPart = New-Partition -DiskNumber $diskNumber -Size 2GB -GptType $DATA -AssignDriveLetter
    Format-Volume -Partition $bootPart -FileSystem FAT32 -NewFileSystemLabel 'PEACEBOOT' -Confirm:$false | Out-Null
    $bootRoot = "$($bootPart.DriveLetter):\"

    # ---- раздел 2: данные ----
    $dataPart = New-Partition -DiskNumber $diskNumber -UseMaximumSize -GptType $DATA -AssignDriveLetter
    Format-Volume -Partition $dataPart -FileSystem NTFS -NewFileSystemLabel 'Windows Peace' -Confirm:$false | Out-Null
    $dataRoot = "$($dataPart.DriveLetter):\"

    Write-Host "Загрузочный раздел $bootRoot, раздел данных $dataRoot"

    # ---- загрузочные файлы ----
    foreach ($item in @('boot', 'efi', 'bootmgr', 'bootmgr.efi')) {
        $src = Join-Path $SourceRoot $item
        if (Test-Path $src) { Copy-Item $src -Destination $bootRoot -Recurse -Force }
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $bootRoot 'sources') | Out-Null
    Copy-Item (Join-Path $SourceRoot 'sources\boot.wim') (Join-Path $bootRoot 'sources\boot.wim') -Force

    # ---- данные ----
    New-Item -ItemType Directory -Force -Path (Join-Path $dataRoot 'sources'), `
        (Join-Path $dataRoot 'recipes'), (Join-Path $dataRoot 'WindowsPeace') | Out-Null

    if (-not $SkipInstallWim) {
        Copy-Item (Join-Path $SourceRoot 'sources\install.wim') (Join-Path $dataRoot 'sources\install.wim') -Force
    }

    robocopy $AppFolder (Join-Path $dataRoot 'WindowsPeace') /E /R:2 /W:2 /NFL /NDL /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy приложения завершился с кодом $LASTEXITCODE" }

    $recipeName = Split-Path $RecipeFile -Leaf
    Copy-Item $RecipeFile (Join-Path $dataRoot "recipes\$recipeName") -Force

    # ---- опись ----
    $manifest = [ordered]@{
        schemaVersion = 1
        buildId       = [guid]::NewGuid().ToString()
        createdUtc    = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        tool          = [ordered]@{ name = 'tools/Media/Build-PeaceMedia.ps1'; version = '0.1.0' }
        recipes       = @(
            [ordered]@{
                id          = [IO.Path]::GetFileNameWithoutExtension($recipeName) -replace '\.recipe$', ''
                name        = 'Atlas 25H2 RU'
                description = 'Windows 11 Pro 25H2 ru-RU, Atlas, Windhawk'
                recipeFile  = "recipes\$recipeName"
                image       = [ordered]@{
                    file      = 'sources\install.wim'
                    index     = 1
                    imageName = 'Windows 11 Pro'
                }
            }
        )
    }
    $json = $manifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $dataRoot 'windows-peace-media.json'), $json, [Text.UTF8Encoding]::new($false))

    # ---- метка системного EFI ставится последней: после неё раздел теряет букву ----
    Set-Partition -DiskNumber $diskNumber -PartitionNumber $bootPart.PartitionNumber -GptType $ESP
    Write-Host 'Носитель собран.' -ForegroundColor Green
}
finally {
    if ($PSCmdlet.ParameterSetName -eq 'Vhdx') { Dismount-VHD -Path $VhdxPath -ErrorAction SilentlyContinue }
}
```

- [ ] **Шаг 2: Опубликовать приложение и собрать виртуальный диск**

```bash
dotnet publish src/WindowsPeace.Setup -c Release -r win-x64 --self-contained true -o artifacts/setup
```

```bash
powershell -File tools/Media/Build-PeaceMedia.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -AppFolder artifacts\setup -SkipInstallWim
```

`-SkipInstallWim` на первых кругах экономит девять гигабайт копирования: до шага В образ Windows никому не нужен.

- [ ] **Шаг 3: Проверить получившееся**

```bash
powershell -Command "$d=(Mount-VHD -Path D:\WindowsPeace-Stand\peace.vhdx -Passthru|Get-Disk); Get-Partition -DiskNumber $d.Number | Format-Table PartitionNumber,GptType,DriveLetter,Size; Dismount-VHD -Path D:\WindowsPeace-Stand\peace.vhdx"
```

Ожидается: два раздела, у первого тип `{c12a7328-…}` и **пустая** буква, у второго обычный тип и буква есть.

- [ ] **Шаг 4: Зафиксировать**

```bash
git add tools/Media/Build-PeaceMedia.ps1
git commit -m "Оснастка: сборка носителя Windows Peace из двух разделов"
```

---

## Задача 3: Стенд Hyper-V и снимок экрана гостя

Без снимка экрана каждый круг проверки требует автора. Со снимком — не требует.

**Файлы:**
- Создать: `tools/Media/New-PeaceVm.ps1`, `tools/Media/Get-PeaceVmScreen.ps1`

**Отдаёт дальше:** виртуалку `Windows Peace Stand` и способ увидеть её экран в PNG.

- [ ] **Шаг 1: Написать создание виртуалки**

```powershell
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $VhdxPath,
    [uint64] $MemoryBytes = 4GB
)
$ErrorActionPreference = 'Stop'

if (Get-VM -Name $Name -ErrorAction SilentlyContinue) {
    Stop-VM -Name $Name -TurnOff -Force -ErrorAction SilentlyContinue
    Remove-VM -Name $Name -Force
}

$vm = New-VM -Name $Name -Generation 2 -MemoryStartupBytes $MemoryBytes -VHDPath $VhdxPath
Set-VMProcessor -VMName $Name -Count 2
Set-VMMemory   -VMName $Name -DynamicMemoryEnabled $false
Remove-VMNetworkAdapter -VMName $Name -ErrorAction SilentlyContinue
Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate 'MicrosoftWindows'
Set-VMFirmware -VMName $Name -FirstBootDevice (Get-VMHardDiskDrive -VMName $Name)
Write-Host "Виртуалка '$Name' создана: 2 ядра, $([math]::Round($MemoryBytes/1GB,0)) ГБ, безопасная загрузка включена." -ForegroundColor Green
```

Сеть отключается намеренно: на шаге Б она не нужна, а лишнее устройство — лишний источник задержек при загрузке.

- [ ] **Шаг 2: Написать снимок экрана**

```powershell
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $OutPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ns  = 'root\virtualization\v2'
$vm  = Get-CimInstance -Namespace $ns -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
if (-not $vm) { throw "Виртуалка '$Name' не найдена." }

$settings = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VirtualSystemSettingData |
            Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' }
$head = Get-CimAssociatedInstance $vm -ResultClassName Msvm_VideoHead
$w = [uint16]$head.CurrentHorizontalResolution
$h = [uint16]$head.CurrentVerticalResolution
if (-not $w -or -not $h) { throw 'Гость не отдаёт разрешение экрана — вероятно, он выключен.' }

$svc = Get-CimInstance -Namespace $ns -ClassName Msvm_VirtualSystemManagementService
$res = Invoke-CimMethod -InputObject $svc -MethodName GetVirtualSystemThumbnailImage -Arguments @{
    TargetSystem = [CimInstance]$settings
    WidthPixels  = $w
    HeightPixels = $h
}
if ($res.ReturnValue -ne 0) { throw "GetVirtualSystemThumbnailImage вернул $($res.ReturnValue)" }

# Hyper-V отдаёт картинку в формате RGB565 — два байта на точку.
$bytes = [byte[]]$res.ImageData
$bmp   = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
$rect  = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$data  = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$bmp.UnlockBits($data)
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Экран снят: $OutPath ($w×$h)"
```

- [ ] **Шаг 3: Проверить весь стенд разом**

```bash
powershell -File tools/Media/New-PeaceVm.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx
```

```bash
powershell -Command "Start-VM -Name 'Windows Peace Stand'; Start-Sleep -Seconds 45; & 'tools/Media/Get-PeaceVmScreen.ps1' -OutPath D:\WindowsPeace-Stand\screen.png"
```

Ожидается: PNG, на котором виден загрузившийся установщик Windows. Именно установщик, а не наш мастер: образ пока не правлен — так и задумано.

- [ ] **Шаг 4: Если снимок не получился**

Запасной путь: открыть окно `vmconnect.exe` и снять его приёмом шага А — `PrintWindow` с флагом `PW_RENDERFULLCONTENT`. Записать в заметку, какой способ сработал, и дальше пользоваться им.

- [ ] **Шаг 5: Зафиксировать**

```bash
git add tools/Media/New-PeaceVm.ps1 tools/Media/Get-PeaceVmScreen.ps1
git commit -m "Оснастка: стенд Hyper-V и снимок экрана гостя без участия автора"
```

---

## Задача 4: Опыт 1 — консоль в WinPE

Первый настоящий ответ. Отвечает разом на три вопроса: стартует ли самодостаточное .NET 8 в WinPE, отвечает ли там `StorageWMI`, видит ли наше перечисление диски.

**Файлы:**
- Изменить: `tools/DiskDump/Program.cs` — добавить запись вывода в файл
- Создать: `docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md`

- [ ] **Шаг 1: Научить DiskDump писать вывод в файл**

В WinPE нет ни буфера обмена, ни возможности переписать экран. После перезагрузки останется только то, что легло на носитель.

В начало `Main` добавить:

```csharp
// В WinPE прочитать экран нечем: ни буфера обмена, ни PowerShell.
// Всё, что мы увидим после перезагрузки, — этот файл на носителе.
var dumpPath = Path.Combine(AppContext.BaseDirectory, "disk-dump.txt");
using var file = new StreamWriter(dumpPath, append: false, new UTF8Encoding(false));
using var both = new DoubleWriter(Console.Out, file);
Console.SetOut(both);
Console.WriteLine($"Windows Peace DiskDump, {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Среда: {System.Environment.OSVersion}, каталог {AppContext.BaseDirectory}");
```

И рядом, в том же файле:

```csharp
/// <summary>Пишет одновременно на экран и в файл: в WinPE экран прочитать нечем.</summary>
internal sealed class DoubleWriter : TextWriter
{
    private readonly TextWriter _first;
    private readonly TextWriter _second;

    public DoubleWriter(TextWriter first, TextWriter second)
    {
        _first = first;
        _second = second;
    }

    public override Encoding Encoding => _first.Encoding;

    public override void Write(char value)
    {
        _first.Write(value);
        _second.Write(value);
    }

    public override void Flush()
    {
        _first.Flush();
        _second.Flush();
    }
}
```

- [ ] **Шаг 2: Опубликовать DiskDump самодостаточным и проверить его на обычной Windows**

```bash
dotnet publish tools/DiskDump -c Release -r win-x64 --self-contained true -o artifacts/diskdump
```

```bash
artifacts\diskdump\DiskDump.exe
```

Ожидается: те же три диска, что и раньше, и рядом появился `artifacts\diskdump\disk-dump.txt` с тем же текстом.

- [ ] **Шаг 3: Положить обе программы на носитель и пересобрать виртуальный диск**

```bash
powershell -File tools/Media/Build-PeaceMedia.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -AppFolder artifacts\setup -SkipInstallWim
```

Затем скопировать `artifacts\diskdump` в `\WindowsPeace\DiskDump\` на разделе данных — примонтировав виртуальный диск.

- [ ] **Шаг 4: Провести опыт**

Запустить виртуалку. Дождаться первого экрана установщика Windows. Нажать **Shift+F10** — откроется командная строка. В ней:

```
for %d in (C D E F G) do @if exist %d:\windows-peace-media.json set P=%d:
%P%\WindowsPeace\DiskDump\DiskDump.exe
```

Снять экран `Get-PeaceVmScreen.ps1`.

Три возможных исхода, и все три — результат:

| Что на экране | Что это значит | Что дальше |
|---|---|---|
| перечень дисков | .NET 8 в WinPE работает, `StorageWMI` отвечает | задача 5 |
| ошибка запуска программы | среда выполнения не стартует | запасной путь `WinPE-NetFx`, спека раздел 13 |
| программа стартовала, но диски не перечислились | среда работает, отказал `StorageWMI` | разобрать по `disk-dump.txt`, это не повод менять технологию |

- [ ] **Шаг 5: Записать результат в заметку**

Создать `docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md`: дата, что запускалось, что вышло, снимок экрана, содержимое `disk-dump.txt`. Заметка ведётся дальше по ходу шага — второй и третий опыты дописываются в неё же.

- [ ] **Шаг 6: Зафиксировать**

```bash
git add tools/DiskDump docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md
git commit -m "Опыт 1: консольная утилита в WinPE, вывод остаётся на носителе"
```

---

## Задача 5: Ранний журнал, снимок среды, контрольные точки старта

Если окно не нарисуется, надо знать, на каком шаге всё оборвалось. Сейчас журнал заводится после старта WPF, и падение до окна не оставляет ничего.

**Файлы:**
- Создать: `src/WindowsPeace.Core/Diagnostics/LogLocationResolver.cs`, `src/WindowsPeace.Core/Diagnostics/NullOperationLog.cs`
- Создать: `src/WindowsPeace.Core/Environment/EnvironmentSnapshot.cs`, `IEnvironmentReader.cs`, `HostEnvironment.cs`
- Изменить: `src/WindowsPeace.Setup/App.xaml.cs`
- Тесты: `test/WindowsPeace.Core.Tests/Diagnostics/LogLocationResolverTests.cs`, `test/WindowsPeace.Core.Tests/Environment/HostEnvironmentTests.cs`

**Отдаёт дальше:** `LogLocationResolver.Resolve(...) -> LogLocation`, `HostEnvironment.Describe(IEnvironmentReader) -> EnvironmentSnapshot`, `NullOperationLog.Instance`.

- [ ] **Шаг 1: Написать падающие тесты выбора места для журнала**

```csharp
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests.Diagnostics;

public sealed class LogLocationResolverTests
{
    private sealed class Probe : IWritabilityProbe
    {
        private readonly HashSet<string> _writable;
        public Probe(params string[] writable) => _writable = new HashSet<string>(writable);
        public bool CanWrite(string directory) => _writable.Contains(directory);
    }

    [Fact]
    public void Предпочтительное_место_выбирается_когда_туда_пишется()
    {
        var location = LogLocationResolver.Resolve(@"E:\WindowsPeace\logs", @"X:\WindowsPeace\logs",
            new Probe(@"E:\WindowsPeace\logs"));

        Assert.True(location.IsAvailable);
        Assert.False(location.IsTemporary);
        Assert.Equal(@"E:\WindowsPeace\logs", location.Directory);
    }

    [Fact]
    public void Откат_помечается_временным_и_объясняется()
    {
        var location = LogLocationResolver.Resolve(@"E:\WindowsPeace\logs", @"X:\WindowsPeace\logs",
            new Probe(@"X:\WindowsPeace\logs"));

        Assert.True(location.IsAvailable);
        Assert.True(location.IsTemporary);
        Assert.Equal(@"X:\WindowsPeace\logs", location.Directory);
        Assert.Contains("перезагрузк", location.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Когда_писать_некуда_журнала_нет_но_программа_не_падает()
    {
        var location = LogLocationResolver.Resolve(@"E:\logs", @"X:\logs", new Probe());

        Assert.False(location.IsAvailable);
        Assert.NotEmpty(location.Reason);
    }
}
```

- [ ] **Шаг 2: Убедиться, что тесты не собираются**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

Ожидается: ошибка компиляции — `LogLocationResolver` не найден.

- [ ] **Шаг 3: Написать реализацию**

`src/WindowsPeace.Core/Diagnostics/LogLocationResolver.cs`:

```csharp
using System;
using System.IO;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>Может ли туда писать. Отдельным интерфейсом, чтобы проверялось тестом.</summary>
public interface IWritabilityProbe
{
    bool CanWrite(string directory);
}

/// <summary>Настоящая проверка: пробным файлом, а не догадкой по признакам.</summary>
public sealed class RealWritabilityProbe : IWritabilityProbe
{
    public bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".peace-write-probe");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>Где будет лежать журнал и переживёт ли он перезагрузку.</summary>
public sealed class LogLocation
{
    public LogLocation(bool isAvailable, string directory, bool isTemporary, string reason)
    {
        IsAvailable = isAvailable;
        Directory = directory;
        IsTemporary = isTemporary;
        Reason = reason;
    }

    public bool IsAvailable { get; }
    public string Directory { get; }
    public bool IsTemporary { get; }
    public string Reason { get; }
}

/// <summary>
/// Журнал нужен именно тогда, когда что-то пошло не так, — то есть после
/// перезагрузки. Оперативный диск WinPE её не переживает, поэтому сначала
/// пробуем носитель и только потом отступаем.
/// </summary>
public static class LogLocationResolver
{
    public static LogLocation Resolve(string preferred, string fallback, IWritabilityProbe probe)
    {
        if (probe.CanWrite(preferred))
        {
            return new LogLocation(true, preferred, false, "Журнал лежит рядом с приложением.");
        }

        if (probe.CanWrite(fallback))
        {
            return new LogLocation(true, fallback, true,
                "Рядом с приложением писать не удалось. Журнал временный: он лежит в оперативной памяти и погибнет при перезагрузке.");
        }

        return new LogLocation(false, string.Empty, false,
            "Записать журнал некуда: ни рядом с приложением, ни на оперативном диске.");
    }
}
```

`src/WindowsPeace.Core/Diagnostics/NullOperationLog.cs`:

```csharp
namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Журнал, которому некуда писать. Существует, чтобы отсутствие места для
/// журнала не роняло программу и не требовало проверок на null в каждом вызове.
/// О том, что журнала нет, человеку сообщается на экране — молча это не проходит.
/// </summary>
public sealed class NullOperationLog : IOperationLog
{
    public static readonly NullOperationLog Instance = new();

    private NullOperationLog()
    {
    }

    public void Write(OperationRecord record)
    {
    }
}
```

- [ ] **Шаг 4: Прогнать тесты**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

Ожидается: три новых теста проходят под обеими целями сборки.

- [ ] **Шаг 5: Написать падающие тесты снимка среды**

```csharp
using WindowsPeace.Core.Environment;
using Xunit;

namespace WindowsPeace.Core.Tests.Environment;

public sealed class HostEnvironmentTests
{
    private sealed class Reader : IEnvironmentReader
    {
        public bool MiniNt { get; set; }
        public bool SegoeUi { get; set; }

        public bool RegistryKeyExists(string path) => MiniNt && path.EndsWith("MiniNT", StringComparison.Ordinal);
        public bool FileExists(string path) => SegoeUi && path.EndsWith("segoeui.ttf", StringComparison.OrdinalIgnoreCase);
        public string OsVersion() => "10.0.26100";
        public ulong TotalMemoryBytes() => 4UL * 1024 * 1024 * 1024;
        public IReadOnlyList<string> VolumeRoots() => new[] { @"X:\", @"E:\" };
        public string WindowsDirectory() => @"X:\Windows";
    }

    [Fact]
    public void Ключ_MiniNT_означает_что_мы_в_WinPE()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = true });
        Assert.True(snapshot.IsWindowsPe);
    }

    [Fact]
    public void Без_ключа_MiniNT_это_обычная_Windows()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = false });
        Assert.False(snapshot.IsWindowsPe);
    }

    [Fact]
    public void Отсутствие_обычного_Segoe_UI_попадает_в_снимок()
    {
        var snapshot = HostEnvironment.Describe(new Reader { SegoeUi = false });
        Assert.False(snapshot.SegoeUiRegularPresent);
    }

    [Fact]
    public void Снимок_описывает_себя_одной_строкой_для_журнала()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = true, SegoeUi = false });
        var text = snapshot.ToString();

        Assert.Contains("WinPE", text, StringComparison.Ordinal);
        Assert.Contains("Segoe UI", text, StringComparison.Ordinal);
    }
}
```

- [ ] **Шаг 6: Написать реализацию снимка среды**

`src/WindowsPeace.Core/Environment/IEnvironmentReader.cs`:

```csharp
using System.Collections.Generic;

namespace WindowsPeace.Core.Environment;

/// <summary>Откуда берутся сведения о среде. Отдельно от разбора, чтобы разбор проверялся тестом.</summary>
public interface IEnvironmentReader
{
    bool RegistryKeyExists(string path);
    bool FileExists(string path);
    string OsVersion();
    ulong TotalMemoryBytes();
    IReadOnlyList<string> VolumeRoots();
    string WindowsDirectory();
}
```

`src/WindowsPeace.Core/Environment/EnvironmentSnapshot.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindowsPeace.Core.Environment;

/// <summary>
/// Что мы знаем о машине на момент старта. Уходит первой записью в журнал:
/// в WinPE это единственное, что останется после перезагрузки.
/// </summary>
public sealed class EnvironmentSnapshot
{
    public string OsVersion { get; init; } = string.Empty;
    public bool IsWindowsPe { get; init; }
    public ulong TotalMemoryBytes { get; init; }
    public bool SegoeUiRegularPresent { get; init; }
    public IReadOnlyList<string> VolumeRoots { get; init; } = new List<string>();

    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}; среда: {1}; память: {2:N0} МБ; обычный Segoe UI: {3}; тома: {4}",
        OsVersion,
        IsWindowsPe ? "WinPE" : "обычная Windows",
        TotalMemoryBytes / (1024 * 1024),
        SegoeUiRegularPresent ? "есть" : "нет",
        string.Join(" ", VolumeRoots.ToArray()));
}
```

`src/WindowsPeace.Core/Environment/HostEnvironment.cs`:

```csharp
using System.IO;

namespace WindowsPeace.Core.Environment;

/// <summary>Сборка снимка среды. Признак WinPE — ключ MiniNT, он создаётся только там.</summary>
public static class HostEnvironment
{
    public const string MiniNtKey = @"SYSTEM\CurrentControlSet\Control\MiniNT";

    public static EnvironmentSnapshot Describe(IEnvironmentReader reader) => new()
    {
        OsVersion = reader.OsVersion(),
        IsWindowsPe = reader.RegistryKeyExists(MiniNtKey),
        TotalMemoryBytes = reader.TotalMemoryBytes(),
        SegoeUiRegularPresent = reader.FileExists(Path.Combine(reader.WindowsDirectory(), "Fonts", "segoeui.ttf")),
        VolumeRoots = reader.VolumeRoots(),
    };
}
```

`src/WindowsPeace.Core/Environment/RealEnvironmentReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace WindowsPeace.Core.Environment;

/// <summary>Настоящие сведения о машине. Каждый вызов защищён: снимок среды не имеет права уронить старт.</summary>
public sealed class RealEnvironmentReader : IEnvironmentReader
{
    public bool RegistryKeyExists(string path)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key is not null;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public string OsVersion() => System.Environment.OSVersion.VersionString;

    public ulong TotalMemoryBytes()
    {
        try
        {
            return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        }
        catch (PlatformNotSupportedException)
        {
            return 0UL;
        }
    }

    public IReadOnlyList<string> VolumeRoots()
    {
        try
        {
            var roots = new List<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                roots.Add(drive.Name);
            }

            return roots;
        }
        catch (IOException)
        {
            return new List<string>();
        }
    }

    public string WindowsDirectory() => System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
}
```

Замечание для исполнителя: `Microsoft.VisualBasic.Devices.ComputerInfo` под `net8.0-windows` требует `<UseVB>` или ссылки на `Microsoft.VisualBasic`. Если сборка не проходит — заменить чтение памяти на WMI-запрос `Win32_ComputerSystem.TotalPhysicalMemory` через уже подключённый `System.Management`, тем же приёмом, что в `WmiDiskEnumerator`. Тесты от этого не меняются: они работают с подставным читателем.

- [ ] **Шаг 7: Прогнать тесты**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

Ожидается: четыре новых теста проходят.

- [ ] **Шаг 8: Подключить всё это к старту приложения**

`src/WindowsPeace.Setup/App.xaml.cs`, метод `OnStartup` — заменить первые строки:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // Место для журнала выбирается до всего остального: если дальше что-то
    // упадёт, единственным следом останется этот файл.
    var location = LogLocationResolver.Resolve(
        Path.Combine(AppContext.BaseDirectory, "logs"),
        @"X:\WindowsPeace\logs",
        new RealWritabilityProbe());

    _log = location.IsAvailable
        ? new JsonLinesOperationLog(Path.Combine(location.Directory, "windows-peace.jsonl"))
        : null;

    IOperationLog log = (IOperationLog?)_log ?? NullOperationLog.Instance;

    Checkpoint(log, "Место для журнала выбрано", location.Reason);

    var snapshot = HostEnvironment.Describe(new RealEnvironmentReader());
    Checkpoint(log, "Снимок среды", snapshot.ToString());

    var probe = new RealFileSystemProbe();

    var diskPicker = new DiskPickerViewModel(
        new WmiDiskEnumerator(log),
        new FileSystemContentInspector(probe),
        probe);

    Checkpoint(log, "Модели экранов созданы", null);

    var navigator = new WizardNavigator(new List<IWizardPage>
    {
        diskPicker,
        new PlaceholderViewModel(),
    });

    var window = new ShellWindow { DataContext = new ShellViewModel(navigator) };
    Checkpoint(log, "Окно создано", null);

    window.ContentRendered += (_, _) => Checkpoint(log, "Первая отрисовка прошла", null);
    window.Show();
    Checkpoint(log, "Show вызван", null);
}

/// <summary>
/// Контрольная точка старта. В WinPE падение до окна не оставляет ничего,
/// кроме журнала, — по этим записям видно, на каком шаге всё оборвалось.
/// </summary>
private static void Checkpoint(IOperationLog log, string what, string? detail)
    => log.Write(new OperationRecord(
        DateTimeOffset.Now, "Setup.Startup", what, TimeSpan.Zero, OperationOutcome.Success, detail));
```

Добавить недостающие `using`: `System.IO`, `WindowsPeace.Core.Environment`.

- [ ] **Шаг 9: Проверить на обычной Windows**

```bash
dotnet build && dotnet test
```

Запустить приложение и убедиться, что в `logs\windows-peace.jsonl` появились пять записей `Setup.Startup`, включая снимок среды со строкой «обычная Windows» и «обычный Segoe UI: есть».

- [ ] **Шаг 10: Зафиксировать**

```bash
git add src/WindowsPeace.Core/Diagnostics src/WindowsPeace.Core/Environment src/WindowsPeace.Setup/App.xaml.cs test
git commit -m "Ранний журнал, снимок среды и контрольные точки старта"
```

---

## Задача 6: Опыт 2 — окно в WinPE

Главный вопрос проекта. К этому моменту известно, что среда выполнения работает; остаётся отрисовка.

- [ ] **Шаг 1: Пересобрать приложение и носитель**

```bash
dotnet publish src/WindowsPeace.Setup -c Release -r win-x64 --self-contained true -o artifacts/setup
```

```bash
powershell -File tools/Media/Build-PeaceMedia.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -AppFolder artifacts\setup -SkipInstallWim
```

- [ ] **Шаг 2: Провести опыт**

Запустить виртуалку, дождаться установщика, Shift+F10, и в командной строке:

```
for %d in (C D E F G) do @if exist %d:\windows-peace-media.json set P=%d:
%P%\WindowsPeace\WindowsPeace.Setup.exe
```

Снять экран.

- [ ] **Шаг 3: Разобрать исход**

| Что вышло | Что дальше |
|---|---|
| окно нарисовалось, диски видны | риск снят, идём к задаче 7 |
| окно нарисовалось, текст кривой или пустой | вопрос шрифтов, задача 12; риск снят |
| окна нет, в журнале есть «Окно создано» | WPF создался, но не отрисовался — пробовать `RenderOptions.ProcessRenderMode = SoftwareOnly` |
| окна нет, в журнале только «Снимок среды» | WPF не загрузился — запасной путь `WinPE-NetFx`, спека раздел 13 |
| журнала нет вовсе | носитель недоступен для записи — смотреть `X:\WindowsPeace\logs` |

Журнал забирается с носителя: выключить виртуалку, примонтировать виртуальный диск в хозяйской системе, прочитать `\WindowsPeace\logs\windows-peace.jsonl`.

- [ ] **Шаг 4: Записать результат и решение**

Дописать в `docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md`: снимок экрана, выдержку из журнала, вывод. Если сработал первый путь — записать это в [ROADMAP.md](../../ROADMAP.md) и [ARCHITECTURE.md](../../ARCHITECTURE.md): от ответа зависит целевая версия .NET во всём проекте.

**Это место, где план останавливается и ждёт автора.** Если окно не нарисовалось, решение о переходе на запасной путь принимает он, а не исполнитель.

- [ ] **Шаг 5: Зафиксировать**

```bash
git add docs
git commit -m "Опыт 2: окно WPF в WinPE, ответ на главный вопрос проекта"
```

---

## Задача 7: Контракт описи носителя

**Файлы:**
- Создать: `contract/media.schema.json`, `contract/examples/one-recipe.media.json`

**Отдаёт дальше:** форму описи, на которую опираются задачи 8 и 10.

- [ ] **Шаг 1: Написать схему**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://windowspeace.local/media.schema.json",
  "title": "Опись носителя Windows Peace",
  "type": "object",
  "required": ["schemaVersion", "buildId", "createdUtc", "recipes"],
  "additionalProperties": false,
  "properties": {
    "$schema": { "type": "string" },
    "schemaVersion": { "type": "integer", "minimum": 1 },
    "buildId": { "type": "string", "minLength": 1 },
    "createdUtc": { "type": "string", "format": "date-time" },
    "tool": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "name": { "type": "string" },
        "version": { "type": "string" }
      }
    },
    "recipes": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "name", "recipeFile", "image"],
        "additionalProperties": false,
        "properties": {
          "id": { "type": "string", "minLength": 1 },
          "name": { "type": "string", "minLength": 1 },
          "description": { "type": "string" },
          "recipeFile": { "type": "string", "minLength": 1 },
          "image": {
            "type": "object",
            "required": ["file", "index"],
            "additionalProperties": false,
            "properties": {
              "file": { "type": "string", "minLength": 1 },
              "index": { "type": "integer", "minimum": 1 },
              "imageName": { "type": "string" },
              "sizeBytes": { "type": "integer", "minimum": 0 }
            }
          }
        }
      }
    }
  }
}
```

Пустой список рецептов схемой разрешён намеренно: это не поломка формата, а отдельный исход чтения, о котором мастер сообщает своими словами.

- [ ] **Шаг 2: Написать пример**

`contract/examples/one-recipe.media.json` — ровно тот образец, что в спеке, раздел 5.

- [ ] **Шаг 3: Зафиксировать**

```bash
git add contract
git commit -m "Контракт описи носителя: схема и пример"
```

---

## Задача 8: Чтение описи

**Файлы:**
- Создать: `src/WindowsPeace.Core/Media/MediaManifest.cs`, `MediaManifestReader.cs`, `ITextFileReader.cs`
- Изменить: `src/WindowsPeace.Core/WindowsPeace.Core.csproj`
- Тесты: `test/WindowsPeace.Core.Tests/Media/MediaManifestReaderTests.cs`

**Отдаёт дальше:** `MediaManifestReader.Read(string json) -> MediaManifestResult` со статусами `Ok`, `Damaged`, `TooNew`, `NoRecipes`.

**Отступление от прежнего решения.** В `JsonLinesOperationLog` написано, что сериализация своя, «чтобы под net48 не тянуть зависимость». Для записи это верно: собрать строку просто. Для разбора — нет: свой разборщик JSON это ровно та кустарщина, которой в проекте быть не должно. Поэтому `System.Text.Json` добавляется, и та же библиотека понадобится для чтения рецепта на шаге В. Запись журнала не трогаем.

- [ ] **Шаг 1: Добавить зависимость**

В `src/WindowsPeace.Core/WindowsPeace.Core.csproj`, в блок `net48`:

```xml
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

- [ ] **Шаг 2: Проверить, что обе цели собираются**

```bash
dotnet build src/WindowsPeace.Core
```

- [ ] **Шаг 3: Написать падающие тесты**

```csharp
using WindowsPeace.Core.Media;
using Xunit;

namespace WindowsPeace.Core.Tests.Media;

public sealed class MediaManifestReaderTests
{
    private const string Whole = """
    {
      "schemaVersion": 1,
      "buildId": "8f3c9d2e",
      "createdUtc": "2026-08-14T12:00:00Z",
      "recipes": [{
        "id": "atlas-25h2-ru",
        "name": "Atlas 25H2 RU",
        "recipeFile": "recipes\\atlas.recipe.json",
        "image": { "file": "sources\\install.wim", "index": 1, "imageName": "Windows 11 Pro" }
      }]
    }
    """;

    [Fact]
    public void Целая_опись_читается()
    {
        var result = MediaManifestReader.Read(Whole);

        Assert.Equal(MediaManifestStatus.Ok, result.Status);
        Assert.Single(result.Manifest!.Recipes);
        Assert.Equal("Atlas 25H2 RU", result.Manifest!.Recipes[0].Name);
        Assert.Equal(1, result.Manifest!.Recipes[0].Image.Index);
    }

    [Fact]
    public void Испорченный_текст_объявляется_повреждением()
    {
        var result = MediaManifestReader.Read("{ это не json ");

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public void Версия_из_будущего_не_читается_молча()
    {
        var result = MediaManifestReader.Read(Whole.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"));

        Assert.Equal(MediaManifestStatus.TooNew, result.Status);
        Assert.Contains("99", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Пустой_список_рецептов_это_отдельный_исход()
    {
        var result = MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """);

        Assert.Equal(MediaManifestStatus.NoRecipes, result.Status);
    }

    [Fact]
    public void Рецепт_без_обязательного_поля_это_повреждение()
    {
        var result = MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z",
          "recipes": [ { "id": "x", "name": "X" } ] }
        """);

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
    }
}
```

- [ ] **Шаг 4: Убедиться, что тесты не собираются**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

- [ ] **Шаг 5: Написать модель**

`src/WindowsPeace.Core/Media/MediaManifest.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace WindowsPeace.Core.Media;

/// <summary>Один образ Windows, лежащий на носителе.</summary>
public sealed class MediaImage
{
    public string File { get; init; } = string.Empty;
    public int Index { get; init; }
    public string? ImageName { get; init; }
    public ulong? SizeBytes { get; init; }
}

/// <summary>Один рецепт из описи: что человек увидит на первом экране.</summary>
public sealed class MediaRecipe
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string RecipeFile { get; init; } = string.Empty;
    public MediaImage Image { get; init; } = new();
}

/// <summary>
/// Опись носителя. Единственный файл, который читается раньше рецепта:
/// по нему строится первый экран и по нему носитель опознаёт сам себя.
/// </summary>
public sealed class MediaManifest
{
    public int SchemaVersion { get; init; }
    public string BuildId { get; init; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; init; }
    public IReadOnlyList<MediaRecipe> Recipes { get; init; } = new List<MediaRecipe>();
}
```

- [ ] **Шаг 6: Написать чтение**

`src/WindowsPeace.Core/Media/MediaManifestReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace WindowsPeace.Core.Media;

/// <summary>Чем закончилось чтение описи.</summary>
public enum MediaManifestStatus
{
    Ok,
    Damaged,
    TooNew,
    NoRecipes,
}

/// <summary>Исход чтения вместе с объяснением для человека.</summary>
public sealed class MediaManifestResult
{
    public MediaManifestResult(MediaManifestStatus status, MediaManifest? manifest, string message)
    {
        Status = status;
        Manifest = manifest;
        Message = message;
    }

    public MediaManifestStatus Status { get; }
    public MediaManifest? Manifest { get; }
    public string Message { get; }
}

/// <summary>
/// Разбор описи. Молча продолжать нельзя ни в одном случае: дальше по пути
/// форматирование диска, и «наверное, там было что-то похожее» не годится.
/// </summary>
public static class MediaManifestReader
{
    public const int SupportedSchemaVersion = 1;

    public static MediaManifestResult Read(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged,
                null, "Опись носителя не разбирается: " + error.Message);
        }

        using (document)
        {
            var root = document.RootElement;

            if (!root.TryGetProperty("schemaVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number)
            {
                return new MediaManifestResult(MediaManifestStatus.Damaged,
                    null, "В описи нет версии формата.");
            }

            var version = versionElement.GetInt32();
            if (version > SupportedSchemaVersion)
            {
                return new MediaManifestResult(MediaManifestStatus.TooNew, null, string.Format(
                    CultureInfo.CurrentCulture,
                    "Носитель собран более новой версией Windows Peace: формат описи {0}, эта программа понимает {1}.",
                    version, SupportedSchemaVersion));
            }

            var recipes = new List<MediaRecipe>();
            if (root.TryGetProperty("recipes", out var recipesElement) &&
                recipesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in recipesElement.EnumerateArray())
                {
                    var recipe = ReadRecipe(item);
                    if (recipe is null)
                    {
                        return new MediaManifestResult(MediaManifestStatus.Damaged,
                            null, "В описи есть рецепт без обязательных полей.");
                    }

                    recipes.Add(recipe);
                }
            }
            else
            {
                return new MediaManifestResult(MediaManifestStatus.Damaged,
                    null, "В описи нет списка рецептов.");
            }

            var manifest = new MediaManifest
            {
                SchemaVersion = version,
                BuildId = Text(root, "buildId") ?? string.Empty,
                CreatedUtc = Moment(root, "createdUtc"),
                Recipes = recipes,
            };

            return recipes.Count == 0
                ? new MediaManifestResult(MediaManifestStatus.NoRecipes, manifest,
                    "На носителе нет ни одного рецепта.")
                : new MediaManifestResult(MediaManifestStatus.Ok, manifest, string.Empty);
        }
    }

    private static MediaRecipe? ReadRecipe(JsonElement element)
    {
        var id = Text(element, "id");
        var name = Text(element, "name");
        var recipeFile = Text(element, "recipeFile");

        if (id is null || name is null || recipeFile is null ||
            !element.TryGetProperty("image", out var imageElement))
        {
            return null;
        }

        var file = Text(imageElement, "file");
        if (file is null || !imageElement.TryGetProperty("index", out var indexElement) ||
            indexElement.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return new MediaRecipe
        {
            Id = id,
            Name = name,
            Description = Text(element, "description"),
            RecipeFile = recipeFile,
            Image = new MediaImage
            {
                File = file,
                Index = indexElement.GetInt32(),
                ImageName = Text(imageElement, "imageName"),
                SizeBytes = imageElement.TryGetProperty("sizeBytes", out var size) &&
                            size.ValueKind == JsonValueKind.Number
                    ? size.GetUInt64()
                    : null,
            },
        };
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset Moment(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment)
            ? moment
            : DateTimeOffset.MinValue;
}
```

- [ ] **Шаг 7: Прогнать тесты**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

Ожидается: пять новых тестов проходят под обеими целями.

- [ ] **Шаг 8: Зафиксировать**

```bash
git add src/WindowsPeace.Core test/WindowsPeace.Core.Tests
git commit -m "Чтение описи носителя: четыре исхода, ни один не молчаливый"
```

---

## Задача 9: Поиск носителя и отладочный ключ

**Файлы:**
- Создать: `src/WindowsPeace.Core/Media/MediaLocation.cs`, `src/WindowsPeace.Core/Media/ITextFileReader.cs`
- Изменить: `src/WindowsPeace.Core/Storage/BootMediaLocator.cs`
- Тесты: `test/WindowsPeace.Core.Tests/Storage/BootMediaLocatorTests.cs` (дополнить)

**Берёт:** `MediaManifestReader` из задачи 8.
**Отдаёт дальше:** `BootMediaLocator.Find(disks, probe) -> MediaLocation?` и `MediaLocation.Load(ITextFileReader) -> MediaManifestResult`.

- [ ] **Шаг 1: Написать падающий тест**

```csharp
[Fact]
public void Поиск_возвращает_корень_раздела_с_описью()
{
    var disk = DiskWithLetters('C', 'E');
    var probe = new FakeProbe(@"E:\windows-peace-media.json");

    var location = BootMediaLocator.Find(new[] { disk }, probe);

    Assert.NotNull(location);
    Assert.Equal(@"E:\", location!.Root);
    Assert.Equal(@"E:\windows-peace-media.json", location.ManifestPath);
}

[Fact]
public void Когда_описи_нигде_нет_поиск_возвращает_ничего()
{
    var disk = DiskWithLetters('C');
    Assert.Null(BootMediaLocator.Find(new[] { disk }, new FakeProbe()));
}
```

Вспомогательные `DiskWithLetters` и `FakeProbe` уже есть в файле тестов шага А — использовать их, а не заводить новые.

- [ ] **Шаг 2: Убедиться, что тест не собирается**

```bash
dotnet test test/WindowsPeace.Core.Tests
```

- [ ] **Шаг 3: Написать MediaLocation и ITextFileReader**

`src/WindowsPeace.Core/Media/ITextFileReader.cs`:

```csharp
using System;
using System.IO;

namespace WindowsPeace.Core.Media;

/// <summary>Чтение текстового файла. Отдельным интерфейсом, чтобы разбор описи проверялся без диска.</summary>
public interface ITextFileReader
{
    bool Exists(string path);
    string ReadAllText(string path);
}

/// <summary>Настоящее чтение с диска.</summary>
public sealed class FileTextReader : ITextFileReader
{
    public bool Exists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public string ReadAllText(string path) => File.ReadAllText(path);
}
```

`src/WindowsPeace.Core/Media/MediaLocation.cs`:

```csharp
using System;
using System.IO;

namespace WindowsPeace.Core.Media;

/// <summary>
/// Найденный носитель Windows Peace. Опознание идёт по наличию файла описи,
/// а не по его содержимому: испорченная опись не делает носитель чужим,
/// и предлагать установку на него всё равно нельзя.
/// </summary>
public sealed class MediaLocation
{
    public MediaLocation(string root)
    {
        Root = root;
        ManifestPath = Path.Combine(root, ManifestFileName);
    }

    public const string ManifestFileName = "windows-peace-media.json";

    public string Root { get; }

    public string ManifestPath { get; }

    /// <summary>Прочитать опись. Все отказы возвращаются исходом, а не исключением.</summary>
    public MediaManifestResult Load(ITextFileReader reader)
    {
        if (!reader.Exists(ManifestPath))
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Опись носителя исчезла между поиском и чтением.");
        }

        try
        {
            return MediaManifestReader.Read(reader.ReadAllText(ManifestPath));
        }
        catch (IOException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Опись носителя не читается: " + error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Доступ к описи носителя закрыт: " + error.Message);
        }
    }
}
```

- [ ] **Шаг 4: Добавить поиск в BootMediaLocator**

Имя файла теперь живёт в `MediaLocation`. В `BootMediaLocator` оставить константу как ссылку на него, чтобы не расходились:

```csharp
/// <summary>Имя описи. То же значение используется Studio при записи носителя.</summary>
public const string ManifestFileName = MediaLocation.ManifestFileName;

/// <summary>Первый найденный носитель, либо ничего. Пометку дисков не трогает.</summary>
public static MediaLocation? Find(IReadOnlyList<DiskInfo> disks, IFileSystemProbe probe)
{
    foreach (var disk in disks)
    {
        foreach (var partition in disk.Partitions)
        {
            if (partition.DriveLetter is null)
            {
                continue;
            }

            var root = string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value);
            if (probe.FileExists(Path.Combine(root, ManifestFileName)))
            {
                return new MediaLocation(root);
            }
        }
    }

    return null;
}
```

Добавить `using WindowsPeace.Core.Media;`.

- [ ] **Шаг 5: Прогнать тесты**

```bash
dotnet test
```

Ожидается: два новых теста проходят, все прежние — тоже.

- [ ] **Шаг 6: Добавить отладочный ключ --media**

В `App.xaml.cs`, после снимка среды:

```csharp
// На обычной Windows описи нигде нет. Отладочный ключ позволяет работать
// над экранами, не перезагружаясь в WinPE. В обычном ходе работы не используется.
var forcedMedia = ReadMediaOverride(e.Args);
Checkpoint(log, "Отладочный ключ --media", forcedMedia);
```

и рядом:

```csharp
private static string? ReadMediaOverride(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--media", StringComparison.Ordinal))
        {
            return args[i + 1];
        }
    }

    return null;
}
```

- [ ] **Шаг 7: Зафиксировать**

```bash
git add src test
git commit -m "Поиск носителя по описи и отладочный ключ --media"
```

---

## Задача 10: Экран 1 «Что ставим»

**Файлы:**
- Создать: `src/WindowsPeace.Setup/Pages/RecipePickerViewModel.cs`, `RecipeRowViewModel.cs`, `RecipePickerPage.xaml`, `RecipePickerPage.xaml.cs`
- Изменить: `src/WindowsPeace.Setup/App.xaml` (шаблон страницы), `App.xaml.cs` (порядок страниц)
- Тесты: `test/WindowsPeace.Setup.Tests/RecipePickerViewModelTests.cs`

**Отдаёт дальше:** `RecipePickerViewModel.SelectedRecipe` типа `MediaRecipe?` — им пользуется задача 11.

- [ ] **Шаг 1: Написать падающие тесты**

```csharp
using WindowsPeace.Core.Media;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public sealed class RecipePickerViewModelTests
{
    private static MediaManifestResult OneRecipe() => MediaManifestReader.Read("""
    { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z",
      "recipes": [ { "id": "atlas", "name": "Atlas 25H2 RU", "recipeFile": "r.json",
                     "image": { "file": "sources\\install.wim", "index": 1 } } ] }
    """);

    [Fact]
    public void Экран_ничего_не_выбирает_за_человека()
    {
        var page = new RecipePickerViewModel(OneRecipe());

        Assert.Single(page.Recipes);
        Assert.Null(page.SelectedRecipe);
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void После_выбора_можно_идти_дальше()
    {
        var page = new RecipePickerViewModel(OneRecipe());
        page.SelectedRow = page.Recipes[0];

        Assert.NotNull(page.SelectedRecipe);
        Assert.True(page.CanGoNext);
    }

    [Fact]
    public void Повреждённая_опись_объясняется_и_дальше_не_пускает()
    {
        var page = new RecipePickerViewModel(MediaManifestReader.Read("{ мусор"));

        Assert.Empty(page.Recipes);
        Assert.False(page.CanGoNext);
        Assert.Contains("не разбирается", page.Trouble, StringComparison.Ordinal);
    }

    [Fact]
    public void Пустой_список_рецептов_объясняется_своими_словами()
    {
        var page = new RecipePickerViewModel(MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """));

        Assert.False(page.CanGoNext);
        Assert.Contains("ни одного рецепта", page.Trouble, StringComparison.Ordinal);
    }

    [Fact]
    public void Носитель_не_найден_вовсе()
    {
        var page = RecipePickerViewModel.WithoutMedia(new[] { @"C:\", @"X:\" });

        Assert.False(page.CanGoNext);
        Assert.Contains("C:", page.Trouble, StringComparison.Ordinal);
    }
}
```

- [ ] **Шаг 2: Убедиться, что тесты не собираются**

```bash
dotnet test test/WindowsPeace.Setup.Tests
```

- [ ] **Шаг 3: Написать строку списка**

`src/WindowsPeace.Setup/Pages/RecipeRowViewModel.cs`:

```csharp
using System.Globalization;
using WindowsPeace.Core.Media;

namespace WindowsPeace.Setup.Pages;

/// <summary>Один рецепт в списке «что ставим».</summary>
public sealed class RecipeRowViewModel
{
    public RecipeRowViewModel(MediaRecipe recipe)
    {
        Recipe = recipe;
    }

    public MediaRecipe Recipe { get; }

    public string Name => Recipe.Name;

    public string Description => Recipe.Description ?? string.Empty;

    public string Image => Recipe.Image.ImageName ?? Recipe.Image.File;

    public string Size => Recipe.Image.SizeBytes is { } bytes
        ? string.Format(CultureInfo.CurrentCulture, "{0:N1} ГБ", bytes / 1024d / 1024d / 1024d)
        : string.Empty;

    // Средствам доступности строка обязана называть себя по-человечески,
    // а не именем класса. Тот же дефект уже ловили на шаге А.
    public override string ToString() => string.IsNullOrEmpty(Description)
        ? Name
        : string.Format(CultureInfo.CurrentCulture, "{0}. {1}", Name, Description);
}
```

- [ ] **Шаг 4: Написать модель экрана**

`src/WindowsPeace.Setup/Pages/RecipePickerViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using WindowsPeace.Core.Media;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран «что ставим». Ничего не выбирает за человека даже тогда, когда
/// рецепт на носителе единственный: он должен видеть, что именно ставит.
/// </summary>
public sealed class RecipePickerViewModel : ViewModelBase, IWizardPage
{
    private RecipeRowViewModel? _selectedRow;

    public RecipePickerViewModel(MediaManifestResult result)
    {
        Recipes = new ObservableCollection<RecipeRowViewModel>();

        switch (result.Status)
        {
            case MediaManifestStatus.Ok:
                foreach (var recipe in result.Manifest!.Recipes)
                {
                    Recipes.Add(new RecipeRowViewModel(recipe));
                }

                Trouble = string.Empty;
                break;

            default:
                Trouble = result.Message;
                break;
        }
    }

    private RecipePickerViewModel(string trouble)
    {
        Recipes = new ObservableCollection<RecipeRowViewModel>();
        Trouble = trouble;
    }

    /// <summary>Носитель не найден ни на одном разделе. Перечисляем, где искали.</summary>
    public static RecipePickerViewModel WithoutMedia(IReadOnlyList<string> checkedRoots)
        => new(string.Format(
            CultureInfo.CurrentCulture,
            "Носитель Windows Peace не найден. Файл описи «{0}» искали в корне каждого раздела: {1}.",
            MediaLocation.ManifestFileName,
            string.Join(", ", checkedRoots)));

    public string Title => "Что ставим?";

    public ObservableCollection<RecipeRowViewModel> Recipes { get; }

    /// <summary>Пусто, когда всё в порядке. Иначе — объяснение для человека.</summary>
    public string Trouble { get; }

    public bool HasTrouble => !string.IsNullOrEmpty(Trouble);

    public RecipeRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (Set(ref _selectedRow, value))
            {
                Raise(nameof(SelectedRecipe));
                CanGoNextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public MediaRecipe? SelectedRecipe => _selectedRow?.Recipe;

    public bool CanGoNext => _selectedRow is not null;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
    }
}
```

- [ ] **Шаг 5: Написать разметку**

`src/WindowsPeace.Setup/Pages/RecipePickerPage.xaml`:

```xml
<UserControl x:Class="WindowsPeace.Setup.Pages.RecipePickerPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="{Binding Trouble}"
                   TextWrapping="Wrap"
                   Margin="0,0,0,12"
                   Visibility="{Binding HasTrouble, Converter={StaticResource BoolToVisibility}}" />

        <ListView Grid.Row="1"
                  ItemsSource="{Binding Recipes}"
                  SelectedItem="{Binding SelectedRow, Mode=TwoWay}"
                  IsSynchronizedWithCurrentItem="False">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Рецепт" Width="260" DisplayMemberBinding="{Binding Name}" />
                    <GridViewColumn Header="Что это" Width="420" DisplayMemberBinding="{Binding Description}" />
                    <GridViewColumn Header="Образ" Width="200" DisplayMemberBinding="{Binding Image}" />
                    <GridViewColumn Header="Размер" Width="100" DisplayMemberBinding="{Binding Size}" />
                </GridView>
            </ListView.View>
        </ListView>
    </Grid>
</UserControl>
```

`IsSynchronizedWithCurrentItem="False"` обязателен: без него список сам выбирает первую строку, и «Далее» оживает, хотя человек ничего не выбирал. Этот дефект уже ловили на шаге А.

`BoolToVisibility` — ресурс в `App.xaml`; если его там ещё нет, добавить `<BooleanToVisibilityConverter x:Key="BoolToVisibility" />` в `Application.Resources`.

- [ ] **Шаг 6: Подключить страницу**

В `App.xaml`, в `Application.Resources`:

```xml
<DataTemplate DataType="{x:Type pages:RecipePickerViewModel}">
    <pages:RecipePickerPage />
</DataTemplate>
```

В `App.xaml.cs` — собрать экран и поставить его первым:

```csharp
var textReader = new FileTextReader();
var media = forcedMedia is not null ? new MediaLocation(forcedMedia) : null;

var recipePicker = media is null
    ? RecipePickerViewModel.WithoutMedia(snapshot.VolumeRoots)
    : new RecipePickerViewModel(media.Load(textReader));

Checkpoint(log, "Опись носителя прочитана", recipePicker.Trouble);

var navigator = new WizardNavigator(new List<IWizardPage>
{
    recipePicker,
    diskPicker,
    new PlaceholderViewModel(),
});
```

Настоящий поиск носителя по дискам добавляется в задаче 11, когда будет известен выбранный диск: до перечисления дисков носитель искать не по чему.

- [ ] **Шаг 7: Прогнать тесты и посмотреть глазами**

```bash
dotnet test
```

```bash
artifacts\setup\WindowsPeace.Setup.exe --media D:\WindowsPeace-Stand\fake-media
```

Заранее положить в эту папку `windows-peace-media.json` с одним рецептом. Ожидается: первым открывается экран «Что ставим?», строка не выбрана, «Далее» выключена.

- [ ] **Шаг 8: Зафиксировать**

```bash
git add src test
git commit -m "Экран «Что ставим»: список рецептов с носителя"
```

---

## Задача 11: Экран 3 «Проверьте и подтвердите»

Последний экран, где можно отступить.

**Файлы:**
- Создать: `src/WindowsPeace.Setup/Pages/ConfirmViewModel.cs`, `ConfirmPage.xaml`, `ConfirmPage.xaml.cs`
- Изменить: `src/WindowsPeace.Setup/App.xaml`, `App.xaml.cs`
- Тесты: `test/WindowsPeace.Setup.Tests/ConfirmViewModelTests.cs`

**Берёт:** `RecipePickerViewModel.SelectedRecipe`, `DiskPickerViewModel` (выбранная цель, план разметки, предупреждения).

- [ ] **Шаг 1: Написать падающие тесты**

```csharp
using WindowsPeace.Core.Selection;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public sealed class ConfirmViewModelTests
{
    private static ConfirmViewModel Page(bool requireTyped = true) => new(
        recipeName: "Atlas 25H2 RU",
        diskModel: "ST1000DM010-2EP102",
        diskSummary: "931,5 ГБ, Sata HDD, серийный номер Z9A1B2C3",
        planSummary: "EFI 300 МБ · MSR 16 МБ · Windows 930,2 ГБ · Восстановление 1 ГБ",
        warnings: new[] { new PlanWarning(WarningKind.WindowsOnTarget, WarningSeverity.Important, "На цели установлена Windows.") },
        requireTypedConfirmation: requireTyped);

    [Fact]
    public void Пока_модель_не_введена_дальше_нельзя()
    {
        var page = Page();
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void Неверная_модель_не_открывает_дорогу()
    {
        var page = Page();
        page.TypedModel = "ST1000";
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void Верная_модель_открывает_дорогу_невзирая_на_регистр_и_пробелы()
    {
        var page = Page();
        page.TypedModel = "  st1000dm010-2ep102 ";
        Assert.True(page.CanGoNext);
    }

    [Fact]
    public void Когда_рецепт_не_требует_подтверждения_поле_не_показывается()
    {
        var page = Page(requireTyped: false);
        Assert.False(page.NeedsTypedConfirmation);
        Assert.True(page.CanGoNext);
    }

    [Fact]
    public void Все_предупреждения_показываются()
    {
        Assert.Single(Page().Warnings);
    }
}
```

- [ ] **Шаг 2: Убедиться, что тесты не собираются**

```bash
dotnet test test/WindowsPeace.Setup.Tests
```

- [ ] **Шаг 3: Написать модель экрана**

```csharp
using System;
using System.Collections.Generic;
using WindowsPeace.Core.Selection;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Сводка перед установкой. Подтверждение вводом модели диска — требование
/// раздела 8 архитектуры: инструмент раздаётся незнакомым людям и стирает
/// диски с их данными, поэтому последнее действие должно быть осознанным.
/// </summary>
public sealed class ConfirmViewModel : ViewModelBase, IWizardPage
{
    private readonly string _diskModel;
    private readonly bool _requireTypedConfirmation;
    private string _typedModel = string.Empty;

    public ConfirmViewModel(
        string recipeName,
        string diskModel,
        string diskSummary,
        string planSummary,
        IReadOnlyList<PlanWarning> warnings,
        bool requireTypedConfirmation)
    {
        RecipeName = recipeName;
        _diskModel = diskModel;
        DiskSummary = diskSummary;
        PlanSummary = planSummary;
        Warnings = warnings;
        _requireTypedConfirmation = requireTypedConfirmation;
    }

    public string Title => "Проверьте и подтвердите";

    public string RecipeName { get; }

    public string DiskModel => _diskModel;

    public string DiskSummary { get; }

    public string PlanSummary { get; }

    public IReadOnlyList<PlanWarning> Warnings { get; }

    public bool NeedsTypedConfirmation => _requireTypedConfirmation;

    public string TypedModel
    {
        get => _typedModel;
        set
        {
            if (Set(ref _typedModel, value))
            {
                CanGoNextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanGoNext => !_requireTypedConfirmation ||
        string.Equals(_typedModel.Trim(), _diskModel.Trim(), StringComparison.OrdinalIgnoreCase);

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
    }
}
```

- [ ] **Шаг 4: Написать разметку**

`src/WindowsPeace.Setup/Pages/ConfirmPage.xaml`:

```xml
<UserControl x:Class="WindowsPeace.Setup.Pages.ConfirmPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel>
        <TextBlock Text="Что ставим" FontWeight="Bold" Margin="0,0,0,4" />
        <TextBlock Text="{Binding RecipeName}" Margin="0,0,0,16" />

        <TextBlock Text="Куда ставим" FontWeight="Bold" Margin="0,0,0,4" />
        <TextBlock Text="{Binding DiskModel}" />
        <TextBlock Text="{Binding DiskSummary}" Margin="0,0,0,16" />

        <TextBlock Text="Что будет сделано" FontWeight="Bold" Margin="0,0,0,4" />
        <TextBlock Text="{Binding PlanSummary}" TextWrapping="Wrap" Margin="0,0,0,16" />

        <ItemsControl ItemsSource="{Binding Warnings}" Margin="0,0,0,16">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding Text}" TextWrapping="Wrap" Margin="0,0,0,4" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <StackPanel Visibility="{Binding NeedsTypedConfirmation, Converter={StaticResource BoolToVisibility}}">
            <TextBlock TextWrapping="Wrap" Margin="0,0,0,4">
                <Run Text="Чтобы продолжить, введите модель диска:" />
                <Run Text="{Binding DiskModel, Mode=OneWay}" FontWeight="Bold" />
            </TextBlock>
            <TextBox Text="{Binding TypedModel, UpdateSourceTrigger=PropertyChanged}" Width="360"
                     HorizontalAlignment="Left" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

`UpdateSourceTrigger=PropertyChanged` обязателен: без него кнопка оживёт только после ухода из поля.

- [ ] **Шаг 5: Собрать экран в App.xaml.cs**

`ConfirmViewModel` строится не при запуске, а при входе на страницу: до выбора диска подтверждать нечего. Простейший способ, не ломающий `WizardNavigator`, — собрать его в `OnEnter` через посредника, который знает предыдущие два экрана:

```csharp
/// <summary>
/// Собирает сводку в тот момент, когда на неё переходят: раньше выбранного
/// диска ещё нет. Оболочка при этом не знает о связях между экранами.
/// </summary>
public sealed class ConfirmPageHost : ViewModelBase, IWizardPage
{
    private readonly RecipePickerViewModel _recipes;
    private readonly DiskPickerViewModel _disks;
    private ConfirmViewModel? _current;

    public ConfirmPageHost(RecipePickerViewModel recipes, DiskPickerViewModel disks)
    {
        _recipes = recipes;
        _disks = disks;
    }

    public string Title => "Проверьте и подтвердите";

    public ConfirmViewModel? Current => _current;

    public bool CanGoNext => _current?.CanGoNext ?? false;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        var target = _disks.SelectedTarget;
        var disk = target?.Disk;

        _current = new ConfirmViewModel(
            recipeName: _recipes.SelectedRecipe?.Name ?? "рецепт не выбран",
            diskModel: disk?.Identity.Model ?? string.Empty,
            diskSummary: _disks.SelectedSummary ?? string.Empty,
            planSummary: _disks.PlanSummary ?? string.Empty,
            warnings: _disks.Warnings,
            requireTypedConfirmation: true);

        _current.CanGoNextChanged += (_, _) => CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        Raise(nameof(Current));
        CanGoNextChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

Имена `SelectedTarget`, `SelectedSummary`, `PlanSummary`, `Warnings` взять из существующего `DiskPickerViewModel`; если там они называются иначе — использовать тамошние, а не переименовывать рабочий экран. `requireTypedConfirmation` пока задан постоянным `true`; чтение его из рецепта появится на шаге В вместе с разбором рецепта.

- [ ] **Шаг 6: Прогнать тесты и посмотреть глазами**

```bash
dotnet test
```

Ожидается: пять новых тестов проходят; в окне после выбора диска открывается сводка, «Далее» выключена, пока не введена модель.

- [ ] **Шаг 7: Зафиксировать**

```bash
git add src test
git commit -m "Экран подтверждения: сводка и ввод модели диска руками"
```

---

## Задача 12: Экраны 4 и 5, полноэкранный режим и шрифты

**Файлы:**
- Создать: `src/WindowsPeace.Setup/Pages/ProgressViewModel.cs` + `.xaml`, `DoneViewModel.cs` + `.xaml`
- Изменить: `src/WindowsPeace.Setup/Shell/ShellWindow.xaml`, `ShellWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`
- Удалить: `src/WindowsPeace.Setup/Pages/PlaceholderPage.xaml` и его модель — их место занимают экраны 4 и 5

- [ ] **Шаг 1: Написать оба каркасных экрана**

```csharp
using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран хода установки. Каркас: устройство то же, что понадобится на шаге В,
/// но полоска не рисуется, пока за ней нет настоящей работы. Поддельный
/// прогресс убедителен ровно до того дня, когда о его ненастоящести забудут.
/// </summary>
public sealed class ProgressViewModel : IWizardPage
{
    public string Title => "Установка";

    public string Explanation =>
        "Здесь пойдёт разметка диска, распаковка Windows, установка драйверов и загрузчика. " +
        "Это появится на шаге В. Сейчас программа ничего не записывает на диск.";

    public bool CanGoNext => true;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter() => CanGoNextChanged?.Invoke(this, EventArgs.Empty);
}
```

```csharp
using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>Экран завершения. Каркас.</summary>
public sealed class DoneViewModel : IWizardPage
{
    public DoneViewModel(string logDirectory) => LogDirectory = logDirectory;

    public string Title => "Готово";

    public string LogDirectory { get; }

    public string Explanation =>
        "Здесь будет итог установки и кнопка перезагрузки. Журнал работы лежит рядом с приложением.";

    public bool CanGoNext => false;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
    }
}
```

Разметка обоих — `TextBlock` с `Explanation` и, у экрана 5, строка с `LogDirectory`. Шаблоны страниц добавить в `App.xaml` рядом с остальными.

- [ ] **Шаг 2: Собрать порядок из пяти экранов**

```csharp
var navigator = new WizardNavigator(new List<IWizardPage>
{
    recipePicker,
    diskPicker,
    new ConfirmPageHost(recipePicker, diskPicker),
    new ProgressViewModel(),
    new DoneViewModel(location.IsAvailable ? location.Directory : "журнал не ведётся"),
});
```

- [ ] **Шаг 3: Полноэкранный режим в WinPE и цепочка шрифтов**

В `ShellWindow.xaml` заменить строку окна на:

```xml
Title="Windows Peace" Height="720" Width="1024"
WindowStartupLocation="CenterScreen"
FontFamily="Segoe UI, Tahoma, Microsoft Sans Serif"
```

Цепочка нужна потому, что обычного начертания Segoe UI в образе WinPE нет — проверено чтением образа. Tahoma и Microsoft Sans Serif там есть, и обе с кириллицей.

В `ShellWindow.xaml.cs`:

```csharp
public ShellWindow(bool fullScreen)
{
    InitializeComponent();

    if (fullScreen)
    {
        // В WinPE нет рабочего стола и панели задач: окно в рамке посреди
        // чёрного экрана выглядит поломкой. Установщик Windows ведёт себя так же.
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
    }
}
```

В `App.xaml.cs` — `new ShellWindow(snapshot.IsWindowsPe)`.

- [ ] **Шаг 4: Прогнать всё и посмотреть глазами**

```bash
dotnet build && dotnet test
```

Ожидается: сборка без предупреждений, все тесты проходят, в окне пять экранов и переходы между ними работают.

- [ ] **Шаг 5: Зафиксировать**

```bash
git add src test
git commit -m "Экраны хода установки и завершения, полноэкранный режим и цепочка шрифтов"
```

---

## Задача 13: Опыт 3 — самозапуск

**Файлы:**
- Создать: `tools/Media/Patch-BootWim.ps1`, `tools/Media/winpeshl.ini`, `tools/Media/peace-launch.cmd`

- [ ] **Шаг 1: Написать файлы, которые лягут в образ**

`tools/Media/winpeshl.ini`:

```ini
[LaunchApps]
%SYSTEMROOT%\System32\wpeinit.exe
%SYSTEMROOT%\System32\peace-launch.cmd
```

`tools/Media/peace-launch.cmd`:

```bat
@echo off
rem Носитель ищется по описи, а не по букве: буквы в WinPE непостоянны.
set PEACE=
for %%d in (C D E F G H I J K L M N O P Q R S T U V W Y Z) do (
    if exist %%d:\windows-peace-media.json set PEACE=%%d:
)

if "%PEACE%"=="" (
    echo Носитель Windows Peace не найден ни на одном разделе.
    echo Ищется файл windows-peace-media.json в корне раздела.
    cmd.exe
    exit /b 1
)

echo Носитель найден: %PEACE%
"%PEACE%\WindowsPeace\WindowsPeace.Setup.exe"

rem Обратно в командную строку, а не в перезагрузку: winpeshl считает выход
rem последнего приложения концом работы и перезагружает машину, унося экран.
cmd.exe
```

- [ ] **Шаг 2: Написать правку образа**

`tools/Media/Patch-BootWim.ps1`:

```powershell
[CmdletBinding()]
param(
    [string] $WimPath = 'D:\WindowsPeace-Source\sources\boot.wim',
    [int]    $Index = 2,
    [string] $MountPath = 'D:\WindowsPeace-Stand\mount',
    [uint32] $ScratchSpaceMb = 512
)
$ErrorActionPreference = 'Stop'

$p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Нужны права администратора: DISM без них образ не монтирует.'
}

New-Item -ItemType Directory -Force -Path $MountPath | Out-Null
$here = Split-Path -Parent $PSCommandPath

Write-Host "Монтируем индекс $Index из $WimPath ..."
dism /English /Mount-Wim /WimFile:$WimPath /Index:$Index /MountDir:$MountPath
if ($LASTEXITCODE -ne 0) { throw "Mount-Wim вернул $LASTEXITCODE" }

try {
    Copy-Item (Join-Path $here 'winpeshl.ini')     (Join-Path $MountPath 'Windows\System32\winpeshl.ini') -Force
    Copy-Item (Join-Path $here 'peace-launch.cmd') (Join-Path $MountPath 'Windows\System32\peace-launch.cmd') -Force

    dism /English /Image:$MountPath /Set-ScratchSpace:$ScratchSpaceMb
    if ($LASTEXITCODE -ne 0) { throw "Set-ScratchSpace вернул $LASTEXITCODE" }

    dism /English /Image:$MountPath /Get-ScratchSpace
}
finally {
    Write-Host 'Размонтируем с сохранением ...'
    dism /English /Unmount-Wim /MountDir:$MountPath /Commit
    if ($LASTEXITCODE -ne 0) { throw "Unmount-Wim вернул $LASTEXITCODE — образ остался смонтированным, разбирайся вручную." }
}
Write-Host 'Образ правлен.' -ForegroundColor Green
```

- [ ] **Шаг 3: Править образ и пересобрать носитель**

```bash
powershell -File tools/Media/Patch-BootWim.ps1
```

```bash
powershell -File tools/Media/Build-PeaceMedia.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx -AppFolder artifacts\setup -SkipInstallWim
```

- [ ] **Шаг 4: Провести опыт**

```bash
powershell -Command "Start-VM -Name 'Windows Peace Stand'; Start-Sleep -Seconds 60; & 'tools/Media/Get-PeaceVmScreen.ps1' -OutPath D:\WindowsPeace-Stand\selfstart.png"
```

Ожидается: на снимке мастер Windows Peace, экран «Что ставим?», без всякой командной строки.

Если вместо мастера открылся установщик Windows — `winpeshl.ini` не перекрыл запуск. Тогда второй приём: смонтировать образ, загрузить куст `HKLM\SYSTEM` из `<mount>\Windows\System32\config\SYSTEM` командой `reg load`, заменить значение `CmdLine` в ключе `Setup` на `peace-launch.cmd`, выгрузить куст, размонтировать с сохранением. Это записано в спеке как известное неизвестное, раздел 14.

- [ ] **Шаг 5: Записать результат**

Дописать третий опыт в заметку: снимок экрана, какой приём сработал, сколько занял старт.

- [ ] **Шаг 6: Зафиксировать**

```bash
git add tools/Media docs
git commit -m "Опыт 3: мастер стартует сам, установщик Windows подменён"
```

---

## Задача 14: Проход на настоящей флешке и приёмка

- [ ] **Шаг 1: Собрать настоящую флешку**

Узнать номер и модель диска:

```bash
powershell -Command "Get-Disk | Where-Object BusType -eq 'USB' | Format-Table Number,FriendlyName,Size"
```

Собрать, назвав модель точно как показано, — иначе скрипт откажется:

```bash
powershell -File tools/Media/Build-PeaceMedia.ps1 -UsbDiskNumber 2 -ConfirmModel "VendorC ProductCode" -AppFolder artifacts\setup
```

Здесь `install.wim` копируется полностью: это будущий рабочий носитель.

- [ ] **Шаг 2: Проверить флешку глазами до перезагрузки**

```bash
powershell -Command "Get-Partition -DiskNumber 2 | Format-Table PartitionNumber,GptType,DriveLetter,Size; Get-PSDrive -PSProvider FileSystem | Format-Table Name,Used,Free"
```

Ожидается: два раздела, у первого тип `{c12a7328-…}` и буквы нет; в проводнике один новый диск с меткой «Windows Peace».

- [ ] **Шаг 3: Загрузиться с флешки** — это делает автор

Перед перезагрузкой предупредить его о двух вещах: если на `C:` включено шифрование BitLocker, держать под рукой ключ восстановления, потому что смена порядка загрузки в прошивке иногда вызывает запрос ключа; и что мастер на этом шаге ничего на диски не пишет.

Проверить по списку приёмки из спеки, раздел 12, все двенадцать пунктов. Экран сфотографировать.

- [ ] **Шаг 4: Забрать журнал с флешки и разобрать**

Вернувшись в обычную Windows, прочитать `\WindowsPeace\logs\windows-peace.jsonl` с флешки: контрольные точки старта, снимок среды, время перечисления дисков, расход памяти.

- [ ] **Шаг 5: Написать запись о приёмке**

`docs/superpowers/notes/2026-08-14-step-b-acceptance.md` по образцу приёмки шага А: таблица из двенадцати пунктов, фотография экрана, выдержки из журнала, что нашлось и что исправлено.

- [ ] **Шаг 6: Обновить документы проекта**

- [ROADMAP.md](../../ROADMAP.md), раздел «Б»: отметить сделанным, записать, какой из трёх путей сработал.
- [ARCHITECTURE.md](../../ARCHITECTURE.md), раздел 6: заменить «решается опытом на шаге Б» на ответ; в раздел 5 внести решение об устройстве носителя; добавить требование к памяти.
- [HANDOFF.md](../../HANDOFF.md): переписать под следующую сессию и шаг В.

- [ ] **Шаг 7: Слить и отправить**

```bash
git add docs
git commit -m "Шаг Б принят: мастер работает в WinPE"
```

```bash
git checkout main && git merge --no-ff step-b-winpe && git push origin main
```

---

## Самопроверка плана

Пройдено после написания.

**Покрытие спеки.** Разделы 4 и 6 — задачи 2 и 13. Раздел 5 — задачи 7, 8, 9. Раздел 7 — задачи 10, 11, 12. Раздел 8 — задачи 3 и 12 (память, безопасная загрузка, шрифты, полный экран). Раздел 9 — задача 5. Раздел 10 — задачи 5, 8, 9, 12. Раздел 11 — задачи 5, 8, 10, 13. Раздел 12 — задачи 1, 3, 4, 6, 14. Раздел 13 — задачи 4 и 6, шаги разбора исхода. Раздел 14 — задача 13, шаг 4.

**Согласованность имён.** `MediaManifestReader.Read` → `MediaManifestResult` → `RecipePickerViewModel(MediaManifestResult)`; `MediaLocation.ManifestFileName` — единственное место, где живёт имя файла описи, `BootMediaLocator` ссылается на него; `LogLocationResolver.Resolve` → `LogLocation.Directory` → `DoneViewModel(logDirectory)`; `HostEnvironment.Describe` → `EnvironmentSnapshot.IsWindowsPe` → `ShellWindow(fullScreen)`.

**Два места, где исполнитель обязан посмотреть в существующий код, а не поверить плану:** имена свойств `DiskPickerViewModel` в задаче 11 и наличие `BooleanToVisibilityConverter` в `App.xaml` в задаче 10. Оба помечены прямо в шагах.
