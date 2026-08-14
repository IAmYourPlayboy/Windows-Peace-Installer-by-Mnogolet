using System;
using System.Collections.Generic;
using System.Globalization;
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

    public RecipePickerViewModel(MediaManifestResult result, Action closeWizard)
        : this(result.Message, result.Detail, closeWizard)
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

    private RecipePickerViewModel(string trouble, string? troubleDetail, Action closeWizard)
    {
        Trouble = trouble;
        TroubleDetail = troubleDetail ?? string.Empty;

        // Кнопка нужна только в тупике. В WinPE окно занимает весь экран
        // и креста на нём нет: не предложи мы выход — человеку осталось бы
        // только обесточить машину.
        CloseCommand = new RelayCommand(closeWizard, () => HasTrouble);
    }

    /// <summary>
    /// Носитель не найден ни на одном разделе. Перечисляем, где искали:
    /// по этому списку видно, что мастер запущен не с носителя Windows Peace.
    /// </summary>
    public static RecipePickerViewModel WithoutMedia(IReadOnlyList<string> checkedRoots, Action closeWizard)
    {
        var where = checkedRoots.Count == 0
            ? "разделов на этой машине не нашлось вовсе"
            : string.Format(CultureInfo.CurrentCulture,
                "искали в корне каждого раздела: {0}", string.Join(", ", checkedRoots));

        return new RecipePickerViewModel(
            "Носитель Windows Peace не найден: похоже, мастер запущен не с него. Ставить отсюда нечего.",
            string.Format(CultureInfo.CurrentCulture,
                "Файл описи «{0}» — {1}.", MediaLayout.ManifestFileName, where),
            closeWizard);
    }

    public string Title => "Что ставим?";

    public IReadOnlyList<RecipeRowViewModel> Recipes => _recipes;

    /// <summary>Есть ли что показывать. Пустая таблица с заголовками ничего не объясняет.</summary>
    public bool HasRecipes => _recipes.Count > 0;

    /// <summary>Пусто, когда всё в порядке. Иначе — объяснение для человека.</summary>
    public string Trouble { get; }

    /// <summary>
    /// Техническая причина беды: текст разборщика, отказ файловой системы,
    /// где искали опись. Показывается второй строкой и помельче — объяснять
    /// ею ничего нельзя, но и прятать нечестно: человек снимет экран на телефон
    /// и покажет тому, кто записывал ему носитель.
    /// </summary>
    public string TroubleDetail { get; }

    public bool HasTrouble => !string.IsNullOrEmpty(Trouble);

    public bool HasTroubleDetail => HasTrouble && !string.IsNullOrEmpty(TroubleDetail);

    /// <summary>Выход из тупика. Доступна только тогда, когда идти дальше некуда.</summary>
    public RelayCommand CloseCommand { get; }

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
