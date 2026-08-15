using System;
using System.Collections.Generic;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Сводка перед установкой — последний экран, где можно отступить.
///
/// Собирается при входе, а не при создании: в момент создания мастера диск
/// ещё не выбран, а человек может вернуться назад и выбрать другой.
///
/// Подтверждение вводом модели диска — требование раздела 8 архитектуры,
/// а не украшение: инструмент раздаётся незнакомым людям и стирает диски
/// с их данными, поэтому последнее действие должно быть осознанным.
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
    private bool _needsTypedConfirmation;
    private IReadOnlyList<PlanWarning> _warnings = Array.Empty<PlanWarning>();
    private string _typedModel = string.Empty;

    /// <summary>
    /// Собрана ли сводка. До входа на экран показывать нечего, и пускать дальше
    /// тоже: пустое поле совпало бы с пустой моделью, и «Установить» ожила бы
    /// на экране, которого никто не видел.
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

    /// <summary>Модель целевого диска. Её же человек вводит руками.</summary>
    public string DiskModel
    {
        get => _diskModel;
        private set => Set(ref _diskModel, value);
    }

    /// <summary>Объём, шина и опознавательный признак диска.</summary>
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
    /// Что случится с тем, что на диске есть сейчас. Столбик будущих размеров
    /// об этом не говорит, а на пустом диске не будет и предупреждений.
    /// </summary>
    public string PlanEffect
    {
        get => _planEffect;
        private set => Set(ref _planEffect, value);
    }

    /// <summary>Предупреждения правил выбора. Здесь они не сочиняются заново.</summary>
    public IReadOnlyList<PlanWarning> Warnings
    {
        get => _warnings;
        private set => Set(ref _warnings, value);
    }

    /// <summary>Требуется ли ввести модель диска руками.</summary>
    public bool NeedsTypedConfirmation
    {
        get => _needsTypedConfirmation;
        private set => Set(ref _needsTypedConfirmation, value);
    }

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

    /// <summary>Модель диска, набранная человеком.</summary>
    public string TypedModel
    {
        get => _typedModel;
        set
        {
            // Привязка из разметки не обязана считаться с тем, что тип
            // не допускает пустоты: пустое значение уронило бы сравнение.
            if (Set(ref _typedModel, value ?? string.Empty))
            {
                Raise(nameof(CanGoNext));
                CanGoNextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Сравнение без учёта регистра и пробелов по краям: человек переписывает
    /// строку с экрана, и придираться к пробелу — издевательство. Всё остальное
    /// должно совпасть в точности.
    /// </summary>
    public bool CanGoNext => _described && !HasTrouble &&
        (!_needsTypedConfirmation ||
         string.Equals(_typedModel.Trim(), _diskModel, StringComparison.OrdinalIgnoreCase));

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        // Введённое раньше относилось к прежнему выбору. Человек мог сходить
        // назад и поменять диск — тогда подтверждение уже ничего не значит,
        // и дать его надо заново.
        _typedModel = string.Empty;
        Raise(nameof(TypedModel));

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
            : "Остальные разделы этого диска не изменяются.";
        Warnings = SelectionRules.Warnings(target, _choice.Disks);
        NeedsTypedConfirmation = _choice.RequiresTypedConfirmation;

        // Устройство может не ответить даже на запрос своего имени. Пустое поле
        // совпало бы с пустой моделью, и подтверждение выродилось бы в нажатие
        // кнопки, то есть в ничто.
        Trouble = NeedsTypedConfirmation && DiskModel.Length == 0
            ? "У этого диска не читается модель, и подтвердить выбор нечем. Вернитесь назад и выберите другой диск."
            : string.Empty;
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
        Warnings = Array.Empty<PlanWarning>();
        NeedsTypedConfirmation = false;
    }
}
