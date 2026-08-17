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

    /// <summary>
    /// Объём и шина одной строкой: «500 ГБ · Sata HDD».
    ///
    /// Серийный номер (отпечаток диска) убран по приёмке 17.08.2026: длинная
    /// строка на экране мешала, а сверяет диск человек по модели и объёму.
    /// Отпечаток остаётся в данных диска и в журнале.
    /// </summary>
    public static string Summary(DiskInfo disk)
        => ByteSize.Format(disk.Identity.SizeBytes) + Separator + Bus(disk);

    private static string Media(MediaKind media) => media switch
    {
        MediaKind.Ssd => "SSD",
        MediaKind.Hdd => "HDD",
        MediaKind.Scm => "SCM",
        _ => string.Empty,
    };
}
