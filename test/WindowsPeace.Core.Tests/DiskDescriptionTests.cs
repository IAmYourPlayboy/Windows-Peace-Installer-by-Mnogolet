using System;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Диск описывается словами дважды: в списке выбора и в сводке перед установкой.
/// Разойдясь, эти два описания читались бы как два разных диска — а человек
/// в этот момент решает, что стирать.
/// </summary>
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

    [Fact]
    public void Сводка_называет_объём_шину_и_серийный_номер()
    {
        var disk = TestDisks.Disk(serial: "Z9A1B2C3", size: 500 * TestDisks.Gib,
            bus: BusType.Sata, media: MediaKind.Hdd);

        var summary = DiskDescription.Summary(disk);

        Assert.Contains("500 ГБ", summary, StringComparison.Ordinal);
        Assert.Contains("Sata HDD", summary, StringComparison.Ordinal);
        Assert.Contains("серийный номер Z9A1B2C3", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Когда серийного номера нет, диск опознаётся по разметке. Выдавать этот
    /// признак за серийный номер нельзя: он меняется при переразметке,
    /// а человек по нему сверяет наклейку на корпусе.
    /// </summary>
    [Fact]
    public void Признак_из_разметки_не_выдаётся_за_серийный_номер()
    {
        const string Guid = "{2f7a1b90-5c3d-4e11-9a02-6b8c7d4e5f60}";
        var disk = TestDisks.Disk(serial: null, gptGuid: Guid);

        var summary = DiskDescription.Summary(disk);

        Assert.DoesNotContain(Guid, summary, StringComparison.Ordinal);
        Assert.Contains("серийного номера нет", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Когда_опознать_нечем_об_этом_сказано_прямо()
    {
        var disk = TestDisks.Disk(serial: null);

        Assert.Contains("серийный номер не читается", DiskDescription.Summary(disk),
            StringComparison.Ordinal);
    }
}
