using System;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

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
    private string _planEffect = string.Empty;
    private string _trouble = string.Empty;

    /// <summary>
    /// Собрана ли сводка. До входа на экран показывать нечего, и пускать дальше
    /// тоже.
    /// </summary>
    private bool _described;

    public ConfirmViewModel(IWizardChoice choice)
    {
        _choice = choice;
    }

    public string Title => "Проверьте и подтвердите";

    /// <summary>
    /// После этого экрана начинается работа с диском. Кнопка обязана называть
    /// действие своим словом: «Далее» здесь значило бы «сотрите мой диск».
    /// </summary>
    public string NextTitle => "Установить";

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
    public string PlanEffect
    {
        get => _planEffect;
        private set
        {
            if (Set(ref _planEffect, value))
            {
                Raise(nameof(HasPlanEffect));
            }
        }
    }

    /// <summary>Есть ли что сказать про судьбу нынешнего содержимого диска.</summary>
    public bool HasPlanEffect => !string.IsNullOrEmpty(_planEffect);

    /// <summary>
    /// Пусто, когда всё в порядке. Иначе — объяснение для человека, и только оно:
    /// техническая причина живёт в журнале.
    /// </summary>
    public string Trouble
    {
        get => _trouble;
        private set
        {
            if (Set(ref _trouble, value))
            {
                Raise(nameof(HasTrouble));
            }
        }
    }

    public bool HasTrouble => !string.IsNullOrEmpty(_trouble);

    /// <summary>
    /// Войдя на экран с выбранным рецептом и диском, дальше можно идти сразу:
    /// подтверждать вводом больше нечего. Не пускает только потерянный выбор.
    /// </summary>
    public bool CanGoNext => _described && !HasTrouble;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        Describe();

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
            Trouble = "Выбор потерялся: вернитесь назад и укажите, что ставим и куда.";
            return;
        }

        var plan = DeploymentPlanner.Build(target);

        _described = true;
        RecipeName = recipe.Name;
        DiskModel = target.Disk.Identity.Model.Trim();
        DiskSummary = DiskDescription.Summary(target.Disk);
        PlanSummary = plan.Summary;
        PlanEffect = plan.WipesWholeDisk
            ? "Диск будет размечен заново. Всё, что на нём сейчас есть, исчезнет безвозвратно."
            : string.Empty;
        Trouble = string.Empty;
    }

    /// <summary>Забыть сводку целиком: показывать половину — хуже, чем ничего.</summary>
    private void Forget()
    {
        _described = false;
        RecipeName = string.Empty;
        DiskModel = string.Empty;
        DiskSummary = string.Empty;
        PlanSummary = string.Empty;
        PlanEffect = string.Empty;
    }
}
