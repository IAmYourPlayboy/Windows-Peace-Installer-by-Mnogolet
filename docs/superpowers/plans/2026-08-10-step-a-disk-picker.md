# Шаг А: оболочка мастера и экран выбора диска — план реализации

> **Для агента-исполнителя:** ОБЯЗАТЕЛЬНЫЙ СУБ-СКИЛЛ: используй `superpowers:subagent-driven-development` (рекомендуется) либо `superpowers:executing-plans` для выполнения задача за задачей. Шаги размечены чекбоксами (`- [ ]`).

**Цель:** приложение под обычную Windows, которое показывает настоящие диски и разделы машины, позволяет выбрать цель установки и показывает предпросмотр разметки — не производя ни одной операции записи.

**Устройство:** вся логика в библиотеке `WindowsPeace.Core` без интерфейса; окно `WindowsPeace.Setup` — тонкая надстройка WPF. Перечисление дисков спрятано за интерфейсом `IDiskEnumerator`, поэтому правила выбора проверяются тестами на слепках, а не на живом железе.

**Технологии:** C# 12, .NET 8 (WPF, самодостаточная публикация), `System.Management` для WMI, xUnit для тестов. Visual Studio не требуется — всё через `dotnet` CLI.

**Спека:** [../specs/2026-08-10-disk-picker-design.md](../specs/2026-08-10-disk-picker-design.md). Архитектура: [../../ARCHITECTURE.md](../../ARCHITECTURE.md).

## Общие ограничения

Действуют для всех задач без исключения.

- Корень репозитория: `D:\Projects\WindowsPeace`. Все пути в плане — от корня.
- `WindowsPeace.Core` и тесты собираются под **две** цели: `net48;net8.0-windows`. `WindowsPeace.Setup` — только `net8.0-windows`.
- В `Core` запрещено пользоваться тем, чего нет в .NET Framework 4.8. Нарушение ловится сборкой под `net48`.
- `TreatWarningsAsErrors` включён везде. Предупреждение — это ошибка сборки.
- **Пустой `catch` запрещён.** Правило анализатора, а не договорённость.
- Ни одного вызова наружу без предельного времени и признака отмены.
- Ни одной операции записи на диск: шаг А только читает.
- Язык интерфейса и сообщений об ошибках — русский.
- Коммит после каждой задачи, сообщение на русском, в повелительном наклонении.
- Тесты пишутся до реализации. Сначала красный, потом зелёный.

---

## Карта файлов

| Файл | Ответственность |
|---|---|
| `WindowsPeace.sln` | решение |
| `Directory.Build.props` | общие свойства сборки для всех проектов |
| `.editorconfig` | правила анализатора, включая запрет пустого `catch` |
| `src/WindowsPeace.Core/Polyfills/IsExternalInit.cs` | поддержка `init` под `net48` |
| `src/WindowsPeace.Core/Diagnostics/Timeouts.cs` | предельные времена в одном месте |
| `src/WindowsPeace.Core/Diagnostics/IOperationLog.cs` | приёмник записей журнала |
| `src/WindowsPeace.Core/Diagnostics/OperationScope.cs` | область с замером времени и гарантированной записью |
| `src/WindowsPeace.Core/Diagnostics/JsonLinesOperationLog.cs` | журнал в файл, одна запись — одна строка JSON |
| `src/WindowsPeace.Core/Storage/BusType.cs` | шина диска |
| `src/WindowsPeace.Core/Storage/MediaKind.cs` | тип носителя |
| `src/WindowsPeace.Core/Storage/PartitionKind.cs` | назначение раздела и разбор GUID типа GPT |
| `src/WindowsPeace.Core/Storage/IdentityConfidence.cs` | уровень доверия к отпечатку |
| `src/WindowsPeace.Core/Storage/DiskIdentity.cs` | отпечаток диска и цепочка поиска серийного номера |
| `src/WindowsPeace.Core/Storage/VolumeInfo.cs` | том: файловая система, метка, занято |
| `src/WindowsPeace.Core/Storage/PartitionInfo.cs` | раздел |
| `src/WindowsPeace.Core/Storage/FreeSpaceInfo.cs` | незанятый промежуток |
| `src/WindowsPeace.Core/Storage/DiskInfo.cs` | диск целиком |
| `src/WindowsPeace.Core/Storage/DiskSnapshot.cs` | результат перечисления: диски и сбои |
| `src/WindowsPeace.Core/Storage/FreeSpaceCalculator.cs` | вычисление промежутков между разделами |
| `src/WindowsPeace.Core/Storage/IDiskEnumerator.cs` | перечисление дисков |
| `src/WindowsPeace.Core/Storage/WmiDiskEnumerator.cs` | реализация через WMI |
| `src/WindowsPeace.Core/Storage/IDiskContentInspector.cs` | что лежит на разделе |
| `src/WindowsPeace.Core/Storage/FileSystemContentInspector.cs` | реализация через файловую систему |
| `src/WindowsPeace.Core/Storage/BootMediaLocator.cs` | поиск загрузочного носителя по описи |
| `src/WindowsPeace.Core/Selection/DeploymentLayout.cs` | размеры разделов, значения по умолчанию из схемы |
| `src/WindowsPeace.Core/Selection/SelectionTarget.cs` | что выбрано: диск, раздел или промежуток |
| `src/WindowsPeace.Core/Selection/SelectionVerdict.cs` | можно ли выбрать и почему нет |
| `src/WindowsPeace.Core/Selection/SelectionRules.cs` | правила выбора и запретов |
| `src/WindowsPeace.Core/Selection/PlanWarning.cs` | предупреждение с причиной |
| `src/WindowsPeace.Core/Selection/DeploymentPlan.cs` | предпросмотр разметки |
| `src/WindowsPeace.Core/Selection/DeploymentPlanner.cs` | построение предпросмотра |
| `src/WindowsPeace.Setup/Infrastructure/ViewModelBase.cs` | уведомления об изменении свойств |
| `src/WindowsPeace.Setup/Infrastructure/RelayCommand.cs` | команда для кнопок |
| `src/WindowsPeace.Setup/Shell/IWizardPage.cs` | страница мастера |
| `src/WindowsPeace.Setup/Shell/WizardNavigator.cs` | переходы между страницами |
| `src/WindowsPeace.Setup/Shell/ShellViewModel.cs` | состояние оболочки |
| `src/WindowsPeace.Setup/Shell/ShellWindow.xaml` | окно оболочки |
| `src/WindowsPeace.Setup/Pages/DiskPickerViewModel.cs` | состояние экрана дисков |
| `src/WindowsPeace.Setup/Pages/DiskRowViewModel.cs` | строка списка |
| `src/WindowsPeace.Setup/Pages/DiskPickerPage.xaml` | разметка экрана дисков |
| `src/WindowsPeace.Setup/Pages/PlaceholderPage.xaml` | заглушка следующего шага |
| `test/WindowsPeace.Core.Tests/**` | тесты и слепки |

---

## Задача 1: Каркас решения

**Файлы:**
- Создать: `Directory.Build.props`, `.editorconfig`, `WindowsPeace.sln`
- Создать: `src/WindowsPeace.Core/WindowsPeace.Core.csproj`
- Создать: `src/WindowsPeace.Core/Polyfills/IsExternalInit.cs`
- Создать: `src/WindowsPeace.Setup/WindowsPeace.Setup.csproj`
- Создать: `test/WindowsPeace.Core.Tests/WindowsPeace.Core.Tests.csproj`
- Тест: `test/WindowsPeace.Core.Tests/BuildSmokeTests.cs`

**Интерфейсы:**
- Отдаёт дальше: три собирающихся проекта, команда `dotnet test` проходит под обеими целями.

- [ ] **Шаг 1: Создать `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NeutralLanguage>ru-RU</NeutralLanguage>
    <Company>Windows Peace</Company>
  </PropertyGroup>
</Project>
```

- [ ] **Шаг 2: Создать `.editorconfig` с запретом пустого `catch`**

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
charset = utf-8-bom
end_of_line = crlf

# Пустой catch — дефект. См. docs/ARCHITECTURE.md, раздел 9.
dotnet_diagnostic.CA1031.severity = warning
dotnet_diagnostic.RCS1075.severity = error
dotnet_diagnostic.CS0168.severity = error
dotnet_diagnostic.CS0219.severity = error
```

- [ ] **Шаг 3: Создать проект `WindowsPeace.Core`**

Файл `src/WindowsPeace.Core/WindowsPeace.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
    <RootNamespace>WindowsPeace.Core</RootNamespace>
    <AssemblyName>WindowsPeace.Core</AssemblyName>
    <UseWindowsForms>false</UseWindowsForms>
  </PropertyGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net48'">
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <Reference Include="System.Management" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
    <PackageReference Include="System.Management" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Roslynator.Analyzers" Version="4.12.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Шаг 4: Создать полифилл для `init` под net48**

Файл `src/WindowsPeace.Core/Polyfills/IsExternalInit.cs`:

```csharp
#if NETFRAMEWORK
using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Нужен компилятору для свойств с init-сеттером. В .NET Framework отсутствует,
    /// поэтому объявляется здесь. Под .NET 8 берётся из среды выполнения.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
#endif
```

- [ ] **Шаг 5: Создать проект `WindowsPeace.Setup`**

Файл `src/WindowsPeace.Setup/WindowsPeace.Setup.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RootNamespace>WindowsPeace.Setup</RootNamespace>
    <AssemblyName>WindowsPeace.Setup</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WindowsPeace.Core\WindowsPeace.Core.csproj" />
  </ItemGroup>
</Project>
```

Файл `src/WindowsPeace.Setup/app.manifest` — без запроса повышения, шаг А ничего не пишет:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

- [ ] **Шаг 6: Создать тестовый проект**

Файл `test/WindowsPeace.Core.Tests/WindowsPeace.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
    <IsPackable>false</IsPackable>
    <RootNamespace>WindowsPeace.Core.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net48'">
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\WindowsPeace.Core\WindowsPeace.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Шаг 7: Написать проверочный тест**

Файл `test/WindowsPeace.Core.Tests/BuildSmokeTests.cs`:

```csharp
using Xunit;

namespace WindowsPeace.Core.Tests;

public class BuildSmokeTests
{
    [Fact]
    public void Сборка_ядра_доступна_из_тестов()
    {
        var assembly = typeof(WindowsPeace.Core.Polyfills.AssemblyMarker).Assembly;
        Assert.Equal("WindowsPeace.Core", assembly.GetName().Name);
    }
}
```

Файл `src/WindowsPeace.Core/Polyfills/AssemblyMarker.cs`:

```csharp
namespace WindowsPeace.Core.Polyfills;

/// <summary>Опорный тип для поиска сборки из тестов.</summary>
public static class AssemblyMarker
{
}
```

- [ ] **Шаг 8: Собрать решение и запустить тест**

```bash
dotnet new sln -n WindowsPeace
```

Затем:

Выполнить: `dotnet sln add src/WindowsPeace.Core/WindowsPeace.Core.csproj src/WindowsPeace.Setup/WindowsPeace.Setup.csproj test/WindowsPeace.Core.Tests/WindowsPeace.Core.Tests.csproj`

Выполнить: `dotnet test`
Ожидается: PASS, тест выполнен дважды — под `net48` и под `net8.0-windows`.

Если `net48` не собирается из-за отсутствия пакета таргетинга — проверить, что восстановление NuGet прошло: `dotnet restore`. Интернет нужен один раз.

- [ ] **Шаг 9: Коммит**

```bash
git add -A
git commit -m "Каркас решения: три проекта, две цели сборки, запрет пустого catch"
```

---

## Задача 2: Перечисления и отпечаток диска

**Файлы:**
- Создать: `src/WindowsPeace.Core/Storage/BusType.cs`, `MediaKind.cs`, `IdentityConfidence.cs`, `DiskIdentity.cs`
- Тест: `test/WindowsPeace.Core.Tests/DiskIdentityTests.cs`

**Интерфейсы:**
- Отдаёт дальше: `DiskIdentity.Create(string? physicalSerial, string? diskSerial, string? win32Serial, string? uniqueId, string? gptGuid, string model, ulong sizeBytes, BusType bus)` возвращает `DiskIdentity` со свойствами `SerialNumber`, `Source`, `Confidence`, `Model`, `SizeBytes`, `BusType`.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/DiskIdentityTests.cs`:

```csharp
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class DiskIdentityTests
{
    private static DiskIdentity Create(
        string? physicalSerial = null,
        string? diskSerial = null,
        string? win32Serial = null,
        string? uniqueId = null,
        string? gptGuid = null)
        => DiskIdentity.Create(physicalSerial, diskSerial, win32Serial, uniqueId, gptGuid,
            model: "Тестовый диск", sizeBytes: 500_000_000_000UL, busType: BusType.Nvme);

    [Fact]
    public void Серийник_физического_диска_имеет_наивысший_приоритет()
    {
        var id = Create(physicalSerial: "PHYS1", diskSerial: "DISK1", win32Serial: "WIN1");

        Assert.Equal("PHYS1", id.SerialNumber);
        Assert.Equal(IdentitySource.PhysicalDisk, id.Source);
        Assert.Equal(IdentityConfidence.Hardware, id.Confidence);
    }

    [Fact]
    public void Пустой_серийник_пропускается_и_берётся_следующий()
    {
        var id = Create(physicalSerial: "   ", diskSerial: "DISK1");

        Assert.Equal("DISK1", id.SerialNumber);
        Assert.Equal(IdentitySource.Disk, id.Source);
    }

    [Fact]
    public void Серийник_обрезается_по_краям()
    {
        var id = Create(physicalSerial: "  S/N-42  ");

        Assert.Equal("S/N-42", id.SerialNumber);
    }

    [Fact]
    public void При_отсутствии_серийников_берётся_UniqueId_и_доверие_остаётся_аппаратным()
    {
        var id = Create(uniqueId: "600508B1001C...");

        Assert.Equal("600508B1001C...", id.SerialNumber);
        Assert.Equal(IdentitySource.UniqueId, id.Source);
        Assert.Equal(IdentityConfidence.Hardware, id.Confidence);
    }

    [Fact]
    public void GUID_разметки_даёт_только_временное_доверие()
    {
        var id = Create(gptGuid: "{7b2c9f1e-0000-0000-0000-000000000001}");

        Assert.Equal(IdentitySource.GptGuid, id.Source);
        Assert.Equal(IdentityConfidence.Volatile, id.Confidence);
    }

    [Fact]
    public void Когда_нет_ничего_доверия_нет_а_отпечаток_всё_равно_создаётся()
    {
        var id = Create();

        Assert.Null(id.SerialNumber);
        Assert.Equal(IdentitySource.None, id.Source);
        Assert.Equal(IdentityConfidence.None, id.Confidence);
        Assert.Equal("Тестовый диск", id.Model);
    }

    [Fact]
    public void Режим_pinned_допустим_только_при_аппаратном_доверии()
    {
        Assert.True(Create(physicalSerial: "PHYS1").CanBePinned);
        Assert.False(Create(gptGuid: "{7b2c9f1e-0000-0000-0000-000000000001}").CanBePinned);
        Assert.False(Create().CanBePinned);
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter DiskIdentityTests`
Ожидается: FAIL, тип `DiskIdentity` не найден.

- [ ] **Шаг 3: Написать перечисления**

