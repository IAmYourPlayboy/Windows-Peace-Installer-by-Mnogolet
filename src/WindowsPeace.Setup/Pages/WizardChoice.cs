using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Сводит выбор с трёх первых экранов в одно место. Сам ничего не решает,
/// только пересказывает: благодаря ему экран подтверждения не знает о соседях,
/// а соседи — о нём.
/// </summary>
public sealed class WizardChoice : IWizardChoice
{
    private readonly RecipePickerViewModel _recipes;
    private readonly DiskPickerViewModel _disks;
    private readonly LanguageViewModel _language;

    public WizardChoice(RecipePickerViewModel recipes, DiskPickerViewModel disks, LanguageViewModel language)
    {
        _recipes = recipes;
        _disks = disks;
        _language = language;
    }

    public MediaRecipe? Recipe => _recipes.SelectedRecipe;

    public SelectionTarget? Target => _disks.Selected?.Target;

    public IReadOnlyList<DiskInfo> Disks => _disks.Disks;

    /// <summary>
    /// По умолчанию русский: до входа на экран языка выбора ещё нет, а сюда
    /// попадать без выбора не должно, но пустое значение тут недопустимо —
    /// шаг В ждёт конкретный язык.
    /// </summary>
    public CoreLocalization.Language SystemLanguage => _language.Selected?.Language ?? CoreLocalization.Language.Russian;
}
