using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Что человек выбрал к этому моменту.
///
/// Экран подтверждения читает это при каждом входе, а не при создании:
/// в момент создания диск ещё не выбран. Отдельным интерфейсом — чтобы экран
/// не знал о соседних экранах и проверялся без них, а на шаге В отсюда же
/// брала выбор сама установка.
/// </summary>
public interface IWizardChoice
{
    /// <summary>Что ставим. Пусто, пока рецепт не выбран.</summary>
    MediaRecipe? Recipe { get; }

    /// <summary>Куда ставим. Пусто, пока цель не выбрана.</summary>
    SelectionTarget? Target { get; }

    /// <summary>Все диски машины: по ним видно, не стоит ли Windows на соседнем.</summary>
    IReadOnlyList<DiskInfo> Disks { get; }
}