Файл `src/WindowsPeace.Core/Storage/BusType.cs` — числовые значения соответствуют свойству `BusType` класса WMI `MSFT_Disk`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Шина подключения диска. Значения совпадают с MSFT_Disk.BusType.</summary>
public enum BusType
{
    Unknown = 0,
    Scsi = 1,
    Atapi = 2,
    Ata = 3,
    Ieee1394 = 4,
    Ssa = 5,
    FibreChannel = 6,
    Usb = 7,
    Raid = 8,
    Iscsi = 9,
    Sas = 10,
    Sata = 11,
    Sd = 12,
    Mmc = 13,
    Max = 14,
    FileBackedVirtual = 15,
    StorageSpaces = 16,
    Nvme = 17,
}
```

Файл `src/WindowsPeace.Core/Storage/MediaKind.cs` — значения соответствуют `MSFT_PhysicalDisk.MediaType`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Тип носителя. Значения совпадают с MSFT_PhysicalDisk.MediaType.</summary>
public enum MediaKind
{
    Unspecified = 0,
    Hdd = 3,
    Ssd = 4,
    Scm = 5,
}
```

Файл `src/WindowsPeace.Core/Storage/IdentityConfidence.cs`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Откуда взят опознавательный признак диска.</summary>
public enum IdentitySource
{
    None = 0,
    PhysicalDisk,
    Disk,
    Win32DiskDrive,
    UniqueId,
    GptGuid,
}

/// <summary>
/// Насколько признаку можно верить. Определяет, годится ли диск
/// для режима pinned из рецепта: см. contract/recipe.schema.json, diskFingerprint.
/// </summary>
public enum IdentityConfidence
{
    /// <summary>Опознать нечем. Только выбор человеком, с предупреждением.</summary>
    None = 0,

    /// <summary>Признак меняется при переразметке. Годен внутри одного сеанса.</summary>
    Volatile,

    /// <summary>Признак принадлежит устройству и переживает переразметку.</summary>
    Hardware,
}
```

- [ ] **Шаг 4: Написать `DiskIdentity`**

Файл `src/WindowsPeace.Core/Storage/DiskIdentity.cs`:

```csharp
using System.Collections.Generic;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Отпечаток диска. Порядковый номер диска сюда не попадает намеренно:
/// он нестабилен между загрузками. См. docs/ARCHITECTURE.md, дефект A.
/// </summary>
public sealed class DiskIdentity
{
    private DiskIdentity(
        string? serialNumber,
        IdentitySource source,
        IdentityConfidence confidence,
        string model,
        ulong sizeBytes,
        BusType busType)
    {
        SerialNumber = serialNumber;
        Source = source;
        Confidence = confidence;
        Model = model;
        SizeBytes = sizeBytes;
        BusType = busType;
    }

    public string? SerialNumber { get; }
    public IdentitySource Source { get; }
    public IdentityConfidence Confidence { get; }
    public string Model { get; }
    public ulong SizeBytes { get; }
    public BusType BusType { get; }

    /// <summary>Годится ли диск для режима pinned из рецепта.</summary>
    public bool CanBePinned => Confidence == IdentityConfidence.Hardware;

    /// <summary>
    /// Собирает отпечаток, перебирая источники по убыванию надёжности.
    /// Первый непустой выигрывает.
    /// </summary>
    public static DiskIdentity Create(
        string? physicalDiskSerial,
        string? diskSerial,
        string? win32DiskDriveSerial,
        string? uniqueId,
        string? gptGuid,
        string model,
        ulong sizeBytes,
        BusType busType)
    {
        var candidates = new List<(string? Value, IdentitySource Source, IdentityConfidence Confidence)>
        {
            (physicalDiskSerial, IdentitySource.PhysicalDisk, IdentityConfidence.Hardware),
            (diskSerial, IdentitySource.Disk, IdentityConfidence.Hardware),
            (win32DiskDriveSerial, IdentitySource.Win32DiskDrive, IdentityConfidence.Hardware),
            (uniqueId, IdentitySource.UniqueId, IdentityConfidence.Hardware),
            (gptGuid, IdentitySource.GptGuid, IdentityConfidence.Volatile),
        };

        foreach (var candidate in candidates)
        {
            var trimmed = candidate.Value?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                return new DiskIdentity(trimmed, candidate.Source, candidate.Confidence, model, sizeBytes, busType);
            }
        }

        return new DiskIdentity(null, IdentitySource.None, IdentityConfidence.None, model, sizeBytes, busType);
    }
}
```

- [ ] **Шаг 5: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter DiskIdentityTests`
Ожидается: PASS, 7 тестов, каждый выполнен под обеими целями.

- [ ] **Шаг 6: Коммит**

```bash
git add -A
git commit -m "Отпечаток диска: цепочка источников и уровни доверия"
```

---

## Задача 3: Классификация разделов

**Файлы:**
- Создать: `src/WindowsPeace.Core/Storage/PartitionKind.cs`
- Тест: `test/WindowsPeace.Core.Tests/PartitionKindTests.cs`

