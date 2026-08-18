using System;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Сводка перед установкой - последний экран, где можно отступить.
///
/// Собирается при входе, а не при создании: в момент создания мастера диск
/// ещё не выбран, а человек может вернуться назад и выбрать другой.
///
/// Подтверждение вводом модели диска убрано по приёмке 17.08.2026: шаг Б ничего
/// на диск не пишет, а настоящий барьер стирания - задача шага В, где он и будет
/// спроектирован заново. См.
/// docs/superpowers/specs/2026-08-17-step-b-hardware-feedback-design.md.
/// </summary>
public sealed class ConfirmViewModel : ViewModelBase, IWizardPage
{
    private readonly IWizardChoice _choice;

    private string _recipeName = string.Empty;
    private string _diskModel = string.Empty;
    private string _diskSummary = string.Empty;
    private string _planSummary = string.Empty;

    /// <summary>
    /// Будет ли стёрт весь диск. Состояние, а не готовая строка: сводка
    /// собирается один раз при входе на экран (<see cref="OnEnter"/>), а язык
    /// можно сменить и после — тогда <see cref="PlanEffect"/> обязан заговорить
    /// на новом языке, а не остаться в том, что был при сборке.
    /// </summary>
    private bool _wipesWholeDisk;

    /// <summary>Выбор потерялся: ни рецепта, ни диска нет. См. <see cref="_wipesWholeDisk"/>.</summary>
    private bool _lostChoice;

    /// <summary>
    /// Собрана ли сводка. До входа на экран показывать нечего, и пускать дальше
    /// тоже.
    /// </summary>
    private bool _described;

    public ConfirmViewModel(IWizardChoice choice)
    {
        _choice = choice;
    }

    public string Title => CoreLocalization.Localization.Current[CoreLocalization.Keys.Confirm.Title];

    /// <summary>
    /// После этого экрана начинается работа с диском. Кнопка обязана называть
    /// действие своим словом: «Далее» здесь значило бы «сотрите мой диск».
    /// </summary>
    public string NextTitle => CoreLocalization.Localization.Current[CoreLocalization.Keys.Confirm.Install];

    /// <summary>Что ставим.</summary>
    public string RecipeName
    {
        get => _recipeName;
        private set => Set(ref _recipeName, value);
    }

    /// <summary>Модель целевого диска.</summary>
    public string DiskModel
    {
        get => _diskModel;
        private set => Set(ref _diskModel, value);
    }

    /// <summary>Объём и шина диска.</summary>
    public string DiskSummary
    {
        get => _diskSummary;
        private set => Set(ref _diskSummary, value);
    }

    /// <summary>Будущая разметка: какие разделы получатся и какого размера.</summary>
    public string PlanSummary
    {
        get => _planSummary;
        private set => Set(ref _planSummary, value);
    }

    /// <summary>
    /// Что случится с тем, что на диске есть сейчас. Пусто при установке в раздел:
    /// там эту мысль несёт сам PlanSummary, и повторять её дважды незачем.
    /// </summary>
    public string PlanEffect => _wipesWholeDisk
        ? CoreLocalization.Localization.Current[CoreLocalization.Keys.Confirm.Wipe]
        : string.Empty;

    /// <summary>Есть ли что сказать про судьбу нынешнего содержимого диска.</summary>
    public bool HasPlanEffect => !string.IsNullOrEmpty(PlanEffect);

    /// <summary>
    /// Пусто, когда всё в порядке. Иначе — объяснение для человека, и только оно:
    /// техническая причина живёт в журнале.
    /// </summary>
    public string Trouble => _lostChoice
        ? CoreLocalization.Localization.Current[CoreLocalization.Keys.Confirm.LostChoice]
        : string.Empty;

    public bool HasTrouble => !string.IsNullOrEmpty(Trouble);

    /// <summary>
    /// Войдя на экран с выбранным рецептом и диском, дальше можно идти сразу:
    /// подтверждать вводом больше нечего. Не пускает только потерянный выбор.
    /// </summary>
    public bool CanGoNext => _described && !HasTrouble;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        Describe();

        Raise(nameof(PlanEffect));
        Raise(nameof(HasPlanEffect));
        Raise(nameof(Trouble));
        Raise(nameof(HasTrouble));
        Raise(nameof(CanGoNext));
        CanGoNextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Describe()
    {
        var recipe = _choice.Recipe;
        var target = _choice.Target;

        if (recipe is null || target is null)
        {
            // Сюда нельзя попасть, не выбрав и то и другое: мастер не пускает
            // дальше без выбора. Но дальше по пути форматирование диска,
            // и показывать пустую сводку молча нельзя.
            Forget();
            _lostChoice = true;
            return;
        }

        var plan = DeploymentPlanner.Build(target);

        _described = true;
        RecipeName = recipe.Name;
        DiskModel = target.Disk.Identity.Model.Trim();
        DiskSummary = DiskDescription.Summary(target.Disk);
        PlanSummary = plan.Summary;
        _wipesWholeDisk = plan.WipesWholeDisk;
        _lostChoice = false;
    }

    /// <summary>Забыть сводку целиком: показывать половину — хуже, чем ничего.</summary>
    private void Forget()
    {
        _described = false;
        RecipeName = string.Empty;
        DiskModel = string.Empty;
        DiskSummary = string.Empty;
        PlanSummary = string.Empty;
        _wipesWholeDisk = false;
    }
}
