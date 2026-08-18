using System;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Диск описывается словами дважды: в списке выбора и в сводке перед установкой.
/// Разойдясь, эти два описания читались бы как два разных диска — а человек
/// в этот момент решает, что стирать.
/// </summary>
[Collection(LocalizationCollection.Name)]
public class DiskDescriptionTests
{
    [Fact]
    public void Шина_и_тип_носителя_идут_одной_строкой()
    {
        var disk = TestDisks.Disk(bus: BusType.Sata, media: MediaKind.Hdd);

        Assert.Equal("Sata HDD", DiskDescription.Bus(disk));
    }

    /// <summary>
    /// Флешки и виртуальные диски на вопрос о типе носителя не отвечают.
    /// Это не ошибка, и висячего пробела после шины быть не должно.
    /// </summary>
    [Fact]
    public void Неизвестный_тип_носителя_не_оставляет_хвоста()
    {
        var disk = TestDisks.Disk(bus: BusType.Usb, media: MediaKind.Unspecified);

        Assert.Equal("Usb", DiskDescription.Bus(disk));
    }

    /// <summary>
    /// Сводка называет объём и шину. Серийный номер и признак опознания убраны
    /// по приёмке 17.08.2026: человек сверяет диск по модели и объёму, а длинная
    /// строка отпечатка на экране только мешала. Отпечаток остаётся в журнале.
    /// </summary>
    [Fact]
    public void Сводка_называет_объём_и_шину_без_серийного_номера()
    {
        var disk = TestDisks.Disk(serial: "Z9A1B2C3", size: 500 * TestDisks.Gib,
            bus: BusType.Sata, media: MediaKind.Hdd);

        var summary = DiskDescription.Summary(disk);

        Assert.Contains("500 ГБ", summary, StringComparison.Ordinal);
        Assert.Contains("Sata HDD", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("серийн", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Z9A1B2C3", summary, StringComparison.Ordinal);
    }
}