**Интерфейсы:**
- Отдаёт дальше: перечисление `PartitionKind` и метод `PartitionKinds.FromGptType(string? gptType)`.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/PartitionKindTests.cs`:

```csharp
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class PartitionKindTests
{
    [Theory]
    [InlineData("{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}", PartitionKind.EfiSystem)]
    [InlineData("{e3c9e316-0b5c-4db8-817d-f92df00215ae}", PartitionKind.MicrosoftReserved)]
    [InlineData("{de94bba4-06d1-4d40-a16a-bfd50179d6ac}", PartitionKind.WindowsRecovery)]
    [InlineData("{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}", PartitionKind.BasicData)]
    public void Известные_типы_GPT_распознаются(string gptType, PartitionKind expected)
    {
        Assert.Equal(expected, PartitionKinds.FromGptType(gptType));
    }

    [Fact]
    public void Регистр_и_фигурные_скобки_не_имеют_значения()
    {
        Assert.Equal(PartitionKind.EfiSystem, PartitionKinds.FromGptType("C12A7328-F81F-11D2-BA4B-00A0C93EC93B"));
    }

    [Fact]
    public void Неизвестный_тип_даёт_Unknown_а_не_исключение()
    {
        Assert.Equal(PartitionKind.Unknown, PartitionKinds.FromGptType("{00000000-0000-0000-0000-000000000000}"));
        Assert.Equal(PartitionKind.Unknown, PartitionKinds.FromGptType(null));
        Assert.Equal(PartitionKind.Unknown, PartitionKinds.FromGptType("мусор"));
    }

    [Theory]
    [InlineData(PartitionKind.EfiSystem, true)]
    [InlineData(PartitionKind.MicrosoftReserved, true)]
    [InlineData(PartitionKind.WindowsRecovery, true)]
    [InlineData(PartitionKind.BasicData, false)]
    [InlineData(PartitionKind.Unknown, false)]
    public void Служебные_разделы_помечаются_как_служебные(PartitionKind kind, bool expected)
    {
        Assert.Equal(expected, PartitionKinds.IsSystemService(kind));
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter PartitionKindTests`
Ожидается: FAIL, тип `PartitionKind` не найден.

- [ ] **Шаг 3: Написать реализацию**

Файл `src/WindowsPeace.Core/Storage/PartitionKind.cs`:

```csharp
using System;

namespace WindowsPeace.Core.Storage;

/// <summary>Назначение раздела, выведенное из типа GPT.</summary>
public enum PartitionKind
{
    Unknown = 0,
    EfiSystem,
    MicrosoftReserved,
    WindowsRecovery,
    BasicData,
}

/// <summary>Разбор типов GPT. Идентификаторы задокументированы Microsoft и не меняются.</summary>
public static class PartitionKinds
{
    private static readonly Guid EfiSystemGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid MicrosoftReservedGuid = new("e3c9e316-0b5c-4db8-817d-f92df00215ae");
    private static readonly Guid WindowsRecoveryGuid = new("de94bba4-06d1-4d40-a16a-bfd50179d6ac");
    private static readonly Guid BasicDataGuid = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");

    /// <summary>
    /// Переводит значение MSFT_Partition.GptType в назначение раздела.
    /// Неразобранное значение не считается ошибкой: диск мог быть размечен чем угодно.
    /// </summary>
    public static PartitionKind FromGptType(string? gptType)
    {
        if (string.IsNullOrWhiteSpace(gptType) || !Guid.TryParse(gptType, out var guid))
        {
            return PartitionKind.Unknown;
        }

        if (guid == EfiSystemGuid) return PartitionKind.EfiSystem;
        if (guid == MicrosoftReservedGuid) return PartitionKind.MicrosoftReserved;
        if (guid == WindowsRecoveryGuid) return PartitionKind.WindowsRecovery;
        if (guid == BasicDataGuid) return PartitionKind.BasicData;

        return PartitionKind.Unknown;
    }

    /// <summary>Служебный раздел — тот, который создаёт и обслуживает сама система.</summary>
    public static bool IsSystemService(PartitionKind kind)
        => kind is PartitionKind.EfiSystem or PartitionKind.MicrosoftReserved or PartitionKind.WindowsRecovery;
}
```

`Guid.TryParse` принимает форму как в фигурных скобках, так и без них, и не зависит от регистра — отдельной чистки строки не требуется.

- [ ] **Шаг 4: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter PartitionKindTests`
Ожидается: PASS, 11 случаев.

- [ ] **Шаг 5: Коммит**

```bash
git add -A
git commit -m "Классификация разделов по типу GPT"
```

---

## Задача 4: Журнал и предельные времена

**Файлы:**
- Создать: `src/WindowsPeace.Core/Diagnostics/Timeouts.cs`, `IOperationLog.cs`, `OperationScope.cs`, `JsonLinesOperationLog.cs`
- Тест: `test/WindowsPeace.Core.Tests/OperationScopeTests.cs`

**Интерфейсы:**
- Отдаёт дальше: `IOperationLog.Write(OperationRecord record)`; `OperationScope.Start(IOperationLog log, string component, string operation)` с методами `Success()`, `Failure(string reason)`, `TimedOut()`; статические поля `Timeouts.DiskEnumeration`, `Timeouts.SingleDiskProbe`, `Timeouts.ContentInspection`.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/OperationScopeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests;

internal sealed class RecordingLog : IOperationLog
{
    public List<OperationRecord> Records { get; } = new();

    public void Write(OperationRecord record) => Records.Add(record);
}

public class OperationScopeTests
{
    [Fact]
    public void Успешная_область_оставляет_запись_с_исходом_Success()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Перечисление дисков"))
        {
            scope.Success();
        }

        var record = Assert.Single(log.Records);
        Assert.Equal("Storage", record.Component);
        Assert.Equal("Перечисление дисков", record.Operation);
        Assert.Equal(OperationOutcome.Success, record.Outcome);
        Assert.Null(record.Reason);
    }

    [Fact]
    public void Область_без_явного_исхода_считается_прерванной()
    {
        var log = new RecordingLog();

        using (OperationScope.Start(log, "Storage", "Опрос диска"))
        {
        }

        var record = Assert.Single(log.Records);
        Assert.Equal(OperationOutcome.Abandoned, record.Outcome);
    }

    [Fact]
    public void Отказ_сохраняет_причину()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.Failure("WMI недоступно");
        }

        var record = Assert.Single(log.Records);
        Assert.Equal(OperationOutcome.Failure, record.Outcome);
        Assert.Equal("WMI недоступно", record.Reason);
    }

    [Fact]
    public void Истечение_времени_отличается_от_обычного_отказа()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.TimedOut();
        }

        Assert.Equal(OperationOutcome.TimedOut, Assert.Single(log.Records).Outcome);
    }

    [Fact]
    public void Длительность_измеряется_и_попадает_в_запись()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.Success();
        }

        Assert.True(Assert.Single(log.Records).Duration >= TimeSpan.Zero);
    }

    [Fact]
    public void Предельные_времена_заданы_явно_и_не_бесконечны()
    {
        Assert.True(Timeouts.DiskEnumeration > TimeSpan.Zero);
        Assert.True(Timeouts.DiskEnumeration < TimeSpan.FromMinutes(5));
        Assert.True(Timeouts.SingleDiskProbe > TimeSpan.Zero);
        Assert.True(Timeouts.ContentInspection > TimeSpan.Zero);
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter OperationScopeTests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 3: Написать предельные времена**

Файл `src/WindowsPeace.Core/Diagnostics/Timeouts.cs`:

```csharp
using System;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Предельные времена собраны в одном месте намеренно: рассыпанные по коду
/// значения невозможно ни просмотреть целиком, ни поменять разом.
/// См. docs/ARCHITECTURE.md, раздел 9.
/// </summary>
public static class Timeouts
{
    /// <summary>Полное перечисление дисков. WMI на сбойном контроллере умеет висеть минутами.</summary>
    public static readonly TimeSpan DiskEnumeration = TimeSpan.FromSeconds(30);

    /// <summary>Опрос одного диска. Изолирован, чтобы один сбойный не утянул остальные.</summary>
    public static readonly TimeSpan SingleDiskProbe = TimeSpan.FromSeconds(10);

    /// <summary>Проверка содержимого раздела через файловую систему.</summary>
    public static readonly TimeSpan ContentInspection = TimeSpan.FromSeconds(5);
}
```

- [ ] **Шаг 4: Написать журнал и область**

Файл `src/WindowsPeace.Core/Diagnostics/IOperationLog.cs`:

```csharp
using System;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>Чем закончилась операция.</summary>
public enum OperationOutcome
{
    /// <summary>Область закрыта без объявления исхода — это дефект в коде вызывающего.</summary>
    Abandoned = 0,
    Success,
    Failure,
    TimedOut,
}

/// <summary>Одна запись журнала. Плоская и машиночитаемая.</summary>
public sealed class OperationRecord
{
    public OperationRecord(
        DateTimeOffset startedAt,
        string component,
        string operation,
        TimeSpan duration,
        OperationOutcome outcome,
        string? reason)
    {
        StartedAt = startedAt;
        Component = component;
        Operation = operation;
        Duration = duration;
        Outcome = outcome;
        Reason = reason;
    }

    public DateTimeOffset StartedAt { get; }
    public string Component { get; }
    public string Operation { get; }
    public TimeSpan Duration { get; }
    public OperationOutcome Outcome { get; }
    public string? Reason { get; }
}

/// <summary>Приёмник записей журнала.</summary>
public interface IOperationLog
{
    void Write(OperationRecord record);
}
```

Файл `src/WindowsPeace.Core/Diagnostics/OperationScope.cs`:

```csharp
using System;
using System.Diagnostics;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Область выполнения операции. Замеряет время и гарантирует запись в журнал
/// даже тогда, когда вызывающий забыл объявить исход — такой случай отмечается
/// отдельным значением Abandoned, чтобы его было видно.
/// </summary>
public sealed class OperationScope : IDisposable
{
    private readonly IOperationLog _log;
    private readonly string _component;
    private readonly string _operation;
    private readonly DateTimeOffset _startedAt;
    private readonly Stopwatch _stopwatch;

    private OperationOutcome _outcome = OperationOutcome.Abandoned;
    private string? _reason;
    private bool _written;

    private OperationScope(IOperationLog log, string component, string operation)
    {
        _log = log;
        _component = component;
        _operation = operation;
        _startedAt = DateTimeOffset.Now;
        _stopwatch = Stopwatch.StartNew();
    }

    public static OperationScope Start(IOperationLog log, string component, string operation)
        => new(log, component, operation);

    public void Success() => Set(OperationOutcome.Success, reason: null);

    public void Failure(string reason) => Set(OperationOutcome.Failure, reason);

    public void TimedOut() => Set(OperationOutcome.TimedOut, "Превышено предельное время");

    private void Set(OperationOutcome outcome, string? reason)
    {
        _outcome = outcome;
        _reason = reason;
    }

    public void Dispose()
    {
        if (_written)
        {
            return;
        }

        _written = true;
        _stopwatch.Stop();
        _log.Write(new OperationRecord(_startedAt, _component, _operation, _stopwatch.Elapsed, _outcome, _reason));
    }
}
```

- [ ] **Шаг 5: Написать файловый журнал**

Файл `src/WindowsPeace.Core/Diagnostics/JsonLinesOperationLog.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Журнал в файл: одна запись — одна строка JSON. Формат выбран потому,
/// что такой файл читается и человеком, и разбором, и дописывается без перечитывания.
/// Собственная сериализация вместо библиотеки — чтобы под net48 не тянуть зависимость.
/// </summary>
public sealed class JsonLinesOperationLog : IOperationLog, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public JsonLinesOperationLog(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    /// <summary>Путь журнала по умолчанию: рядом с приложением, чтобы работало и в WinPE.</summary>
    public static string DefaultPath(string baseDirectory)
        => Path.Combine(baseDirectory, "logs", "windows-peace.jsonl");

    public void Write(OperationRecord record)
    {
        var line = new StringBuilder()
            .Append('{')
            .Append("\"time\":\"").Append(record.StartedAt.ToString("o", CultureInfo.InvariantCulture)).Append("\",")
            .Append("\"component\":\"").Append(Escape(record.Component)).Append("\",")
            .Append("\"operation\":\"").Append(Escape(record.Operation)).Append("\",")
            .Append("\"durationMs\":").Append(((long)record.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append("\"outcome\":\"").Append(record.Outcome).Append('"');

        if (record.Reason is not null)
        {
            line.Append(",\"reason\":\"").Append(Escape(record.Reason)).Append('"');
        }

        line.Append('}');

        lock (_gate)
        {
            _writer.WriteLine(line.ToString());
        }
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
```

- [ ] **Шаг 6: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter OperationScopeTests`
Ожидается: PASS, 6 тестов.

- [ ] **Шаг 7: Коммит**

```bash
git add -A
git commit -m "Журнал операций и предельные времена в одном месте"
```

---

## Задача 5: Модель дисков и вычисление промежутков

**Файлы:**
- Создать: `src/WindowsPeace.Core/Storage/VolumeInfo.cs`, `PartitionInfo.cs`, `FreeSpaceInfo.cs`, `DiskInfo.cs`, `DiskSnapshot.cs`, `FreeSpaceCalculator.cs`
- Тест: `test/WindowsPeace.Core.Tests/FreeSpaceCalculatorTests.cs`

**Интерфейсы:**
- Отдаёт дальше: `DiskInfo` со свойствами `Identity`, `Number`, `FriendlyName`, `Media`, `IsSystem`, `IsBoot`, `IsOffline`, `IsReadOnly`, `IsRemovable`, `PartitionStyle`, `Partitions` (`IReadOnlyList<PartitionInfo>`), `FreeSpaces` (`IReadOnlyList<FreeSpaceInfo>`), `ProbeError`; `DiskSnapshot` со свойствами `Disks` и `EnumerationError`; `FreeSpaceCalculator.Calculate(ulong diskSize, IReadOnlyList<PartitionInfo> partitions)`.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/FreeSpaceCalculatorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class FreeSpaceCalculatorTests
{
    private const ulong Mib = 1024UL * 1024UL;
    private const ulong Gib = 1024UL * Mib;

    private static PartitionInfo Partition(ulong offset, ulong size, int number = 1)
        => new(number, offset, size, PartitionKind.BasicData, driveLetter: null,
            isSystem: false, isHidden: false, volume: null);

    [Fact]
    public void Пустой_диск_даёт_один_промежуток_с_учётом_служебного_запаса()
    {
        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, new List<PartitionInfo>());

        var gap = Assert.Single(gaps);
        Assert.Equal(Mib, gap.Offset);
        Assert.Equal(100 * Gib - 2 * Mib, gap.Size);
    }

    [Fact]
    public void Промежуток_между_разделами_находится()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(Mib, 10 * Gib, 1),
            Partition(20 * Gib, 10 * Gib, 2),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.Contains(gaps, g => g.Offset == Mib + 10 * Gib && g.Size == 20 * Gib - (Mib + 10 * Gib));
    }

    [Fact]
    public void Хвост_после_последнего_раздела_находится()
    {
        var partitions = new List<PartitionInfo> { Partition(Mib, 10 * Gib, 1) };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        var tail = gaps.Last();
        Assert.Equal(Mib + 10 * Gib, tail.Offset);
        Assert.Equal(100 * Gib - Mib - (Mib + 10 * Gib), tail.Size);
    }

    [Fact]
    public void Промежутки_меньше_мегабайта_не_показываются()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(Mib, 10 * Gib, 1),
            Partition(Mib + 10 * Gib + 4096, 10 * Gib, 2),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.DoesNotContain(gaps, g => g.Size < Mib);
    }

    [Fact]
    public void Разделы_в_произвольном_порядке_обрабатываются_правильно()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(50 * Gib, 10 * Gib, 2),
            Partition(Mib, 10 * Gib, 1),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.Equal(2, gaps.Count);
        Assert.True(gaps[0].Offset < gaps[1].Offset);
    }

    [Fact]
    public void Заполненный_до_конца_диск_не_даёт_промежутков()
    {
        var partitions = new List<PartitionInfo> { Partition(Mib, 100 * Gib - 2 * Mib, 1) };

        Assert.Empty(FreeSpaceCalculator.Calculate(100 * Gib, partitions));
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter FreeSpaceCalculatorTests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 3: Написать модели**

Файл `src/WindowsPeace.Core/Storage/VolumeInfo.cs`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Том на разделе. Отсутствует, если раздел не смонтирован.</summary>
public sealed class VolumeInfo
{
    public VolumeInfo(string? fileSystem, string? label, ulong sizeBytes, ulong freeBytes)
    {
        FileSystem = fileSystem;
        Label = label;
        SizeBytes = sizeBytes;
        FreeBytes = freeBytes;
    }

    public string? FileSystem { get; }
    public string? Label { get; }
    public ulong SizeBytes { get; }
    public ulong FreeBytes { get; }
    public ulong UsedBytes => SizeBytes > FreeBytes ? SizeBytes - FreeBytes : 0UL;
}
```

Файл `src/WindowsPeace.Core/Storage/PartitionInfo.cs`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Что найдено на разделе. Заполняется отдельным проходом.</summary>
public sealed class PartitionContent
{
    public PartitionContent(bool windowsFound, string? windowsProductName, bool userFilesFound, bool inspected, string? notInspectedReason)
    {
        WindowsFound = windowsFound;
        WindowsProductName = windowsProductName;
        UserFilesFound = userFilesFound;
        Inspected = inspected;
        NotInspectedReason = notInspectedReason;
    }

    public static PartitionContent NotInspected(string reason) => new(false, null, false, false, reason);

    public bool WindowsFound { get; }
    public string? WindowsProductName { get; }
    public bool UserFilesFound { get; }
    public bool Inspected { get; }
    public string? NotInspectedReason { get; }
}

/// <summary>Раздел диска.</summary>
public sealed class PartitionInfo
{
    public PartitionInfo(
        int number,
        ulong offset,
        ulong size,
        PartitionKind kind,
        char? driveLetter,
        bool isSystem,
        bool isHidden,
        VolumeInfo? volume)
    {
        Number = number;
        Offset = offset;
        Size = size;
        Kind = kind;
        DriveLetter = driveLetter;
        IsSystem = isSystem;
        IsHidden = isHidden;
        Volume = volume;
        Content = PartitionContent.NotInspected("Содержимое ещё не проверялось");
    }

    public int Number { get; }
    public ulong Offset { get; }
    public ulong Size { get; }
    public ulong End => Offset + Size;
    public PartitionKind Kind { get; }
    public char? DriveLetter { get; }
    public bool IsSystem { get; }
    public bool IsHidden { get; }
    public VolumeInfo? Volume { get; }

    /// <summary>Заполняется инспектором содержимого. До этого — «не проверено».</summary>
    public PartitionContent Content { get; internal set; }
}
```

Файл `src/WindowsPeace.Core/Storage/FreeSpaceInfo.cs`:

```csharp
namespace WindowsPeace.Core.Storage;

/// <summary>Незанятый промежуток на диске. Не раздел: у него нет номера и файловой системы.</summary>
public sealed class FreeSpaceInfo
{
    public FreeSpaceInfo(ulong offset, ulong size)
    {
        Offset = offset;
        Size = size;
    }

    public ulong Offset { get; }
    public ulong Size { get; }
    public ulong End => Offset + Size;
}
```

Файл `src/WindowsPeace.Core/Storage/DiskInfo.cs`:

```csharp
using System.Collections.Generic;

namespace WindowsPeace.Core.Storage;

/// <summary>Стиль разметки диска. Значения совпадают с MSFT_Disk.PartitionStyle.</summary>
public enum PartitionStyle
{
    Unknown = 0,
    Mbr = 1,
    Gpt = 2,
}

/// <summary>Физический диск со всем, что о нём удалось выяснить.</summary>
public sealed class DiskInfo
{
    public DiskInfo(
        DiskIdentity identity,
        int number,
        string friendlyName,
        MediaKind media,
        PartitionStyle partitionStyle,
        bool isSystem,
        bool isBoot,
        bool isOffline,
        bool isReadOnly,
        bool isRemovable,
        IReadOnlyList<PartitionInfo> partitions,
        IReadOnlyList<FreeSpaceInfo> freeSpaces,
        string? probeError)
    {
        Identity = identity;
        Number = number;
        FriendlyName = friendlyName;
        Media = media;
        PartitionStyle = partitionStyle;
        IsSystem = isSystem;
        IsBoot = isBoot;
        IsOffline = isOffline;
        IsReadOnly = isReadOnly;
        IsRemovable = isRemovable;
        Partitions = partitions;
        FreeSpaces = freeSpaces;
        ProbeError = probeError;
    }

    public DiskIdentity Identity { get; }

    /// <summary>
    /// Порядковый номер. Используется ТОЛЬКО для соединения записей WMI между собой
    /// и для отладки. В интерфейсе не показывается, в рецепт не попадает.
    /// </summary>
    public int Number { get; }

    public string FriendlyName { get; }
    public MediaKind Media { get; }
    public PartitionStyle PartitionStyle { get; }

    /// <summary>На диске лежит работающая сейчас система.</summary>
    public bool IsSystem { get; }

    /// <summary>С диска выполнялась текущая загрузка.</summary>
    public bool IsBoot { get; }

    public bool IsOffline { get; }
    public bool IsReadOnly { get; }
    public bool IsRemovable { get; }

    public IReadOnlyList<PartitionInfo> Partitions { get; }
    public IReadOnlyList<FreeSpaceInfo> FreeSpaces { get; }

    /// <summary>Заполнено, если разделы прочитать не удалось. Сам диск при этом показывается.</summary>
    public string? ProbeError { get; }

    /// <summary>Загрузочный носитель Windows Peace. Проставляется BootMediaLocator.</summary>
    public bool IsWindowsPeaceMedia { get; internal set; }
}
```

Файл `src/WindowsPeace.Core/Storage/DiskSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Результат одного перечисления. Отдельное поле под общий сбой нужно,
/// чтобы отличать «дисков нет» от «спросить не удалось».
/// </summary>
public sealed class DiskSnapshot
{
    public DiskSnapshot(IReadOnlyList<DiskInfo> disks, string? enumerationError)
    {
        Disks = disks;
        EnumerationError = enumerationError;
    }

    public static DiskSnapshot Failed(string error) => new(new List<DiskInfo>(), error);

    public IReadOnlyList<DiskInfo> Disks { get; }
    public string? EnumerationError { get; }
    public bool IsFailed => EnumerationError is not null;
}
```

- [ ] **Шаг 4: Написать вычисление промежутков**

Файл `src/WindowsPeace.Core/Storage/FreeSpaceCalculator.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace WindowsPeace.Core.Storage;

/// <summary>Находит незанятые промежутки между разделами.</summary>
public static class FreeSpaceCalculator
{
    private const ulong Mib = 1024UL * 1024UL;

    /// <summary>Первый мегабайт занят таблицей разделов и выравниванием.</summary>
    private const ulong HeadReserve = Mib;

    /// <summary>Последний мегабайт занят резервной копией таблицы GPT.</summary>
    private const ulong TailReserve = Mib;

    /// <summary>Промежутки меньше мегабайта бесполезны и только засоряют список.</summary>
    private const ulong MinimumUsefulGap = Mib;

    public static IReadOnlyList<FreeSpaceInfo> Calculate(ulong diskSize, IReadOnlyList<PartitionInfo> partitions)
    {
        var result = new List<FreeSpaceInfo>();

        if (diskSize <= HeadReserve + TailReserve)
        {
            return result;
        }

        var limit = diskSize - TailReserve;
        var cursor = HeadReserve;

        foreach (var partition in partitions.OrderBy(p => p.Offset))
        {
            if (partition.Offset > cursor)
            {
                AddIfUseful(result, cursor, partition.Offset);
            }

            if (partition.End > cursor)
            {
                cursor = partition.End;
            }
        }

        if (limit > cursor)
        {
            AddIfUseful(result, cursor, limit);
        }

        return result;
    }

    private static void AddIfUseful(ICollection<FreeSpaceInfo> result, ulong from, ulong to)
    {
        var size = to - from;
        if (size >= MinimumUsefulGap)
        {
            result.Add(new FreeSpaceInfo(from, size));
        }
    }
}
```

- [ ] **Шаг 5: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter FreeSpaceCalculatorTests`
Ожидается: PASS, 6 тестов.

- [ ] **Шаг 6: Коммит**

```bash
git add -A
git commit -m "Модель дисков и вычисление незанятых промежутков"
```

---

## Задача 6: Правила выбора и предупреждения

**Файлы:**
- Создать: `src/WindowsPeace.Core/Selection/SelectionTarget.cs`, `SelectionVerdict.cs`, `PlanWarning.cs`, `SelectionRules.cs`
- Тест: `test/WindowsPeace.Core.Tests/SelectionRulesTests.cs`, `test/WindowsPeace.Core.Tests/TestDisks.cs`

**Интерфейсы:**
- Отдаёт дальше: `SelectionRules.Evaluate(SelectionTarget target)` возвращает `SelectionVerdict` со свойствами `IsAllowed`, `Reason`; `SelectionRules.Warnings(SelectionTarget target, IReadOnlyList<DiskInfo> allDisks)` возвращает `IReadOnlyList<PlanWarning>`; константа `SelectionRules.MinimumWindowsPartitionBytes`.

- [ ] **Шаг 1: Написать построитель тестовых дисков**

Файл `test/WindowsPeace.Core.Tests/TestDisks.cs`:

```csharp
using System.Collections.Generic;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Tests;

/// <summary>Сборка дисков для тестов. Живое железо здесь не участвует.</summary>
internal static class TestDisks
{
    public const ulong Gib = 1024UL * 1024UL * 1024UL;

    public static DiskIdentity Identity(string? serial = "SN-1", ulong size = 500 * Gib)
        => DiskIdentity.Create(serial, null, null, null, null, "Тестовый диск", size, BusType.Nvme);

    public static PartitionInfo Partition(
        int number = 1,
        ulong offset = 1048576UL,
        ulong size = 100 * Gib,
        PartitionKind kind = PartitionKind.BasicData,
        char? letter = 'C',
        VolumeInfo? volume = null)
        => new(number, offset, size, kind, letter, isSystem: false, isHidden: false, volume: volume);

    public static DiskInfo Disk(
        string? serial = "SN-1",
        ulong size = 500 * Gib,
        bool isSystem = false,
        bool isBoot = false,
        bool isOffline = false,
        bool isReadOnly = false,
        bool isRemovable = false,
        bool isMedia = false,
        IReadOnlyList<PartitionInfo>? partitions = null,
        string? probeError = null)
    {
        var actualPartitions = partitions ?? new List<PartitionInfo>();
        var disk = new DiskInfo(
            Identity(serial, size),
            number: 0,
            friendlyName: "Тестовый диск",
            media: MediaKind.Ssd,
            partitionStyle: PartitionStyle.Gpt,
            isSystem: isSystem,
            isBoot: isBoot,
            isOffline: isOffline,
            isReadOnly: isReadOnly,
            isRemovable: isRemovable,
            partitions: actualPartitions,
            freeSpaces: FreeSpaceCalculator.Calculate(size, actualPartitions),
            probeError: probeError);

        disk.IsWindowsPeaceMedia = isMedia;
        return disk;
    }

    public static void SetContent(PartitionInfo partition, bool windows = false, bool userFiles = false)
        => partition.Content = new PartitionContent(windows, windows ? "Windows 11 Pro" : null, userFiles, inspected: true, notInspectedReason: null);
}
```

Для присвоения `Content` и `IsWindowsPeaceMedia` из тестов открой доступ к внутренним членам. Добавь в `src/WindowsPeace.Core/WindowsPeace.Core.csproj`:

```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>WindowsPeace.Core.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

- [ ] **Шаг 2: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/SelectionRulesTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class SelectionRulesTests
{
    [Fact]
    public void Обычный_диск_выбрать_можно()
    {
        var verdict = SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk()));

        Assert.True(verdict.IsAllowed);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Загрузочный_носитель_выбрать_нельзя()
    {
        var verdict = SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk(isMedia: true)));

        Assert.False(verdict.IsAllowed);
        Assert.Contains("носител", verdict.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Диск_работающей_системы_выбрать_нельзя()
    {
        Assert.False(SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk(isSystem: true))).IsAllowed);
        Assert.False(SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk(isBoot: true))).IsAllowed);
    }

    [Fact]
    public void Отключённый_и_защищённый_от_записи_диски_выбрать_нельзя()
    {
        Assert.False(SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk(isOffline: true))).IsAllowed);
        Assert.False(SelectionRules.Evaluate(SelectionTarget.WholeDisk(TestDisks.Disk(isReadOnly: true))).IsAllowed);
    }

    [Fact]
    public void Запрет_диска_наследуется_его_разделами()
    {
        var partition = TestDisks.Partition();
        var disk = TestDisks.Disk(isSystem: true, partitions: new[] { partition });

        Assert.False(SelectionRules.Evaluate(SelectionTarget.Partition(disk, partition)).IsAllowed);
    }

    [Fact]
    public void Раздел_меньше_сорока_гигабайт_выбрать_нельзя_и_сказано_сколько_не_хватает()
    {
        var partition = TestDisks.Partition(size: 30 * TestDisks.Gib);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var verdict = SelectionRules.Evaluate(SelectionTarget.Partition(disk, partition));

        Assert.False(verdict.IsAllowed);
        Assert.Contains("10", verdict.Reason!);
    }

    [Fact]
    public void Служебный_раздел_выбрать_нельзя()
    {
        var partition = TestDisks.Partition(size: 100 * TestDisks.Gib, kind: PartitionKind.EfiSystem);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        Assert.False(SelectionRules.Evaluate(SelectionTarget.Partition(disk, partition)).IsAllowed);
    }

    [Fact]
    public void Незанятый_промежуток_меньше_сорока_гигабайт_выбрать_нельзя()
    {
        var disk = TestDisks.Disk(size: 500 * TestDisks.Gib);
        var small = new FreeSpaceInfo(1048576UL, 30 * TestDisks.Gib);

        Assert.False(SelectionRules.Evaluate(SelectionTarget.FreeSpace(disk, small)).IsAllowed);
    }

    [Fact]
    public void Диск_с_нечитаемыми_разделами_выбрать_целиком_можно_а_разделы_нет()
    {
        var disk = TestDisks.Disk(probeError: "Разделы прочитать не удалось");

        Assert.True(SelectionRules.Evaluate(SelectionTarget.WholeDisk(disk)).IsAllowed);
    }

    [Fact]
    public void Установленная_Windows_и_файлы_пользователя_дают_два_предупреждения()
    {
        var partition = TestDisks.Partition();
        TestDisks.SetContent(partition, windows: true, userFiles: true);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var warnings = SelectionRules.Warnings(SelectionTarget.WholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.WindowsOnTarget);
        Assert.Contains(warnings, w => w.Kind == WarningKind.UserFilesOnTarget);
    }

    [Fact]
    public void Windows_на_другом_диске_даёт_предупреждение_о_перехвате_загрузки()
    {
        var target = TestDisks.Disk(serial: "SN-TARGET");

        var otherPartition = TestDisks.Partition();
        TestDisks.SetContent(otherPartition, windows: true);
        var other = TestDisks.Disk(serial: "SN-OTHER", partitions: new[] { otherPartition });

        var warnings = SelectionRules.Warnings(SelectionTarget.WholeDisk(target), new[] { target, other });

        Assert.Contains(warnings, w => w.Kind == WarningKind.OtherWindowsFound);
    }

    [Fact]
    public void Ненадёжный_отпечаток_даёт_предупреждение()
    {
        var disk = TestDisks.Disk(serial: null);

        var warnings = SelectionRules.Warnings(SelectionTarget.WholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.WeakIdentity);
    }

    [Fact]
    public void Непроверенный_раздел_даёт_предупреждение()
    {
        var partition = TestDisks.Partition(letter: null);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var warnings = SelectionRules.Warnings(SelectionTarget.WholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.ContentNotInspected);
    }

    [Fact]
    public void Предупреждения_не_повторяются()
    {
        var first = TestDisks.Partition(number: 1);
        var second = TestDisks.Partition(number: 2, offset: 200 * TestDisks.Gib);
        TestDisks.SetContent(first, windows: true);
        TestDisks.SetContent(second, windows: true);
        var disk = TestDisks.Disk(partitions: new[] { first, second });

        var warnings = SelectionRules.Warnings(SelectionTarget.WholeDisk(disk), new[] { disk });

        Assert.Single(warnings.Where(w => w.Kind == WarningKind.WindowsOnTarget));
    }
}
```

- [ ] **Шаг 3: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter SelectionRulesTests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 4: Написать цель выбора и вердикт**

Файл `src/WindowsPeace.Core/Selection/SelectionTarget.cs`:

```csharp
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Что именно выбрано в списке.</summary>
public enum TargetKind
{
    WholeDisk,
    ExistingPartition,
    FreeSpace,
}

/// <summary>
/// Цель установки. Разница между «диск целиком» и «раздел» — это разница
/// между «размечаем по рецепту» и «ставим сюда, остального не трогаем».
/// </summary>
public sealed class SelectionTarget
{
    private SelectionTarget(TargetKind kind, DiskInfo disk, PartitionInfo? partition, FreeSpaceInfo? freeSpace)
    {
        Kind = kind;
        Disk = disk;
        Partition = partition;
        FreeSpace = freeSpace;
    }

    public static SelectionTarget WholeDisk(DiskInfo disk) => new(TargetKind.WholeDisk, disk, null, null);

    public static SelectionTarget Partition(DiskInfo disk, PartitionInfo partition)
        => new(TargetKind.ExistingPartition, disk, partition, null);

    public static SelectionTarget FreeSpace(DiskInfo disk, FreeSpaceInfo freeSpace)
        => new(TargetKind.FreeSpace, disk, null, freeSpace);

    public TargetKind Kind { get; }
    public DiskInfo Disk { get; }
    public PartitionInfo? Partition { get; }
    public FreeSpaceInfo? FreeSpace { get; }

    /// <summary>Сколько места отводится под Windows.</summary>
    public ulong AvailableBytes => Kind switch
    {
        TargetKind.WholeDisk => Disk.Identity.SizeBytes,
        TargetKind.ExistingPartition => Partition!.Size,
        TargetKind.FreeSpace => FreeSpace!.Size,
        _ => 0UL,
    };
}
```

Файл `src/WindowsPeace.Core/Selection/SelectionVerdict.cs`:

```csharp
namespace WindowsPeace.Core.Selection;

/// <summary>Можно ли выбрать цель. Отказ всегда сопровождается причиной.</summary>
public sealed class SelectionVerdict
{
    private SelectionVerdict(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public static SelectionVerdict Allowed { get; } = new(true, null);

    public static SelectionVerdict Denied(string reason) => new(false, reason);

    public bool IsAllowed { get; }
    public string? Reason { get; }
}
```

Файл `src/WindowsPeace.Core/Selection/PlanWarning.cs`:

```csharp
namespace WindowsPeace.Core.Selection;

/// <summary>Разновидность предупреждения. По ней интерфейс подбирает вид и порядок.</summary>
public enum WarningKind
{
    WindowsOnTarget,
    UserFilesOnTarget,
    OtherWindowsFound,
    WeakIdentity,
    ContentNotInspected,
    PartitionsNotRead,
}

/// <summary>Насколько предупреждение серьёзно.</summary>
public enum WarningSeverity
{
    Notice,
    Important,
}

/// <summary>Предупреждение с готовым текстом для человека.</summary>
public sealed class PlanWarning
{
    public PlanWarning(WarningKind kind, WarningSeverity severity, string text)
    {
        Kind = kind;
        Severity = severity;
        Text = text;
    }

    public WarningKind Kind { get; }
    public WarningSeverity Severity { get; }
    public string Text { get; }
}
```

- [ ] **Шаг 5: Написать правила**

Файл `src/WindowsPeace.Core/Selection/SelectionRules.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>
/// Что выбрать можно, что нельзя и о чём предупредить. Живёт отдельно от интерфейса,
/// потому что теми же правилами будут пользоваться Studio и автоматический режим
/// из рецепта. См. docs/superpowers/specs/2026-08-10-disk-picker-design.md, раздел 5.
/// </summary>
public static class SelectionRules
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    /// <summary>
    /// Нижняя граница windowsSizeGB из contract/recipe.schema.json.
    /// Расхождение со схемой ловится тестом DeploymentLayoutTests.
    /// </summary>
    public const ulong MinimumWindowsPartitionBytes = 40UL * Gib;

    public static SelectionVerdict Evaluate(SelectionTarget target)
    {
        var diskVerdict = EvaluateDisk(target.Disk);
        if (!diskVerdict.IsAllowed)
        {
            return diskVerdict;
        }

        return target.Kind switch
        {
            TargetKind.WholeDisk => Allowed(target),
            TargetKind.ExistingPartition => EvaluatePartition(target),
            TargetKind.FreeSpace => EvaluateSize(target.FreeSpace!.Size),
            _ => SelectionVerdict.Denied("Неизвестный вид цели"),
        };
    }

    private static SelectionVerdict Allowed(SelectionTarget target)
        => target.Disk.Identity.SizeBytes < MinimumWindowsPartitionBytes
            ? EvaluateSize(target.Disk.Identity.SizeBytes)
            : SelectionVerdict.Allowed;

    private static SelectionVerdict EvaluateDisk(DiskInfo disk)
    {
        if (disk.IsWindowsPeaceMedia)
        {
            return SelectionVerdict.Denied("Это загрузочный носитель Windows Peace — установка сюда невозможна");
        }

        if (disk.IsSystem || disk.IsBoot)
        {
            return SelectionVerdict.Denied("На этом диске работает текущая система");
        }

        if (disk.IsOffline)
        {
            return SelectionVerdict.Denied("Диск отключён");
        }

        if (disk.IsReadOnly)
        {
            return SelectionVerdict.Denied("Диск защищён от записи");
        }

        return SelectionVerdict.Allowed;
    }

    private static SelectionVerdict EvaluatePartition(SelectionTarget target)
    {
        var partition = target.Partition!;

        if (PartitionKinds.IsSystemService(partition.Kind))
        {
            return SelectionVerdict.Denied("Это служебный раздел, система создаёт его сама");
        }

        return EvaluateSize(partition.Size);
    }

    private static SelectionVerdict EvaluateSize(ulong sizeBytes)
    {
        if (sizeBytes >= MinimumWindowsPartitionBytes)
        {
            return SelectionVerdict.Allowed;
        }

        var missingGib = (MinimumWindowsPartitionBytes - sizeBytes + Gib - 1) / Gib;
        var text = string.Format(
            CultureInfo.CurrentCulture,
            "Слишком мало места: не хватает {0} ГБ до минимальных 40 ГБ",
            missingGib);

        return SelectionVerdict.Denied(text);
    }

    public static IReadOnlyList<PlanWarning> Warnings(SelectionTarget target, IReadOnlyList<DiskInfo> allDisks)
    {
        var warnings = new List<PlanWarning>();
        var seen = new HashSet<WarningKind>();

        void Add(WarningKind kind, WarningSeverity severity, string text)
        {
            if (seen.Add(kind))
            {
                warnings.Add(new PlanWarning(kind, severity, text));
            }
        }

        var affected = AffectedPartitions(target);

        if (affected.Any(p => p.Content.WindowsFound))
        {
            Add(WarningKind.WindowsOnTarget, WarningSeverity.Important,
                "На цели установлена Windows. Она будет удалена безвозвратно.");
        }

        if (affected.Any(p => p.Content.UserFilesFound))
        {
            Add(WarningKind.UserFilesOnTarget, WarningSeverity.Important,
                "На цели есть файлы пользователя. Они будут удалены безвозвратно.");
        }

        if (target.Disk.ProbeError is not null)
        {
            Add(WarningKind.PartitionsNotRead, WarningSeverity.Important,
                "Разделы этого диска прочитать не удалось, поэтому неизвестно, что на нём лежит.");
        }

        if (affected.Any(p => !p.Content.Inspected))
        {
            Add(WarningKind.ContentNotInspected, WarningSeverity.Notice,
                "Содержимое части разделов проверить не удалось: у них нет буквы диска.");
        }

        if (target.Disk.Identity.Confidence != IdentityConfidence.Hardware)
        {
            Add(WarningKind.WeakIdentity, WarningSeverity.Notice,
                "У диска не удалось прочитать серийный номер, опознать его надёжно нельзя.");
        }

        var otherWindows = allDisks
            .Where(d => !ReferenceEquals(d, target.Disk))
            .SelectMany(d => d.Partitions)
            .Any(p => p.Content.WindowsFound);

        if (otherWindows)
        {
            Add(WarningKind.OtherWindowsFound, WarningSeverity.Notice,
                "На другом диске найдена установленная Windows. Она может перехватывать загрузку.");
        }

        return warnings;
    }

    private static IReadOnlyList<PartitionInfo> AffectedPartitions(SelectionTarget target) => target.Kind switch
    {
        TargetKind.WholeDisk => target.Disk.Partitions,
        TargetKind.ExistingPartition => new[] { target.Partition! },
        _ => new List<PartitionInfo>(),
    };
}
```

- [ ] **Шаг 6: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter SelectionRulesTests`
Ожидается: PASS, 14 тестов.

- [ ] **Шаг 7: Коммит**

```bash
git add -A
git commit -m "Правила выбора цели и накопление предупреждений"
```

---

## Задача 7: План разметки и сверка со схемой

**Файлы:**
- Создать: `src/WindowsPeace.Core/Selection/DeploymentLayout.cs`, `DeploymentPlan.cs`, `DeploymentPlanner.cs`
- Тест: `test/WindowsPeace.Core.Tests/DeploymentPlannerTests.cs`, `test/WindowsPeace.Core.Tests/DeploymentLayoutTests.cs`

**Интерфейсы:**
- Отдаёт дальше: `DeploymentLayout.Default` со свойствами `EspMb`, `MsrMb`, `RecoveryMb`, `RecoveryAtEnd`; `DeploymentPlanner.Build(SelectionTarget target)` возвращает `DeploymentPlan` со свойствами `Steps` (`IReadOnlyList<PlanStep>`) и `Summary`.

- [ ] **Шаг 1: Написать тест, сверяющий значения со схемой**

Файл `test/WindowsPeace.Core.Tests/DeploymentLayoutTests.cs`:

```csharp
using System;
using System.IO;
using System.Text.RegularExpressions;
using WindowsPeace.Core.Selection;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Значения по умолчанию продублированы в коде и в схеме рецепта.
/// Дублирование допущено осознанно — шаг А не читает рецепт, — но расхождение
/// должно ломать сборку, а не всплывать через полгода на чужой машине.
/// </summary>
public class DeploymentLayoutTests
{
    private static string SchemaText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WindowsPeace.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "contract", "recipe.schema.json"));
    }

    private static int DefaultOf(string property)
    {
        var pattern = "\"" + property + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*(\\d+)";
        var match = Regex.Match(SchemaText(), pattern, RegexOptions.Singleline);
        Assert.True(match.Success, $"В схеме не найдено значение по умолчанию для {property}");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Размер_EFI_совпадает_со_схемой() => Assert.Equal(DefaultOf("espMB"), DeploymentLayout.Default.EspMb);

    [Fact]
    public void Размер_MSR_совпадает_со_схемой() => Assert.Equal(DefaultOf("msrMB"), DeploymentLayout.Default.MsrMb);

    [Fact]
    public void Размер_раздела_восстановления_совпадает_со_схемой()
        => Assert.Equal(DefaultOf("recoveryMB"), DeploymentLayout.Default.RecoveryMb);

    [Fact]
    public void Минимальный_размер_раздела_совпадает_со_схемой()
    {
        var match = Regex.Match(SchemaText(), "\"windowsSizeGB\"\\s*:\\s*\\{[^}]*?\"minimum\"\\s*:\\s*(\\d+)", RegexOptions.Singleline);
        Assert.True(match.Success);

        var minimumGib = ulong.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(minimumGib * 1024UL * 1024UL * 1024UL, SelectionRules.MinimumWindowsPartitionBytes);
    }
}
```

- [ ] **Шаг 2: Написать тесты построителя плана**

Файл `test/WindowsPeace.Core.Tests/DeploymentPlannerTests.cs`:

```csharp
using System.Linq;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class DeploymentPlannerTests
{
    [Fact]
    public void Для_диска_целиком_план_содержит_четыре_раздела_в_нужном_порядке()
    {
        var plan = DeploymentPlanner.Build(SelectionTarget.WholeDisk(TestDisks.Disk(size: 500 * TestDisks.Gib)));

        Assert.Equal(4, plan.Steps.Count);
        Assert.Equal(PartitionKind.EfiSystem, plan.Steps[0].Kind);
        Assert.Equal(PartitionKind.MicrosoftReserved, plan.Steps[1].Kind);
        Assert.Equal(PartitionKind.BasicData, plan.Steps[2].Kind);
        Assert.Equal(PartitionKind.WindowsRecovery, plan.Steps[3].Kind);
    }

    [Fact]
    public void Раздел_Windows_занимает_остаток_диска()
    {
        var size = 500 * TestDisks.Gib;
        var plan = DeploymentPlanner.Build(SelectionTarget.WholeDisk(TestDisks.Disk(size: size)));

        var windows = plan.Steps.Single(s => s.Kind == PartitionKind.BasicData);
        var service = plan.Steps.Where(s => s.Kind != PartitionKind.BasicData).Sum(s => (decimal)s.SizeBytes);

        Assert.Equal(size - (ulong)service, windows.SizeBytes);
    }

    [Fact]
    public void Для_существующего_раздела_план_состоит_из_одного_шага_и_остальное_не_трогается()
    {
        var partition = TestDisks.Partition(size: 200 * TestDisks.Gib);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var plan = DeploymentPlanner.Build(SelectionTarget.Partition(disk, partition));

        var step = Assert.Single(plan.Steps);
        Assert.Equal(PartitionKind.BasicData, step.Kind);
        Assert.Equal(200 * TestDisks.Gib, step.SizeBytes);
        Assert.False(plan.WipesWholeDisk);
    }

    [Fact]
    public void План_для_диска_целиком_помечен_как_стирающий_всё()
    {
        Assert.True(DeploymentPlanner.Build(SelectionTarget.WholeDisk(TestDisks.Disk())).WipesWholeDisk);
    }

    [Fact]
    public void Краткая_строка_плана_перечисляет_разделы_с_размерами()
    {
        var plan = DeploymentPlanner.Build(SelectionTarget.WholeDisk(TestDisks.Disk(size: 500 * TestDisks.Gib)));

        Assert.Contains("EFI", plan.Summary);
        Assert.Contains("300 МБ", plan.Summary);
        Assert.Contains("Восстановление", plan.Summary);
    }
}
```

- [ ] **Шаг 3: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter "DeploymentPlannerTests|DeploymentLayoutTests"`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 4: Написать раскладку и план**

