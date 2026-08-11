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
