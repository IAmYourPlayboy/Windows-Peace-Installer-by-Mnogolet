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