Файл `src/WindowsPeace.Core/Selection/DeploymentLayout.cs`:

```csharp
namespace WindowsPeace.Core.Selection;

/// <summary>
/// Размеры служебных разделов. На шаге А берутся из значений по умолчанию
/// схемы рецепта; на шаге В будут читаться из самого рецепта.
/// Источник: contract/recipe.schema.json, target.layout.
/// </summary>
public sealed class DeploymentLayout
{
    private DeploymentLayout(int espMb, int msrMb, int recoveryMb, bool recoveryAtEnd)
    {
        EspMb = espMb;
        MsrMb = msrMb;
        RecoveryMb = recoveryMb;
        RecoveryAtEnd = recoveryAtEnd;
    }

    public static DeploymentLayout Default { get; } = new(espMb: 300, msrMb: 16, recoveryMb: 1024, recoveryAtEnd: true);

    public int EspMb { get; }
    public int MsrMb { get; }
    public int RecoveryMb { get; }
    public bool RecoveryAtEnd { get; }
}
```

Файл `src/WindowsPeace.Core/Selection/DeploymentPlan.cs`:

```csharp
using System.Collections.Generic;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Один раздел будущей разметки.</summary>
public sealed class PlanStep
{
    public PlanStep(PartitionKind kind, string title, ulong sizeBytes)
    {
        Kind = kind;
        Title = title;
        SizeBytes = sizeBytes;
    }

    public PartitionKind Kind { get; }
    public string Title { get; }
    public ulong SizeBytes { get; }
}

/// <summary>Предпросмотр того, что будет сделано. Ничего не выполняет.</summary>
public sealed class DeploymentPlan
{
    public DeploymentPlan(IReadOnlyList<PlanStep> steps, bool wipesWholeDisk, string summary)
    {
        Steps = steps;
        WipesWholeDisk = wipesWholeDisk;
        Summary = summary;
    }

    public IReadOnlyList<PlanStep> Steps { get; }
    public bool WipesWholeDisk { get; }
    public string Summary { get; }
}
```

