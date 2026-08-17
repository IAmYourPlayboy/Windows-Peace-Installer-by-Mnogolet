using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Сводит выбор с двух первых экранов в одно место. Сам ничего не решает,
/// только пересказывает: благодаря ему экран подтверждения не знает о соседях,
/// а соседи — о нём.
/// </summary>
public sealed class WizardChoice : IWizardChoice
{
    private readonly RecipePickerViewModel _recipes;
    private readonly DiskPickerViewModel _disks;

    public WizardChoice(RecipePickerViewModel recipes, DiskPickerViewModel disks)
    {
        _recipes = recipes;
        _disks = disks;
    }

    public MediaRecipe? Recipe => _recipes.SelectedRecipe;

    public SelectionTarget? Target => _disks.Selected?.Target;

    public IReadOnlyList<DiskInfo> Disks => _disks.Disks;
}
