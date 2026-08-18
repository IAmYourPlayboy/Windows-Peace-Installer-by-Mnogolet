using System;
using System.Collections.Generic;

namespace WindowsPeace.Core.Localization;

public sealed class RussianStrings : ILanguagePack
{
    public Language Language => Language.Russian;

    public IReadOnlyDictionary<string, string> Strings { get; } = new Dictionary<string, string>
    {
        [Keys.Common.Next] = "Далее",
        [Keys.Language.Title] = "Выберите язык",

        [Keys.Shell.Back] = "Назад",
        [Keys.Shell.Exit] = "Выйти из установщика",

        [Keys.Recipe.Title] = "Что ставим?",
        [Keys.Recipe.Intro] = "Выберите, что установить. В списке - то, что записано на этом носителе.",
        [Keys.Recipe.ColName] = "Рецепт",
        [Keys.Recipe.ColImage] = "Издание",
        [Keys.Recipe.ColSize] = "Объём",
        [Keys.Recipe.ColWhat] = "Что это",
        [Keys.Recipe.TroubleNotFound] =
            "Носитель Windows Peace не найден: похоже, мастер запущен не с него. Ставить отсюда нечего.",
        [Keys.Recipe.TroubleDamaged] =
            "Опись носителя испорчена: прочитать её не получается. Установить с этого носителя " +
            "ничего нельзя - его нужно записать заново.",
        [Keys.Recipe.TroubleTooNew] =
            "Носитель собран более новой версией Windows Peace. Установить с него нельзя - нужен мастер посвежее.",
        [Keys.Recipe.TroubleNoRecipes] = "На носителе нет ни одного рецепта: ставить с него нечего.",

        [Keys.Confirm.Title] = "Проверьте и подтвердите",
        [Keys.Confirm.Install] = "Установить",
        [Keys.Confirm.WhatLabel] = "Что ставим",
        [Keys.Confirm.WhereLabel] = "Куда ставим",
        [Keys.Confirm.EffectLabel] = "Что будет сделано",
        [Keys.Confirm.Wipe] =
            "Диск будет размечен заново. Всё, что на нём сейчас есть, исчезнет безвозвратно.",
        [Keys.Confirm.LostChoice] =
            "Выбор потерялся: вернитесь назад и укажите, что ставим и куда.",

        [Keys.Progress.Title] = "Установка",
        [Keys.Progress.Explanation] =
            "Здесь пойдёт разметка диска, распаковка Windows, установка драйверов и загрузчика. " +
            "Это появится на следующем шаге работы над программой. " +
            "Сейчас мастер ничего не записывает на диск.",

        [Keys.Done.Title] = "Готово",
        [Keys.Done.Explanation] =
            "Здесь будет итог установки и кнопка перезагрузки. " +
            "Это появится на следующем шаге работы над программой.",

        [Keys.Error.Title] = "Windows Peace",
        [Keys.Error.Body] =
            "Windows Peace не смог продолжить работу и сейчас закроется." + Environment.NewLine + Environment.NewLine +
            "Разбираться с этим нам, а не вам: что случилось, записано в журнал работы.",

        [Keys.Disk.Title] = "Куда установить Windows Peace?",
        [Keys.Disk.ColName] = "Имя",
        [Keys.Disk.ColSize] = "Объём",
        [Keys.Disk.ColFree] = "Свободно",
        [Keys.Disk.ColType] = "Тип",
        [Keys.Disk.ColState] = "Состояние",
        [Keys.Disk.Refresh] = "Обновить",
        [Keys.Disk.Cancel] = "Прервать",
        [Keys.Disk.Create] = "Создать",
        [Keys.Disk.Delete] = "Удалить",
        [Keys.Disk.Format] = "Форматировать",
        [Keys.Disk.Extend] = "Расширить",
        [Keys.Disk.Details] = "Подробно",
        [Keys.Disk.LoadDriver] = "Загрузить драйвер",
        [Keys.Disk.StatusEnumerating] = "Опрашиваю диски…",
        [Keys.Disk.StatusInspecting] = "Смотрю, что лежит на диске {0} из {1}…",
        [Keys.Disk.StatusLocating] = "Ищу загрузочный носитель…",
        [Keys.Disk.ErrorCancelled] = "Опрос дисков прерван. Нажмите «Обновить», чтобы попробовать снова.",
        [Keys.Disk.FreeSpace] = "Незанятое пространство",
        [Keys.Disk.Partition] = "Раздел {0}",
        [Keys.Disk.PartitionLabel] = "Раздел {0}: {1}",
        [Keys.Disk.NoteMedia] = "Загрузочный носитель - установка сюда невозможна",
        [Keys.Disk.NoteSystem] = "Здесь работает текущая система",
        [Keys.Disk.NoteEmpty] = "Пустой",
        [Keys.Disk.NotePartitions] = "Разделов: {0}",

        [Keys.PartitionType.Efi] = "Системный EFI",
        [Keys.PartitionType.Msr] = "MSR",
        [Keys.PartitionType.Recovery] = "Восстановление",
        [Keys.PartitionType.Basic] = "Основной",
        [Keys.PartitionType.Unknown] = "Неизвестный",

        [Keys.Content.WindowsAndFiles] = "Windows и файлы пользователя",
        [Keys.Content.Windows] = "Windows",
        [Keys.Content.UserFiles] = "Файлы пользователя",

        [Keys.Size.Gb] = "ГБ",
        [Keys.Size.Mb] = "МБ",
        [Keys.Size.LessThanMb] = "менее 1 МБ",

        [Keys.Sel.DenyMedia] = "Это загрузочный носитель Windows Peace - установка сюда невозможна",
        [Keys.Sel.DenySystem] = "На этом диске работает текущая система",
        [Keys.Sel.DenyOffline] = "Диск отключён",
        [Keys.Sel.DenyReadOnly] = "Диск защищён от записи",
        [Keys.Sel.DenyService] = "Это служебный раздел, система создаёт его сама",
        [Keys.Sel.DenyUnknownTarget] = "Неизвестный вид цели",
        [Keys.Sel.TooSmall] = "Слишком мало места: не хватает {0} ГБ до минимальных 40 ГБ",

        [Keys.Warn.WindowsOnTarget] = "На цели установлена Windows. Она будет удалена безвозвратно.",
        [Keys.Warn.UserFilesOnTarget] = "На цели есть файлы пользователя. Они будут удалены безвозвратно.",
        [Keys.Warn.PartitionsNotRead] = "Разделы этого диска прочитать не удалось, поэтому неизвестно, что на нём лежит.",
        [Keys.Warn.ContentNotInspected] = "Содержимое части разделов проверить не удалось: у них нет буквы диска.",
        [Keys.Warn.WeakIdentity] = "У диска не удалось прочитать серийный номер, опознать его надёжно нельзя.",
        [Keys.Warn.OtherWindows] = "На другом диске найдена установленная Windows. Она может перехватывать загрузку.",

        [Keys.Plan.Recovery] = "Восстановление",
        [Keys.Plan.SingleTail] = ". Остальные разделы не изменяются.",
    };
}