Файл `src/WindowsPeace.Core/Selection/DeploymentPlanner.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Строит предпросмотр разметки по выбранной цели.</summary>
public static class DeploymentPlanner
{
    private const ulong Mib = 1024UL * 1024UL;
    private const ulong Gib = 1024UL * Mib;

    public static DeploymentPlan Build(SelectionTarget target)
        => target.Kind == TargetKind.WholeDisk
            ? BuildWholeDisk(target, DeploymentLayout.Default)
            : BuildSingle(target);

    private static DeploymentPlan BuildWholeDisk(SelectionTarget target, DeploymentLayout layout)
    {
        var esp = (ulong)layout.EspMb * Mib;
        var msr = (ulong)layout.MsrMb * Mib;
        var recovery = (ulong)layout.RecoveryMb * Mib;
        var total = target.Disk.Identity.SizeBytes;
        var service = esp + msr + recovery;
        var windows = total > service ? total - service : 0UL;

        var steps = new List<PlanStep>
        {
            new(PartitionKind.EfiSystem, "EFI", esp),
            new(PartitionKind.MicrosoftReserved, "MSR", msr),
            new(PartitionKind.BasicData, "Windows", windows),
            new(PartitionKind.WindowsRecovery, "Восстановление", recovery),
        };

        return new DeploymentPlan(steps, wipesWholeDisk: true, summary: Summarize(steps));
    }

    private static DeploymentPlan BuildSingle(SelectionTarget target)
    {
        var steps = new List<PlanStep>
        {
            new(PartitionKind.BasicData, "Windows", target.AvailableBytes),
        };

        return new DeploymentPlan(steps, wipesWholeDisk: false,
            summary: "Windows " + Format(target.AvailableBytes) + ". Остальные разделы не изменяются.");
    }

    private static string Summarize(IEnumerable<PlanStep> steps)
        => string.Join(" · ", steps.Select(s => s.Title + " " + Format(s.SizeBytes)));

    private static string Format(ulong bytes)
        => bytes >= Gib
            ? ((double)bytes / Gib).ToString("0.#", CultureInfo.CurrentCulture) + " ГБ"
            : (bytes / Mib).ToString(CultureInfo.CurrentCulture) + " МБ";
}
```

- [ ] **Шаг 5: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter "DeploymentPlannerTests|DeploymentLayoutTests"`
Ожидается: PASS, 9 тестов.

- [ ] **Шаг 6: Коммит**

```bash
git add -A
git commit -m "Предпросмотр разметки и сверка размеров со схемой рецепта"
```

---

## Задача 8: Определение содержимого и загрузочного носителя

**Файлы:**
- Создать: `src/WindowsPeace.Core/Storage/IDiskContentInspector.cs`, `FileSystemContentInspector.cs`, `BootMediaLocator.cs`
- Тест: `test/WindowsPeace.Core.Tests/ContentInspectorTests.cs`

**Интерфейсы:**
- Потребляет: `PartitionInfo`, `DiskInfo` из задачи 5.
- Отдаёт дальше: `IDiskContentInspector.Inspect(DiskInfo disk, CancellationToken token)`; `IFileSystemProbe` с методами `DirectoryExists(string path)`, `FileExists(string path)`, `EnumerateDirectories(string path)`; `BootMediaLocator.Mark(IReadOnlyList<DiskInfo> disks, IFileSystemProbe probe)`.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Core.Tests/ContentInspectorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

internal sealed class FakeFileSystem : IFileSystemProbe
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem AddDirectory(string path)
    {
        _directories.Add(path);
        return this;
    }

    public FakeFileSystem AddFile(string path)
    {
        _files.Add(path);
        return this;
    }

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public bool FileExists(string path) => _files.Contains(path);

    public IReadOnlyList<string> EnumerateDirectories(string path)
        => _directories.Where(d => d.StartsWith(path, StringComparison.OrdinalIgnoreCase)
                                   && d.Length > path.Length
                                   && !d.Substring(path.Length).TrimEnd('\\').Contains('\\'))
            .ToList();
}

public class ContentInspectorTests
{
    private static DiskInfo DiskWith(PartitionInfo partition) => TestDisks.Disk(partitions: new[] { partition });

    [Fact]
    public void Windows_находится_по_кусту_реестра()
    {
        var fs = new FakeFileSystem().AddFile(@"C:\Windows\System32\config\SYSTEM");
        var partition = TestDisks.Partition(letter: 'C');
        var inspector = new FileSystemContentInspector(fs);

        inspector.Inspect(DiskWith(partition), CancellationToken.None);

        Assert.True(partition.Content.WindowsFound);
        Assert.True(partition.Content.Inspected);
    }

    [Fact]
    public void Без_куста_реестра_Windows_не_считается_найденной()
    {
        var fs = new FakeFileSystem().AddDirectory(@"C:\Windows\");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.WindowsFound);
    }

    [Fact]
    public void Пользовательские_папки_находятся_а_служебные_не_считаются()
    {
        var fs = new FakeFileSystem()
            .AddDirectory(@"C:\Users\")
            .AddDirectory(@"C:\Users\Default")
            .AddDirectory(@"C:\Users\Public")
            .AddDirectory(@"C:\Users\HugoBoss");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.True(partition.Content.UserFilesFound);
    }

    [Fact]
    public void Только_служебные_папки_не_считаются_файлами_пользователя()
    {
        var fs = new FakeFileSystem()
            .AddDirectory(@"C:\Users\")
            .AddDirectory(@"C:\Users\Default")
            .AddDirectory(@"C:\Users\Public")
            .AddDirectory(@"C:\Users\All Users");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.UserFilesFound);
    }

    [Fact]
    public void Раздел_без_буквы_помечается_как_непроверенный_с_причиной()
    {
        var partition = TestDisks.Partition(letter: null);

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.Inspected);
        Assert.NotNull(partition.Content.NotInspectedReason);
    }

    [Fact]
    public void Служебные_разделы_не_проверяются_вовсе()
    {
        var partition = TestDisks.Partition(letter: 'S', kind: PartitionKind.EfiSystem);

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.Inspected);
    }

    [Fact]
    public void Отмена_прекращает_проверку_и_не_бросает()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), cts.Token);

        Assert.False(partition.Content.Inspected);
    }

    [Fact]
    public void Диск_с_описью_носителя_помечается_загрузочным()
    {
        var fs = new FakeFileSystem().AddFile(@"D:\windows-peace-media.json");
        var partition = TestDisks.Partition(letter: 'D');
        var disk = DiskWith(partition);

        BootMediaLocator.Mark(new[] { disk }, fs);

        Assert.True(disk.IsWindowsPeaceMedia);
    }

    [Fact]
    public void Без_описи_ни_один_диск_загрузочным_не_считается()
    {
        var partition = TestDisks.Partition(letter: 'D');
        var disk = DiskWith(partition);

        BootMediaLocator.Mark(new[] { disk }, new FakeFileSystem());

        Assert.False(disk.IsWindowsPeaceMedia);
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test --filter ContentInspectorTests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 3: Написать интерфейсы**

Файл `src/WindowsPeace.Core/Storage/IDiskContentInspector.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Обращения к файловой системе спрятаны за интерфейсом, чтобы правила
/// определения содержимого проверялись тестами без настоящих дисков.
/// </summary>
public interface IFileSystemProbe
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    IReadOnlyList<string> EnumerateDirectories(string path);
}

/// <summary>Заполняет Content у разделов диска.</summary>
public interface IDiskContentInspector
{
    void Inspect(DiskInfo disk, CancellationToken cancellationToken);
}
```

- [ ] **Шаг 4: Написать инспектор**

Файл `src/WindowsPeace.Core/Storage/FileSystemContentInspector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>Определяет содержимое разделов через файловую систему.</summary>
public sealed class FileSystemContentInspector : IDiskContentInspector
{
    private static readonly string[] ServiceProfiles =
    {
        "Default", "Default User", "Public", "All Users",
    };

    private readonly IFileSystemProbe _probe;

    public FileSystemContentInspector(IFileSystemProbe probe) => _probe = probe;

