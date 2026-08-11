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
