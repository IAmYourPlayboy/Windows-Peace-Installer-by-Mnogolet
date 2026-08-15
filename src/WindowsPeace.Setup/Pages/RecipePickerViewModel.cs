using System;
using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран «что ставим». Ничего не выбирает за человека даже тогда, когда
/// рецепт на носителе единственный: он должен видеть, что именно ставит.
///
/// Все четыре беды с описью — не разбирается, собрана более новой версией,
/// рецептов нет, носителя нет вовсе — приводят сюда же: объяснение словами
/// и выключенная кнопка «Далее». Молча пропускать нельзя ни одну из них:
/// дальше по пути форматирование диска.
/// </summary>
public sealed class RecipePickerViewModel : ViewModelBase, IWizardPage
{
    private readonly List<RecipeRowViewModel> _recipes = new();

    private RecipeRowViewModel? _selectedRow;

    public RecipePickerViewModel(MediaManifestResult result)
        : this(result.Message)
    {
        if (result.Status != MediaManifestStatus.Ok)
        {
            return;
        }

        foreach (var recipe in result.Manifest!.Recipes)
        {
            _recipes.Add(new RecipeRowViewModel(recipe));
        }
    }

    private RecipePickerViewModel(string trouble)
    {
        Trouble = trouble;
    }

    /// <summary>
    /// Носитель не найден ни на одном разделе. Где именно искали — в журнале:
    /// человеку у флешки этот список ничего не объясняет, а разбираться по нему
    /// будем мы.
    /// </summary>
    public static RecipePickerViewModel WithoutMedia()
        => new("Носитель Windows Peace не найден: похоже, мастер запущен не с него. Ставить отсюда нечего.");

    public string Title => "Что ставим?";

    public IReadOnlyList<RecipeRowViewModel> Recipes => _recipes;

    /// <summary>Есть ли что показывать. Пустая таблица с заголовками ничего не объясняет.</summary>
    public bool HasRecipes => _recipes.Count > 0;

    /// <summary>
    /// Пусто, когда всё в порядке. Иначе — объяснение для человека, и только оно:
    /// техническая причина живёт в журнале. Разбираться в наших ошибках человеку
    /// у флешки незачем, это наша работа.
    /// </summary>
    public string Trouble { get; }

    public bool HasTrouble => !string.IsNullOrEmpty(Trouble);

    public RecipeRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (Set(ref _selectedRow, value))
            {
                Raise(nameof(SelectedRecipe));
                Raise(nameof(CanGoNext));
                CanGoNextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Что выбрано. Этим пользуется экран «проверьте и подтвердите».</summary>
    public MediaRecipe? SelectedRecipe => _selectedRow?.Recipe;

    public bool CanGoNext => _selectedRow is not null;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
    }
}