    public void Inspect(DiskInfo disk, CancellationToken cancellationToken)
    {
        foreach (var partition in disk.Partitions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                partition.Content = PartitionContent.NotInspected("Проверка прервана");
                continue;
            }

            partition.Content = InspectPartition(partition);
        }
    }

    private PartitionContent InspectPartition(PartitionInfo partition)
    {
        if (PartitionKinds.IsSystemService(partition.Kind))
        {
            return PartitionContent.NotInspected("Служебный раздел, содержимое не проверяется");
        }

        if (partition.DriveLetter is null)
        {
            return PartitionContent.NotInspected("У раздела нет буквы диска");
        }

        var root = string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value);

        var windowsFound = _probe.FileExists(Path.Combine(root, @"Windows\System32\config\SYSTEM"));
        var userFilesFound = HasUserProfiles(Path.Combine(root, "Users") + "\\");

        return new PartitionContent(windowsFound, windowsFound ? "Windows" : null, userFilesFound,
            inspected: true, notInspectedReason: null);
    }

    private bool HasUserProfiles(string usersPath)
    {
        if (!_probe.DirectoryExists(usersPath))
        {
            return false;
        }

        return _probe.EnumerateDirectories(usersPath)
            .Select(p => new DirectoryInfo(p.TrimEnd('\\')).Name)
            .Any(name => !ServiceProfiles.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
```

Реализация поверх настоящей файловой системы — файл `src/WindowsPeace.Core/Storage/RealFileSystemProbe.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Обращения к настоящей файловой системе. Каждый вызов защищён от исключений:
/// недоступный или сбойный том не должен ронять перечисление целиком.
/// Пустого catch здесь нет — каждый перехват возвращает осмысленное значение.
/// </summary>
public sealed class RealFileSystemProbe : IFileSystemProbe
{
    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
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

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
```

- [ ] **Шаг 5: Написать поиск загрузочного носителя**

Файл `src/WindowsPeace.Core/Storage/BootMediaLocator.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Находит носитель Windows Peace по описи в его корне. Именно по файлу,
/// а не по буквам, номерам или догадкам о том, откуда шла загрузка:
/// цена ошибки здесь — форматирование собственной флешки.
/// </summary>
public static class BootMediaLocator
{
    /// <summary>Имя описи. То же значение используется Studio при записи носителя.</summary>
    public const string ManifestFileName = "windows-peace-media.json";

    public static void Mark(IReadOnlyList<DiskInfo> disks, IFileSystemProbe probe)
    {
        foreach (var disk in disks)
        {
            disk.IsWindowsPeaceMedia = false;

            foreach (var partition in disk.Partitions)
            {
                if (partition.DriveLetter is null)
                {
                    continue;
                }

                var root = string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value);
                if (probe.FileExists(Path.Combine(root, ManifestFileName)))
                {
                    disk.IsWindowsPeaceMedia = true;
                    break;
                }
            }
        }
    }
}
```

- [ ] **Шаг 6: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test --filter ContentInspectorTests`
Ожидается: PASS, 9 тестов.

- [ ] **Шаг 7: Коммит**

```bash
git add -A
git commit -m "Определение содержимого разделов и поиск загрузочного носителя"
```

---

## Задача 9: Перечисление дисков через WMI

**Файлы:**
- Создать: `src/WindowsPeace.Core/Storage/IDiskEnumerator.cs`, `WmiDiskEnumerator.cs`, `WmiValue.cs`
- Создать: `tools/DiskDump/DiskDump.csproj`, `tools/DiskDump/Program.cs`

**Интерфейсы:**
- Потребляет: модели из задачи 5, `Timeouts` и `OperationScope` из задачи 4.
- Отдаёт дальше: `IDiskEnumerator.Enumerate(CancellationToken token)` возвращает `DiskSnapshot`.

Модульных тестов у `WmiDiskEnumerator` нет намеренно: он разговаривает с живым железом. Вместо них — консольная утилита `DiskDump`, вывод которой сверяется с оснасткой «Управление дисками» вручную.

- [ ] **Шаг 1: Написать интерфейс перечисления**

Файл `src/WindowsPeace.Core/Storage/IDiskEnumerator.cs`:

```csharp
using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Перечисление дисков. За интерфейсом — чтобы правила выбора
/// проверялись на слепках, а не на настоящем железе.
/// </summary>
public interface IDiskEnumerator
{
    DiskSnapshot Enumerate(CancellationToken cancellationToken);
}
```

- [ ] **Шаг 2: Написать помощник для чтения свойств WMI**

Файл `src/WindowsPeace.Core/Storage/WmiValue.cs`:

```csharp
using System;
using System.Globalization;
using System.Management;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Чтение свойств WMI. Отсутствующее или неожиданного типа свойство —
/// обычное дело на чужом железе, поэтому здесь оно превращается
/// в значение по умолчанию, а не в исключение.
/// </summary>
internal static class WmiValue
{
    public static string? String(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public static ulong UInt64(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? 0UL : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    }

    public static int Int32(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static bool Boolean(ManagementBaseObject source, string name)
    {
        var value = Read(source, name);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    public static char? Char(ManagementBaseObject source, string name)
    {
        var text = String(source, name);
        return string.IsNullOrWhiteSpace(text) || text![0] == '\0' ? null : text[0];
    }

    private static object? Read(ManagementBaseObject source, string name)
    {
        try
        {
            return source[name];
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
```

- [ ] **Шаг 3: Написать перечисление**

Файл `src/WindowsPeace.Core/Storage/WmiDiskEnumerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using WindowsPeace.Core.Diagnostics;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Перечисление дисков через пространство имён root\Microsoft\Windows\Storage.
/// Оно есть и в обычной Windows, и в WinPE базового образа — проверено описью
/// содержимого boot.wim, см. docs/ARCHITECTURE.md, раздел 6.
/// </summary>
public sealed class WmiDiskEnumerator : IDiskEnumerator
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";
    private const string CimNamespace = @"root\cimv2";
    private const string Component = "Storage";

    private readonly IOperationLog _log;

    public WmiDiskEnumerator(IOperationLog log) => _log = log;

    public DiskSnapshot Enumerate(CancellationToken cancellationToken)
    {
        using var scope = OperationScope.Start(_log, Component, "Перечисление дисков");

        try
        {
            var disks = QueryAll(cancellationToken);
            scope.Success();
            return new DiskSnapshot(disks, enumerationError: null);
        }
        catch (OperationCanceledException)
        {
            scope.TimedOut();
            return DiskSnapshot.Failed("Опрос дисков превысил отведённое время");
        }
        catch (ManagementException exception)
        {
            scope.Failure(exception.Message);
            return DiskSnapshot.Failed("Не удалось обратиться к службе хранилища: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            scope.Failure(exception.Message);
            return DiskSnapshot.Failed("Недостаточно прав для опроса дисков");
        }
    }

    private List<DiskInfo> QueryAll(CancellationToken cancellationToken)
    {
        var physical = Query(StorageNamespace, "SELECT * FROM MSFT_PhysicalDisk", cancellationToken);
        var partitions = Query(StorageNamespace, "SELECT * FROM MSFT_Partition", cancellationToken);
        var volumes = Query(StorageNamespace, "SELECT * FROM MSFT_Volume", cancellationToken);
        var win32 = Query(CimNamespace, "SELECT Index, SerialNumber FROM Win32_DiskDrive", cancellationToken);
        var disks = Query(StorageNamespace, "SELECT * FROM MSFT_Disk", cancellationToken);

        var volumeByLetter = volumes
            .Where(v => WmiValue.Char(v, "DriveLetter") is not null)
            .GroupBy(v => WmiValue.Char(v, "DriveLetter")!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<DiskInfo>();

        foreach (var disk in disks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(BuildDisk(disk, physical, partitions, win32, volumeByLetter));
        }

        return result;
    }

    private DiskInfo BuildDisk(
        ManagementBaseObject disk,
        IReadOnlyList<ManagementBaseObject> physical,
        IReadOnlyList<ManagementBaseObject> allPartitions,
        IReadOnlyList<ManagementBaseObject> win32,
        IReadOnlyDictionary<char, ManagementBaseObject> volumeByLetter)
    {
        var number = WmiValue.Int32(disk, "Number");
        var uniqueId = WmiValue.String(disk, "UniqueId");
        var size = WmiValue.UInt64(disk, "Size");
        var busType = (BusType)WmiValue.Int32(disk, "BusType");
        var friendlyName = WmiValue.String(disk, "FriendlyName") ?? WmiValue.String(disk, "Model") ?? "Диск без имени";

        var matchedPhysical = physical.FirstOrDefault(p =>
            string.Equals(WmiValue.String(p, "UniqueId"), uniqueId, StringComparison.OrdinalIgnoreCase)
            || WmiValue.String(p, "DeviceId") == number.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var matchedWin32 = win32.FirstOrDefault(w => WmiValue.Int32(w, "Index") == number);

        var identity = DiskIdentity.Create(
            physicalDiskSerial: matchedPhysical is null ? null : WmiValue.String(matchedPhysical, "SerialNumber"),
            diskSerial: WmiValue.String(disk, "SerialNumber"),
            win32DiskDriveSerial: matchedWin32 is null ? null : WmiValue.String(matchedWin32, "SerialNumber"),
            uniqueId: uniqueId,
            gptGuid: WmiValue.String(disk, "Guid"),
            model: friendlyName,
            sizeBytes: size,
            busType: busType);

        string? probeError = null;
        var partitions = new List<PartitionInfo>();

        try
        {
            partitions.AddRange(allPartitions
                .Where(p => WmiValue.Int32(p, "DiskNumber") == number)
                .OrderBy(p => WmiValue.UInt64(p, "Offset"))
                .Select(p => BuildPartition(p, volumeByLetter)));
        }
        catch (ManagementException exception)
        {
            probeError = "Разделы прочитать не удалось: " + exception.Message;
        }

        var media = matchedPhysical is null
            ? MediaKind.Unspecified
            : (MediaKind)WmiValue.Int32(matchedPhysical, "MediaType");

        return new DiskInfo(
            identity,
            number,
            friendlyName,
            media,
            (PartitionStyle)WmiValue.Int32(disk, "PartitionStyle"),
            isSystem: WmiValue.Boolean(disk, "IsSystem"),
            isBoot: WmiValue.Boolean(disk, "IsBoot"),
            isOffline: WmiValue.Boolean(disk, "IsOffline"),
            isReadOnly: WmiValue.Boolean(disk, "IsReadOnly"),
            isRemovable: busType == BusType.Usb || busType == BusType.Sd || busType == BusType.Mmc,
            partitions: partitions,
            freeSpaces: FreeSpaceCalculator.Calculate(size, partitions),
            probeError: probeError);
    }

    private static PartitionInfo BuildPartition(
        ManagementBaseObject partition,
        IReadOnlyDictionary<char, ManagementBaseObject> volumeByLetter)
    {
        var letter = WmiValue.Char(partition, "DriveLetter");

        VolumeInfo? volume = null;
        if (letter is not null && volumeByLetter.TryGetValue(letter.Value, out var found))
        {
            volume = new VolumeInfo(
                WmiValue.String(found, "FileSystem"),
                WmiValue.String(found, "FileSystemLabel"),
                WmiValue.UInt64(found, "Size"),
                WmiValue.UInt64(found, "SizeRemaining"));
        }

        return new PartitionInfo(
            WmiValue.Int32(partition, "PartitionNumber"),
            WmiValue.UInt64(partition, "Offset"),
            WmiValue.UInt64(partition, "Size"),
            PartitionKinds.FromGptType(WmiValue.String(partition, "GptType")),
            letter,
            WmiValue.Boolean(partition, "IsSystem"),
            WmiValue.Boolean(partition, "IsHidden"),
            volume);
    }

    private List<ManagementBaseObject> Query(string scope, string query, CancellationToken cancellationToken)
    {
        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(scope),
            new ObjectQuery(query),
            new EnumerationOptions { Timeout = Timeouts.SingleDiskProbe, ReturnImmediately = true, Rewindable = false });

        var result = new List<ManagementBaseObject>();

        foreach (ManagementBaseObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(item);
        }

        return result;
    }
}
```

- [ ] **Шаг 4: Написать утилиту для сверки с живой машиной**

Файл `tools/DiskDump/DiskDump.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RootNamespace>WindowsPeace.Tools.DiskDump</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\WindowsPeace.Core\WindowsPeace.Core.csproj" />
  </ItemGroup>
</Project>
```

Файл `tools/DiskDump/Program.cs`:

```csharp
using System;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Tools.DiskDump;

/// <summary>
/// Печатает то, что видит WmiDiskEnumerator. Нужна для ручной сверки
/// с оснасткой «Управление дисками»: сам перечислитель разговаривает
/// с живым железом и модульными тестами не покрывается.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using var log = new JsonLinesOperationLog(
            JsonLinesOperationLog.DefaultPath(AppContext.BaseDirectory));

        using var cts = new CancellationTokenSource(Timeouts.DiskEnumeration);

        var snapshot = new WmiDiskEnumerator(log).Enumerate(cts.Token);

        if (snapshot.IsFailed)
        {
            Console.Error.WriteLine("Перечисление не удалось: " + snapshot.EnumerationError);
            return 1;
        }

        var probe = new RealFileSystemProbe();
        var inspector = new FileSystemContentInspector(probe);

        foreach (var disk in snapshot.Disks)
        {
            inspector.Inspect(disk, cts.Token);
        }

        BootMediaLocator.Mark(snapshot.Disks, probe);

        foreach (var disk in snapshot.Disks)
        {
            Console.WriteLine($"[{disk.Number}] {disk.FriendlyName}  {Gb(disk.Identity.SizeBytes)}  {disk.Identity.BusType}  {disk.Media}");
            Console.WriteLine($"     отпечаток: {disk.Identity.SerialNumber ?? "нет"}  источник: {disk.Identity.Source}  доверие: {disk.Identity.Confidence}");
            Console.WriteLine($"     система: {disk.IsSystem}  загрузочный: {disk.IsBoot}  съёмный: {disk.IsRemovable}  носитель WP: {disk.IsWindowsPeaceMedia}");

            if (disk.ProbeError is not null)
            {
                Console.WriteLine("     ОШИБКА: " + disk.ProbeError);
            }

            foreach (var partition in disk.Partitions)
            {
                var letter = partition.DriveLetter is null ? "  " : partition.DriveLetter + ":";
                Console.WriteLine($"     раздел {partition.Number} {letter} {Gb(partition.Size),10}  {partition.Kind,-18} " +
                                  $"Windows={partition.Content.WindowsFound} файлы={partition.Content.UserFilesFound} проверен={partition.Content.Inspected}");
            }

            foreach (var gap in disk.FreeSpaces)
            {
                Console.WriteLine($"     незанято {Gb(gap.Size),10} со смещения {gap.Offset}");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static string Gb(ulong bytes) => (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " ГБ";
}
```

- [ ] **Шаг 5: Добавить утилиту в решение и запустить**

Выполнить: `dotnet sln add tools/DiskDump/DiskDump.csproj`

Выполнить: `dotnet run --project tools/DiskDump`

Ожидается: список настоящих дисков машины.

**Сверить вручную:** открыть «Управление дисками» (`diskmgmt.msc`) и убедиться, что совпадают число дисков, их объёмы, число и размеры разделов, буквы. Отдельно проверить, что диск с работающей системой помечен `система: True`, а флешка — `съёмный: True`.

**Записать результат** в `docs/superpowers/notes/2026-08-10-disk-dump.md`: вывод утилиты и отметку, совпало ли. Это первый слепок настоящего железа и заодно ответ на вопрос, читаются ли серийные номера.

- [ ] **Шаг 6: Коммит**

```bash
git add -A
git commit -m "Перечисление дисков через WMI и утилита сверки с живой машиной"
```

---

## Задача 10: Оболочка мастера

**Файлы:**
- Создать: `src/WindowsPeace.Setup/Infrastructure/ViewModelBase.cs`, `RelayCommand.cs`
- Создать: `src/WindowsPeace.Setup/Shell/IWizardPage.cs`, `WizardNavigator.cs`, `ShellViewModel.cs`, `ShellWindow.xaml`, `ShellWindow.xaml.cs`
- Создать: `src/WindowsPeace.Setup/Pages/PlaceholderPage.xaml`, `PlaceholderPage.xaml.cs`, `PlaceholderViewModel.cs`
- Создать: `src/WindowsPeace.Setup/App.xaml`, `App.xaml.cs`
- Тест: `test/WindowsPeace.Setup.Tests/WizardNavigatorTests.cs` и проект `test/WindowsPeace.Setup.Tests/WindowsPeace.Setup.Tests.csproj`

**Интерфейсы:**
- Отдаёт дальше: `IWizardPage` со свойствами `Title`, `CanGoNext` (`bool`), событием `CanGoNextChanged`; `WizardNavigator` с методами `GoNext()`, `GoBack()`, свойствами `Current`, `CanGoBack`, `CanGoNext`, событием `CurrentChanged`.

- [ ] **Шаг 1: Создать тестовый проект для оболочки**

Файл `test/WindowsPeace.Setup.Tests/WindowsPeace.Setup.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <IsPackable>false</IsPackable>
    <RootNamespace>WindowsPeace.Setup.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\WindowsPeace.Setup\WindowsPeace.Setup.csproj" />
  </ItemGroup>
</Project>
```

Выполнить: `dotnet sln add test/WindowsPeace.Setup.Tests/WindowsPeace.Setup.Tests.csproj`

Чтобы проект-приложение можно было ссылать из тестов, добавь в `src/WindowsPeace.Setup/WindowsPeace.Setup.csproj`:

```xml
  <PropertyGroup>
    <EnableDefaultApplicationDefinition>true</EnableDefaultApplicationDefinition>
    <GenerateProgramFile>true</GenerateProgramFile>
  </PropertyGroup>
```

- [ ] **Шаг 2: Написать падающие тесты**

Файл `test/WindowsPeace.Setup.Tests/WizardNavigatorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using WindowsPeace.Setup.Shell;
using Xunit;

namespace WindowsPeace.Setup.Tests;

internal sealed class FakePage : IWizardPage
{
    private bool _canGoNext = true;

    public FakePage(string title) => Title = title;

    public string Title { get; }

    public bool CanGoNext
    {
        get => _canGoNext;
        set
        {
            _canGoNext = value;
            CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanGoNextChanged;

    public int EnterCount { get; private set; }

    public void OnEnter() => EnterCount++;
}

public class WizardNavigatorTests
{
    private static WizardNavigator Navigator(params IWizardPage[] pages) => new(new List<IWizardPage>(pages));

    [Fact]
    public void Первая_страница_становится_текущей_сразу()
    {
        var first = new FakePage("Диски");
        var navigator = Navigator(first, new FakePage("Дальше"));

        Assert.Same(first, navigator.Current);
        Assert.Equal(1, first.EnterCount);
    }

    [Fact]
    public void Назад_с_первой_страницы_невозможно()
    {
        var navigator = Navigator(new FakePage("Диски"), new FakePage("Дальше"));

        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void Переход_вперёд_меняет_текущую_страницу_и_сообщает_об_этом()
    {
        var second = new FakePage("Дальше");
        var navigator = Navigator(new FakePage("Диски"), second);
        var notified = 0;
        navigator.CurrentChanged += (_, _) => notified++;

        navigator.GoNext();

        Assert.Same(second, navigator.Current);
        Assert.Equal(1, notified);
        Assert.Equal(1, second.EnterCount);
    }

    [Fact]
    public void Назад_возвращает_на_предыдущую_страницу()
    {
        var first = new FakePage("Диски");
        var navigator = Navigator(first, new FakePage("Дальше"));

        navigator.GoNext();
        navigator.GoBack();

        Assert.Same(first, navigator.Current);
        Assert.True(navigator.CanGoBack == false);
    }

    [Fact]
    public void Вперёд_с_последней_страницы_ничего_не_ломает()
    {
        var navigator = Navigator(new FakePage("Диски"));

        navigator.GoNext();

        Assert.False(navigator.CanGoNext);
    }

    [Fact]
    public void Готовность_страницы_управляет_возможностью_идти_дальше()
    {
        var first = new FakePage("Диски") { CanGoNext = false };
        var navigator = Navigator(first, new FakePage("Дальше"));

        Assert.False(navigator.CanGoNext);

        first.CanGoNext = true;

        Assert.True(navigator.CanGoNext);
    }

    [Fact]
    public void Изменение_готовности_страницы_поднимает_событие_навигатора()
    {
        var first = new FakePage("Диски") { CanGoNext = false };
        var navigator = Navigator(first, new FakePage("Дальше"));
        var notified = 0;
        navigator.CanGoNextChanged += (_, _) => notified++;

        first.CanGoNext = true;

        Assert.Equal(1, notified);
    }

    [Fact]
    public void Пустой_список_страниц_недопустим()
    {
        Assert.Throws<ArgumentException>(() => new WizardNavigator(new List<IWizardPage>()));
    }
}
```

- [ ] **Шаг 3: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test test/WindowsPeace.Setup.Tests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 4: Написать основу для моделей представления**

Файл `src/WindowsPeace.Setup/Infrastructure/ViewModelBase.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsPeace.Setup.Infrastructure;

/// <summary>Уведомления об изменении свойств.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }
}
```

Файл `src/WindowsPeace.Setup/Infrastructure/RelayCommand.cs`:

```csharp
using System;
using System.Windows.Input;

namespace WindowsPeace.Setup.Infrastructure;

/// <summary>Команда для кнопок. Доступность вычисляется предикатом.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute is null || _canExecute();

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Шаг 5: Написать страницу и навигатор**

Файл `src/WindowsPeace.Setup/Shell/IWizardPage.cs`:

```csharp
using System;

namespace WindowsPeace.Setup.Shell;

/// <summary>
/// Страница мастера. Оболочка знает о странице ровно это и ничего больше —
/// поэтому добавление экрана не требует правки оболочки.
/// </summary>
public interface IWizardPage
{
    string Title { get; }

    /// <summary>Можно ли уходить со страницы вперёд.</summary>
    bool CanGoNext { get; }

    event EventHandler CanGoNextChanged;

    /// <summary>Вызывается каждый раз при появлении страницы.</summary>
    void OnEnter();
}
```

Файл `src/WindowsPeace.Setup/Shell/WizardNavigator.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace WindowsPeace.Setup.Shell;

/// <summary>
/// Единственное место, где меняется текущая страница. Собрано в одном классе,
/// чтобы переходы нельзя было совершить в обход и чтобы их можно было проверить
/// тестами без запуска интерфейса.
/// </summary>
public sealed class WizardNavigator
{
    private readonly IReadOnlyList<IWizardPage> _pages;
    private int _index;

    public WizardNavigator(IReadOnlyList<IWizardPage> pages)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("Мастеру нужна хотя бы одна страница", nameof(pages));
        }

        _pages = pages;

        foreach (var page in _pages)
        {
            page.CanGoNextChanged += OnPageReadinessChanged;
        }

        Current.OnEnter();
    }

    public IWizardPage Current => _pages[_index];

    public bool CanGoBack => _index > 0;

    public bool CanGoNext => _index < _pages.Count - 1 && Current.CanGoNext;

    public event EventHandler? CurrentChanged;

    public event EventHandler? CanGoNextChanged;

    public void GoNext()
    {
        if (!CanGoNext)
        {
            return;
        }

        _index++;
        Current.OnEnter();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _index--;
        Current.OnEnter();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPageReadinessChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current))
        {
            CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Шаг 6: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test test/WindowsPeace.Setup.Tests`
Ожидается: PASS, 8 тестов.

- [ ] **Шаг 7: Написать оболочку и заглушку**

Файл `src/WindowsPeace.Setup/Shell/ShellViewModel.cs`:

```csharp
using System;
using WindowsPeace.Setup.Infrastructure;

namespace WindowsPeace.Setup.Shell;

/// <summary>Состояние оболочки: заголовок, текущая страница, доступность переходов.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly WizardNavigator _navigator;

    public ShellViewModel(WizardNavigator navigator)
    {
        _navigator = navigator;

        BackCommand = new RelayCommand(_navigator.GoBack, () => _navigator.CanGoBack);
        NextCommand = new RelayCommand(_navigator.GoNext, () => _navigator.CanGoNext);

        _navigator.CurrentChanged += OnNavigationChanged;
        _navigator.CanGoNextChanged += OnReadinessChanged;
    }

    public object CurrentPage => _navigator.Current;

    public string Title => _navigator.Current.Title;

    public RelayCommand BackCommand { get; }

    public RelayCommand NextCommand { get; }

    private void OnNavigationChanged(object? sender, EventArgs e)
    {
        Raise(nameof(CurrentPage));
        Raise(nameof(Title));
        OnReadinessChanged(sender, e);
    }

    private void OnReadinessChanged(object? sender, EventArgs e)
    {
        BackCommand.RaiseCanExecuteChanged();
        NextCommand.RaiseCanExecuteChanged();
    }
}
```

Файл `src/WindowsPeace.Setup/Shell/ShellWindow.xaml`:

```xml
<Window x:Class="WindowsPeace.Setup.Shell.ShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Windows Peace" Height="720" Width="1024"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="{Binding Title}" FontSize="24" Margin="0,0,0,16" />

        <ContentControl Grid.Row="1" Content="{Binding CurrentPage}" />

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="Назад" Command="{Binding BackCommand}" Width="120" Height="34" Margin="0,0,12,0" />
            <Button Content="Далее" Command="{Binding NextCommand}" Width="120" Height="34" IsDefault="True" />
        </StackPanel>
    </Grid>
