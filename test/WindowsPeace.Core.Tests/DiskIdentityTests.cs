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
