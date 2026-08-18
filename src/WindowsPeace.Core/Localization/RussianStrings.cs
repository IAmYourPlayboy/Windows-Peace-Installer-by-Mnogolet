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
    };
}