</Window>
```

Файл `src/WindowsPeace.Setup/Shell/ShellWindow.xaml.cs`:

```csharp
using System.Windows;

namespace WindowsPeace.Setup.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow() => InitializeComponent();
}
```

Файл `src/WindowsPeace.Setup/Pages/PlaceholderViewModel.cs`:

```csharp
using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Заглушка следующего шага. Нужна не для вида: без второй страницы
/// переходы оболочки нечем проверить.
/// </summary>
public sealed class PlaceholderViewModel : IWizardPage
{
    public string Title => "Дальше будет установка";

    public bool CanGoNext => false;

    public event EventHandler? CanGoNextChanged
    {
        add { }
        remove { }
    }

    public void OnEnter()
    {
    }
}
```

Файл `src/WindowsPeace.Setup/Pages/PlaceholderPage.xaml`:

```xml
<UserControl x:Class="WindowsPeace.Setup.Pages.PlaceholderPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <TextBlock VerticalAlignment="Center" HorizontalAlignment="Center" TextWrapping="Wrap"
               MaxWidth="520" TextAlignment="Center" FontSize="16"
               Text="Этот экран появится на шаге В. Сейчас установка ещё ничего не записывает на диск." />
</UserControl>
```

Файл `src/WindowsPeace.Setup/Pages/PlaceholderPage.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace WindowsPeace.Setup.Pages;

public partial class PlaceholderPage : UserControl
{
    public PlaceholderPage() => InitializeComponent();
}
```

- [ ] **Шаг 8: Коммит**

```bash
git add -A
git commit -m "Оболочка мастера: страницы, переходы, заглушка следующего шага"
```

---

## Задача 11: Экран выбора диска

**Файлы:**
- Создать: `src/WindowsPeace.Setup/Pages/DiskPickerViewModel.cs`, `DiskRowViewModel.cs`, `DiskPickerPage.xaml`, `DiskPickerPage.xaml.cs`
- Изменить: `src/WindowsPeace.Setup/App.xaml`, `App.xaml.cs`
- Тест: `test/WindowsPeace.Setup.Tests/DiskPickerViewModelTests.cs`

**Интерфейсы:**
- Потребляет: `IDiskEnumerator`, `IDiskContentInspector`, `BootMediaLocator`, `SelectionRules`, `DeploymentPlanner`.
- Отдаёт дальше: работающее приложение.

- [ ] **Шаг 1: Написать падающие тесты**

Файл `test/WindowsPeace.Setup.Tests/DiskPickerViewModelTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

internal sealed class FakeEnumerator : IDiskEnumerator
{
    private readonly DiskSnapshot _snapshot;

    public FakeEnumerator(DiskSnapshot snapshot) => _snapshot = snapshot;

    public DiskSnapshot Enumerate(CancellationToken cancellationToken) => _snapshot;
}

internal sealed class NoopInspector : IDiskContentInspector
{
    public void Inspect(DiskInfo disk, CancellationToken cancellationToken)
    {
    }
}

public class DiskPickerViewModelTests
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    private static DiskInfo Disk(string serial, ulong size, bool isSystem = false, IReadOnlyList<PartitionInfo>? partitions = null)
    {
        var list = partitions ?? new List<PartitionInfo>();
        return new DiskInfo(
            DiskIdentity.Create(serial, null, null, null, null, "Диск " + serial, size, BusType.Nvme),
            number: 0, friendlyName: "Диск " + serial, media: MediaKind.Ssd,
            partitionStyle: PartitionStyle.Gpt, isSystem: isSystem, isBoot: false,
            isOffline: false, isReadOnly: false, isRemovable: false,
            partitions: list, freeSpaces: FreeSpaceCalculator.Calculate(size, list), probeError: null);
    }

    private static DiskPickerViewModel Create(params DiskInfo[] disks)
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(new DiskSnapshot(disks, null)),
            new NoopInspector());
        model.Refresh();
        return model;
    }

    [Fact]
    public void Диски_попадают_в_список()
    {
        var model = Create(Disk("A", 500 * Gib), Disk("B", 1000 * Gib));

        Assert.Equal(2, model.Rows.Count(r => r.Kind == RowKind.Disk));
    }

    [Fact]
    public void Разделы_идут_строками_под_своим_диском()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = Create(Disk("A", 500 * Gib, partitions: new[] { partition }));

        Assert.Equal(RowKind.Disk, model.Rows[0].Kind);
        Assert.Equal(RowKind.Partition, model.Rows[1].Kind);
    }

    [Fact]
    public void Незанятое_пространство_показывается_отдельной_строкой()
    {
        var model = Create(Disk("A", 500 * Gib));

        Assert.Contains(model.Rows, r => r.Kind == RowKind.FreeSpace);
    }

    [Fact]
    public void Пока_ничего_не_выбрано_идти_дальше_нельзя()
    {
        var model = Create(Disk("A", 500 * Gib));

        Assert.False(model.CanGoNext);
    }

    [Fact]
    public void Выбор_допустимого_диска_разрешает_идти_дальше_и_строит_план()
    {
        var model = Create(Disk("A", 500 * Gib));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.True(model.CanGoNext);
        Assert.Contains("EFI", model.PlanSummary);
    }

    [Fact]
    public void Выбор_запрещённого_диска_не_разрешает_идти_дальше_и_объясняет_причину()
    {
        var model = Create(Disk("A", 500 * Gib, isSystem: true));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.False(model.CanGoNext);
        Assert.False(string.IsNullOrEmpty(model.DenialReason));
    }

    [Fact]
    public void Кнопки_разделов_включаются_по_виду_выбранной_строки()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = Create(Disk("A", 500 * Gib, partitions: new[] { partition }));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Partition);
        Assert.True(model.CanDelete);
        Assert.False(model.CanCreate);

        model.Selected = model.Rows.First(r => r.Kind == RowKind.FreeSpace);
        Assert.True(model.CanCreate);
        Assert.False(model.CanDelete);
    }

    [Fact]
    public void Сбой_перечисления_показывается_текстом_и_список_остаётся_пустым()
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(DiskSnapshot.Failed("WMI недоступно")),
            new NoopInspector());

        model.Refresh();

        Assert.Empty(model.Rows);
        Assert.Equal("WMI недоступно", model.EnumerationError);
    }
}
```

- [ ] **Шаг 2: Запустить тесты и убедиться, что они падают**

Выполнить: `dotnet test test/WindowsPeace.Setup.Tests --filter DiskPickerViewModelTests`
Ожидается: FAIL, типы не найдены.

- [ ] **Шаг 3: Написать строку списка**

Файл `src/WindowsPeace.Setup/Pages/DiskRowViewModel.cs`:

```csharp
using System.Globalization;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Pages;

/// <summary>Что представляет строка списка.</summary>
public enum RowKind
{
    Disk,
    Partition,
    FreeSpace,
}

/// <summary>Одна строка двухуровневого списка. Плоский список с отступом проще дерева и ведёт себя предсказуемее.</summary>
public sealed class DiskRowViewModel
{
    private DiskRowViewModel(RowKind kind, SelectionTarget target, string name, string size, string free, string type, string note)
    {
        Kind = kind;
        Target = target;
        Name = name;
        Size = size;
        Free = free;
        Type = type;
        Note = note;
        Verdict = SelectionRules.Evaluate(target);
    }

