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
