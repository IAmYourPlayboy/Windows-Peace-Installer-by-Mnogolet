using System;
using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

/// <summary>
/// Подставной выбор. Экран подтверждения не должен знать, откуда взялись
/// рецепт и диск, — иначе его нельзя проверить, не подняв два соседних экрана.
/// </summary>
internal sealed class FakeChoice : IWizardChoice
{
    public MediaRecipe? Recipe { get; set; } = new() { Id = "atlas", Name = "Atlas 25H2 RU" };

    public SelectionTarget? Target { get; set; }

    public IReadOnlyList<DiskInfo> Disks { get; set; } = Array.Empty<DiskInfo>();

    public bool RequiresTypedConfirmation { get; set; } = true;
}

public class ConfirmViewModelTests
{
    private const string Model = "ST1000DM010-2EP102";

    /// <summary>
    /// Экран, на который уже вошли. Сводка собирается при входе, а не при
    /// создании: в момент создания диск ещё не выбран.
    /// </summary>
    private static ConfirmViewModel Screen(IWizardChoice choice)
    {
        var page = new ConfirmViewModel(choice);
        page.OnEnter();
        return page;
    }

    private static FakeChoice WholeDisk(string model = Model, string? serial = "Z9A1B2C3")
    {
        var disk = TestDisks.Disk(serial: serial, size: 931 * TestDisks.Gib, model: model,
            bus: BusType.Sata, media: MediaKind.Hdd);

        return new FakeChoice { Target = SelectionTarget.ForWholeDisk(disk), Disks = new[] { disk } };
    }

    [Fact]
    public void Сводка_называет_что_ставим_куда_и_что_будет_сделано()
    {
        var page = Screen(WholeDisk());

        Assert.Equal("Atlas 25H2 RU", page.RecipeName);
        Assert.Equal(Model, page.DiskModel);
        Assert.Contains("серийный номер Z9A1B2C3", page.DiskSummary, StringComparison.Ordinal);
        Assert.Contains("EFI", page.PlanSummary, StringComparison.Ordinal);
        Assert.False(page.HasTrouble);
    }

