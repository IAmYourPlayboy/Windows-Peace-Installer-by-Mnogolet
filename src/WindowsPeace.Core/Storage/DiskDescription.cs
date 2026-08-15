namespace WindowsPeace.Core.Storage;

/// <summary>
/// Диск, названный словами для человека.
///
/// Одно место на весь проект. Одно и то же описание читается в списке выбора
/// и в сводке перед установкой; разойдясь, они читались бы как два разных
/// диска — а человек в этот момент решает, что стирать.
/// </summary>
public static class DiskDescription
{
    /// <summary>Чем части разделяются в строке. Тот же знак, что в предпросмотре разметки.</summary>
    private const string Separator = " · ";

    /// <summary>Шина и тип носителя одной строкой: «Sata HDD», «Usb».</summary>
    public static string Bus(DiskInfo disk)
    {
        var bus = disk.Identity.BusType.ToString();
        var media = Media(disk.Media);

        // Флешки и виртуальные диски о типе носителя не сообщают — и это не ошибка.
        // Висячий пробел после шины в таком случае недопустим.
        return media.Length == 0 ? bus : bus + " " + media;
    }

    /// <summary>Объём, шина и опознавательный признак одной строкой.</summary>
    public static string Summary(DiskInfo disk)
        => ByteSize.Format(disk.Identity.SizeBytes) + Separator + Bus(disk) + Separator + Identification(disk);

    private static string Media(MediaKind media) => media switch
    {
        MediaKind.Ssd => "SSD",
        MediaKind.Hdd => "HDD",
        MediaKind.Scm => "SCM",
        _ => string.Empty,
    };

    /// <summary>
    /// Чем диск опознан. Признак из разметки за серийный номер не выдаётся:
    /// он меняется при переразметке, а человек по этой строке сверяет наклейку
    /// на корпусе. Назвать его серийным номером — значит соврать в том самом
    /// месте, где человек проверяет, тот ли это диск.
    /// </summary>
    private static string Identification(DiskInfo disk) => disk.Identity.Confidence switch
    {
        IdentityConfidence.Hardware => "серийный номер " + disk.Identity.SerialNumber,
        IdentityConfidence.Volatile => "серийного номера нет, диск опознан по разметке",
        _ => "серийный номер не читается",
    };
}