    public RowKind Kind { get; }
    public SelectionTarget Target { get; }
    public string Name { get; }
    public string Size { get; }
    public string Free { get; }
    public string Type { get; }
    public string Note { get; }
    public SelectionVerdict Verdict { get; }

    public int Indent => Kind == RowKind.Disk ? 0 : 24;
    public bool IsSelectable => Verdict.IsAllowed;

    public static DiskRowViewModel ForDisk(DiskInfo disk)
        => new(RowKind.Disk, SelectionTarget.WholeDisk(disk),
            disk.FriendlyName,
            Format(disk.Identity.SizeBytes),
            string.Empty,
            DescribeBus(disk),
            DescribeDisk(disk));

    public static DiskRowViewModel ForPartition(DiskInfo disk, PartitionInfo partition)
        => new(RowKind.Partition, SelectionTarget.Partition(disk, partition),
            DescribePartitionName(partition),
            Format(partition.Size),
            partition.Volume is null ? "—" : Format(partition.Volume.FreeBytes),
            DescribeKind(partition.Kind),
            DescribeContent(partition));

    public static DiskRowViewModel ForFreeSpace(DiskInfo disk, FreeSpaceInfo freeSpace)
        => new(RowKind.FreeSpace, SelectionTarget.FreeSpace(disk, freeSpace),
            "Незанятое пространство", Format(freeSpace.Size), string.Empty, "—", string.Empty);

    private static string DescribePartitionName(PartitionInfo partition)
    {
        var label = partition.Volume?.Label;
        var letter = partition.DriveLetter is null ? string.Empty : " (" + partition.DriveLetter + ":)";
        var name = string.IsNullOrWhiteSpace(label)
            ? string.Format(CultureInfo.CurrentCulture, "Раздел {0}", partition.Number)
            : string.Format(CultureInfo.CurrentCulture, "Раздел {0}: {1}", partition.Number, label);
        return name + letter;
    }

    private static string DescribeKind(PartitionKind kind) => kind switch
    {
        PartitionKind.EfiSystem => "Системный EFI",
        PartitionKind.MicrosoftReserved => "MSR",
        PartitionKind.WindowsRecovery => "Восстановление",
        PartitionKind.BasicData => "Основной",
        _ => "Неизвестный",
    };

    private static string DescribeBus(DiskInfo disk)
    {
        var media = disk.Media switch
        {
            MediaKind.Ssd => "SSD",
            MediaKind.Hdd => "HDD",
            MediaKind.Scm => "SCM",
            _ => string.Empty,
        };

        return (disk.Identity.BusType + " " + media).Trim();
    }

    private static string DescribeDisk(DiskInfo disk)
    {
        if (disk.IsWindowsPeaceMedia) return "Загрузочный носитель — установка сюда невозможна";
        if (disk.IsSystem || disk.IsBoot) return "Здесь работает текущая система";
        if (disk.ProbeError is not null) return disk.ProbeError;
        if (disk.Partitions.Count == 0) return "Пустой";
        return string.Format(CultureInfo.CurrentCulture, "Разделов: {0}", disk.Partitions.Count);
    }

    private static string DescribeContent(PartitionInfo partition)
    {
        if (!partition.Content.Inspected) return partition.Content.NotInspectedReason ?? string.Empty;
        if (partition.Content.WindowsFound && partition.Content.UserFilesFound) return "Windows и файлы пользователя";
        if (partition.Content.WindowsFound) return "Windows";
        if (partition.Content.UserFilesFound) return "Файлы пользователя";
        return string.Empty;
    }

    private static string Format(ulong bytes)
    {
        const ulong Mib = 1024UL * 1024UL;
        const ulong Gib = 1024UL * Mib;
        return bytes >= Gib
            ? ((double)bytes / Gib).ToString("0.#", CultureInfo.CurrentCulture) + " ГБ"
            : (bytes / Mib).ToString(CultureInfo.CurrentCulture) + " МБ";
    }
}
```

- [ ] **Шаг 4: Написать модель экрана**

Файл `src/WindowsPeace.Setup/Pages/DiskPickerViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Состояние экрана выбора диска. Решения о допустимости и предупреждениях
/// принимает Core; здесь только показ и переключение доступности кнопок.
/// </summary>
public sealed class DiskPickerViewModel : ViewModelBase, IWizardPage
{
    private readonly IDiskEnumerator _enumerator;
    private readonly IDiskContentInspector _inspector;

    private DiskRowViewModel? _selected;
    private string _planSummary = string.Empty;
    private string? _denialReason;
    private string? _enumerationError;
    private IReadOnlyList<DiskInfo> _disks = Array.Empty<DiskInfo>();

    public DiskPickerViewModel(IDiskEnumerator enumerator, IDiskContentInspector inspector)
    {
        _enumerator = enumerator;
        _inspector = inspector;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public string Title => "Куда установить Windows?";

    public ObservableCollection<DiskRowViewModel> Rows { get; } = new();

    public ObservableCollection<PlanWarning> Warnings { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public DiskRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                UpdateSelection();
            }
        }
    }

    public string PlanSummary
    {
        get => _planSummary;
        private set => Set(ref _planSummary, value);
    }

    public string? DenialReason
    {
        get => _denialReason;
        private set => Set(ref _denialReason, value);
    }

    public string? EnumerationError
    {
        get => _enumerationError;
        private set => Set(ref _enumerationError, value);
    }

    public bool CanGoNext => Selected?.IsSelectable == true;

    public bool CanCreate => Selected?.Kind == RowKind.FreeSpace;

    public bool CanDelete => Selected?.Kind == RowKind.Partition;

    public bool CanFormat => Selected?.Kind == RowKind.Partition && Selected.Target.Partition?.Volume is not null;

    public bool CanExtend => Selected?.Kind == RowKind.Partition && HasAdjacentFreeSpace();

    public bool CanShowDetails => Selected is not null;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        if (Rows.Count == 0 && EnumerationError is null)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        Rows.Clear();
        Warnings.Clear();
        Selected = null;

        using var cts = new CancellationTokenSource(WindowsPeace.Core.Diagnostics.Timeouts.DiskEnumeration);
        var snapshot = _enumerator.Enumerate(cts.Token);

        EnumerationError = snapshot.EnumerationError;
        _disks = snapshot.Disks;

        foreach (var disk in _disks)
        {
            _inspector.Inspect(disk, cts.Token);

            Rows.Add(DiskRowViewModel.ForDisk(disk));

            foreach (var partition in disk.Partitions)
            {
                Rows.Add(DiskRowViewModel.ForPartition(disk, partition));
            }

            foreach (var gap in disk.FreeSpaces)
            {
                Rows.Add(DiskRowViewModel.ForFreeSpace(disk, gap));
            }
        }
    }

    private void UpdateSelection()
    {
        Warnings.Clear();
        DenialReason = null;
        PlanSummary = string.Empty;

        if (Selected is not null)
        {
            DenialReason = Selected.Verdict.Reason;

            if (Selected.IsSelectable)
            {
                PlanSummary = DeploymentPlanner.Build(Selected.Target).Summary;

                foreach (var warning in SelectionRules.Warnings(Selected.Target, _disks))
                {
                    Warnings.Add(warning);
                }
            }
        }

        Raise(nameof(CanCreate));
        Raise(nameof(CanDelete));
        Raise(nameof(CanFormat));
        Raise(nameof(CanExtend));
        Raise(nameof(CanShowDetails));
        Raise(nameof(CanGoNext));
        CanGoNextChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool HasAdjacentFreeSpace()
    {
        var partition = Selected?.Target.Partition;
        return partition is not null
               && Selected!.Target.Disk.FreeSpaces.Any(gap => gap.Offset == partition.End);
    }
}
```

- [ ] **Шаг 5: Запустить тесты и убедиться, что они проходят**

Выполнить: `dotnet test test/WindowsPeace.Setup.Tests --filter DiskPickerViewModelTests`
Ожидается: PASS, 8 тестов.

- [ ] **Шаг 6: Написать разметку экрана**

Файл `src/WindowsPeace.Setup/Pages/DiskPickerPage.xaml`:

```xml
<UserControl x:Class="WindowsPeace.Setup.Pages.DiskPickerPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ListView Grid.Row="0" ItemsSource="{Binding Rows}" SelectedItem="{Binding Selected}">
            <ListView.ItemContainerStyle>
                <Style TargetType="ListViewItem">
                    <Setter Property="IsEnabled" Value="{Binding IsSelectable}" />
                </Style>
            </ListView.ItemContainerStyle>
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Имя" Width="360">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal">
                                    <Border Width="{Binding Indent}" />
                                    <TextBlock Text="{Binding Name}" />
                                </StackPanel>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                    <GridViewColumn Header="Объём" Width="110" DisplayMemberBinding="{Binding Size}" />
                    <GridViewColumn Header="Свободно" Width="110" DisplayMemberBinding="{Binding Free}" />
                    <GridViewColumn Header="Тип" Width="150" DisplayMemberBinding="{Binding Type}" />
                    <GridViewColumn Header="Состояние" Width="260" DisplayMemberBinding="{Binding Note}" />
                </GridView>
            </ListView.View>
        </ListView>

        <WrapPanel Grid.Row="1" Margin="0,12,0,0">
            <Button Content="Обновить" Command="{Binding RefreshCommand}" Margin="0,0,12,0" Padding="10,4" />
            <Button Content="Создать" IsEnabled="{Binding CanCreate}" Margin="0,0,12,0" Padding="10,4" Click="NotYet" />
            <Button Content="Удалить" IsEnabled="{Binding CanDelete}" Margin="0,0,12,0" Padding="10,4" Click="NotYet" />
            <Button Content="Форматировать" IsEnabled="{Binding CanFormat}" Margin="0,0,12,0" Padding="10,4" Click="NotYet" />
            <Button Content="Расширить" IsEnabled="{Binding CanExtend}" Margin="0,0,12,0" Padding="10,4" Click="NotYet" />
            <Button Content="Подробно" IsEnabled="{Binding CanShowDetails}" Margin="0,0,12,0" Padding="10,4" Click="NotYet" />
            <Button Content="Загрузить драйвер" Padding="10,4" Click="NotYet" />
        </WrapPanel>

        <TextBlock Grid.Row="2" Margin="0,12,0,0" Foreground="#B00020" TextWrapping="Wrap"
                   Text="{Binding EnumerationError}" />

        <ItemsControl Grid.Row="3" ItemsSource="{Binding Warnings}" Margin="0,12,0,0">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding Text}" TextWrapping="Wrap" Margin="0,0,0,4" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <StackPanel Grid.Row="4" Margin="0,12,0,0">
            <TextBlock Text="{Binding DenialReason}" Foreground="#B00020" TextWrapping="Wrap" />
            <TextBlock Text="{Binding PlanSummary}" TextWrapping="Wrap" />
        </StackPanel>
    </Grid>
</UserControl>
```

Файл `src/WindowsPeace.Setup/Pages/DiskPickerPage.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;

namespace WindowsPeace.Setup.Pages;

public partial class DiskPickerPage : UserControl
{
    public DiskPickerPage() => InitializeComponent();

    /// <summary>
    /// Операции над разделами появятся на шаге В. Кнопки существуют уже сейчас,
    /// чтобы их доступность проектировалась вместе со списком.
    /// </summary>
    private void NotYet(object sender, RoutedEventArgs e)
        => MessageBox.Show(
            "Эта операция появится на следующем шаге. Сейчас программа ничего не записывает на диск.",
            "Windows Peace",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
}
```

- [ ] **Шаг 7: Собрать приложение воедино**

Файл `src/WindowsPeace.Setup/App.xaml`:

```xml
<Application x:Class="WindowsPeace.Setup.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:pages="clr-namespace:WindowsPeace.Setup.Pages">
    <Application.Resources>
        <DataTemplate DataType="{x:Type pages:DiskPickerViewModel}">
            <pages:DiskPickerPage />
        </DataTemplate>
        <DataTemplate DataType="{x:Type pages:PlaceholderViewModel}">
            <pages:PlaceholderPage />
        </DataTemplate>
    </Application.Resources>
</Application>
```

Файл `src/WindowsPeace.Setup/App.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup;

public partial class App : Application
{
    private JsonLinesOperationLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = new JsonLinesOperationLog(JsonLinesOperationLog.DefaultPath(AppContext.BaseDirectory));

        var probe = new RealFileSystemProbe();

        var diskPicker = new DiskPickerViewModel(
            new WmiDiskEnumerator(_log),
            new FileSystemContentInspector(probe));

        var navigator = new WizardNavigator(new List<IWizardPage>
        {
            diskPicker,
            new PlaceholderViewModel(),
        });

        new ShellWindow { DataContext = new ShellViewModel(navigator) }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Dispose();
        base.OnExit(e);
    }
}
```

Пометка о загрузочном носителе проставляется после перечисления. Добавь в `DiskPickerViewModel.Refresh` вызов `BootMediaLocator.Mark(_disks, probe)` — для этого передай `IFileSystemProbe` третьим параметром конструктора и обнови тесты, добавив в них `new RealFileSystemProbe()`.

- [ ] **Шаг 8: Запустить приложение и сверить с живой машиной**

Выполнить: `dotnet run --project src/WindowsPeace.Setup`

Проверить по списку:

1. окно открылось, заголовок «Куда установить Windows?»;
2. видны все диски машины, объёмы совпадают с `diskmgmt.msc`;
3. разделы показаны под своими дисками с отступом;
4. диск с работающей системой выбрать нельзя, и написано почему;
5. выбор диска целиком даёт строку разметки с четырьмя разделами;
6. выбор раздела даёт другую строку — про один раздел;
7. кнопки «Создать», «Удалить», «Форматировать», «Расширить» включаются по правилам из таблицы спеки;
8. нажатие любой из них показывает сообщение «появится на следующем шаге»;
9. кнопка «Далее» уводит на заглушку, «Назад» возвращает;
10. в `logs\windows-peace.jsonl` рядом с приложением есть записи о перечислении.

- [ ] **Шаг 9: Записать результат проверки**

Создать `docs/superpowers/notes/2026-08-10-step-a-acceptance.md` с ответами по всем десяти пунктам и с выводом `DiskDump` на настоящей машине. Отдельно записать, читаются ли серийные номера дисков и какой уровень доверия получился — это ответ на открытый вопрос из спеки.

- [ ] **Шаг 10: Коммит**

```bash
git add -A
git commit -m "Экран выбора диска и сборка приложения воедино"
git push origin main
```

---

## Самопроверка плана

**Покрытие спеки.** Раздел 4 спеки (экран) — задачи 10 и 11. Раздел 5 (правила выбора и доступность кнопок) — задачи 6 и 11. Раздел 6 (данные, отпечаток, содержимое, носитель) — задачи 2, 3, 5, 8, 9. Раздел 7 (устройство кода) — задачи 1, 4. Раздел 8 (ошибки и отказоустойчивость) — задача 4 плюс обработка в задачах 8, 9, 11. Раздел 9 (проверка) — тесты в каждой задаче и ручная сверка в задачах 9 и 11. Раздел 10 (неизвестное) — вопрос о серийных номерах закрывается записью в задачах 9 и 11.

**Незакрытое намеренно.** Экран выбора рецепта относится к шагу Б и в план не входит — так сказано в спеке. Настоящие операции над разделами относятся к шагу В.

**Согласованность имён.** `DiskIdentity.Create` — задача 2, используется в 9 и в тестах 6 и 11. `PartitionKinds.FromGptType` — задача 3, используется в 9. `FreeSpaceCalculator.Calculate` — задача 5, используется в 9 и в тестовых сборках. `SelectionRules.Evaluate` и `SelectionRules.Warnings` — задача 6, используются в 11. `DeploymentPlanner.Build` — задача 7, используется в 11. `IFileSystemProbe` — задача 8, используется в 9 и 11. `IWizardPage` и `WizardNavigator` — задача 10, используются в 11.