    /// <summary>
    /// Разметка диска перечисляет будущие разделы, но не говорит главного —
    /// что всё нынешнее содержимое исчезнет. На пустом диске предупреждений
    /// не будет вовсе, и человек прочитает только столбик размеров.
    /// </summary>
    [Fact]
    public void Про_стирание_диска_сказано_прямо_а_не_столбиком_размеров()
    {
        Assert.Contains("безвозвратно", Screen(WholeDisk()).PlanEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void Установка_в_раздел_не_обещает_стереть_весь_диск()
    {
        var disk = TestDisks.Disk(size: 500 * TestDisks.Gib);
        var choice = new FakeChoice
        {
            Target = SelectionTarget.ForFreeSpace(disk, disk.FreeSpaces[0]),
            Disks = new[] { disk },
        };

        Assert.DoesNotContain("безвозвратно", Screen(choice).PlanEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void Пока_модель_не_введена_дальше_нельзя()
    {
        var page = Screen(WholeDisk());

        Assert.True(page.NeedsTypedConfirmation);
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void Неверная_модель_не_открывает_дорогу()
    {
        var page = Screen(WholeDisk());

        page.TypedModel = "ST1000";

        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void Верная_модель_открывает_дорогу_невзирая_на_регистр_и_пробелы()
    {
        var page = Screen(WholeDisk());

        page.TypedModel = "  st1000dm010-2ep102 ";

        Assert.True(page.CanGoNext);
    }

    [Fact]
    public void Ввод_сообщается_оболочке_чтобы_ожила_кнопка()
    {
        var page = Screen(WholeDisk());
        var told = 0;
        page.CanGoNextChanged += (_, _) => told++;

        page.TypedModel = Model;

        Assert.Equal(1, told);
    }

    [Fact]
    public void Когда_подтверждение_не_требуется_поля_нет_и_дорога_открыта_сразу()
    {
        var choice = WholeDisk();
        choice.RequiresTypedConfirmation = false;

        var page = Screen(choice);

        Assert.False(page.NeedsTypedConfirmation);
        Assert.True(page.CanGoNext);
    }

    /// <summary>Предупреждения не сочиняются здесь заново, а берутся из правил ядра.</summary>
    [Fact]
    public void Предупреждения_показываются_все()
    {
        var disk = TestDisks.Disk(serial: null, probeError: "Разделы прочитать не удалось");
        var page = Screen(new FakeChoice
        {
            Target = SelectionTarget.ForWholeDisk(disk),
            Disks = new[] { disk },
        });

        Assert.Contains(page.Warnings, w => w.Kind == WarningKind.PartitionsNotRead);
        Assert.Contains(page.Warnings, w => w.Kind == WarningKind.WeakIdentity);
    }

    /// <summary>
    /// Человек сходил назад и вернулся. Подтверждение, данное до этого,
    /// относилось к прежнему выбору и больше ничего не значит.
    /// </summary>
    [Fact]
    public void Возврат_на_экран_требует_подтвердить_заново()
    {
        var page = Screen(WholeDisk());
        page.TypedModel = Model;
        Assert.True(page.CanGoNext);

        page.OnEnter();

        Assert.Equal(string.Empty, page.TypedModel);
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void Смена_диска_меняет_сводку_и_ожидаемую_модель()
    {
        var choice = WholeDisk();
        var page = Screen(choice);

        var other = TestDisks.Disk(serial: "OTHER", size: 240 * TestDisks.Gib, model: "Samsung SSD 860");
        choice.Target = SelectionTarget.ForWholeDisk(other);
        choice.Disks = new[] { other };
        page.OnEnter();

        Assert.Equal("Samsung SSD 860", page.DiskModel);

        page.TypedModel = Model;
        Assert.False(page.CanGoNext);

        page.TypedModel = "Samsung SSD 860";
        Assert.True(page.CanGoNext);
    }

    /// <summary>
    /// У диска может не читаться даже модель: устройство не отвечает на запрос
    /// свойств. Пустое поле совпало бы с пустой моделью, и подтверждение
    /// превратилось бы в нажатие «Далее» — то есть в ничто.
    /// </summary>
    [Fact]
    public void Диск_без_читаемой_модели_не_подтверждается_пустой_строкой()
    {
        var page = Screen(WholeDisk(model: string.Empty));

        Assert.False(page.CanGoNext);
        Assert.True(page.HasTrouble);

        page.TypedModel = string.Empty;
        Assert.False(page.CanGoNext);
    }

    /// <summary>
    /// Сюда нельзя попасть, не выбрав рецепт и диск: мастер не пускает дальше.
    /// Но если это всё же случилось, дальше по пути форматирование — значит,
    /// молча показывать пустую сводку нельзя.
    /// </summary>
    [Fact]
    public void Потерянный_выбор_объясняется_и_дальше_не_пускает()
    {
        var page = Screen(new FakeChoice());

        Assert.False(page.CanGoNext);
        Assert.True(page.HasTrouble);
        Assert.Equal(string.Empty, page.PlanSummary);
    }

    /// <summary>
    /// «Далее» после этого экрана означает разметку диска. Кнопка обязана
    /// называть действие своим словом.
    /// </summary>
    [Fact]
    public void Кнопка_перехода_называет_действие_своим_словом()
    {
        Assert.Equal("Установить", Screen(WholeDisk()).NextTitle);
    }

    /// <summary>
    /// Экран, на который ещё не входили, ничего не показывает — и разрешать
    /// ему нечего. Иначе пустое поле совпало бы с пустой моделью и подтверждение
    /// оказалось бы данным на экране, которого никто не видел.
    /// </summary>
    [Fact]
    public void Пока_на_экран_не_вошли_дальше_нельзя()
    {
        var page = new ConfirmViewModel(WholeDisk());

        Assert.False(page.CanGoNext);
    }
}
