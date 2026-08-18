using System;
using System.Collections.Generic;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

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

    public CoreLocalization.Language SystemLanguage => CoreLocalization.Language.Russian;
}

[Collection(LocalizationCollection.Name)]
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
        Assert.Contains("Sata HDD", page.DiskSummary, StringComparison.Ordinal);
        Assert.Contains("EFI", page.PlanSummary, StringComparison.Ordinal);
        Assert.False(page.HasTrouble);
    }

    /// <summary>
    /// Подтверждение вводом модели убрано по приёмке 17.08.2026: шаг Б ничего
    /// на диск не пишет, настоящий барьер стирания — на шаге В. Значит, войдя
    /// на экран с выбранным диском, дальше можно идти сразу.
    /// </summary>
    [Fact]
    public void После_входа_дорога_открыта_без_всякого_ввода()
    {
        var page = Screen(WholeDisk());

        Assert.True(page.CanGoNext);
    }

    /// <summary>
    /// Серийный номер (длинная строка отпечатка) убран из сводки по той же
    /// приёмке: человек сверяет диск по модели и объёму.
    /// </summary>
    [Fact]
    public void Серийного_номера_в_сводке_нет()
    {
        var page = Screen(WholeDisk());

        Assert.DoesNotContain("серийн", page.DiskSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Z9A1B2C3", page.DiskSummary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Разметка диска перечисляет будущие разделы, но не говорит главного —
    /// что всё нынешнее содержимое исчезнет. Это сказано словом «безвозвратно».
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

    /// <summary>
    /// «Остальные разделы не изменяются» раньше говорилось дважды: из PlanEffect
    /// и из PlanSummary. При установке в раздел эту мысль несёт PlanSummary,
    /// поэтому PlanEffect в этом случае молчит.
    /// </summary>
    [Fact]
    public void При_установке_в_раздел_нет_двойного_остальные_разделы()
    {
        var disk = TestDisks.Disk(size: 500 * TestDisks.Gib);
        var page = Screen(new FakeChoice
        {
            Target = SelectionTarget.ForFreeSpace(disk, disk.FreeSpaces[0]),
            Disks = new[] { disk },
        });

        Assert.Equal(string.Empty, page.PlanEffect);
        Assert.Contains("не изменяются", page.PlanSummary, StringComparison.Ordinal);
    }

    /// <summary>
    /// У диска может не читаться даже модель: устройство не отвечает на запрос
    /// свойств. Раньше это был тупик — нечего было вписать. Теперь ввода нет,
    /// и такой диск не мешает идти дальше: его опознаёт объём и шина.
    /// </summary>
    [Fact]
    public void Диск_без_читаемой_модели_всё_равно_пускает_дальше()
    {
        var page = Screen(WholeDisk(model: string.Empty));

        Assert.True(page.CanGoNext);
        Assert.False(page.HasTrouble);
    }

    [Fact]
    public void Смена_диска_меняет_сводку()
    {
        var choice = WholeDisk();
        var page = Screen(choice);
        Assert.Equal(Model, page.DiskModel);

        var other = TestDisks.Disk(serial: "OTHER", size: 240 * TestDisks.Gib, model: "Samsung SSD 860");
        choice.Target = SelectionTarget.ForWholeDisk(other);
        choice.Disks = new[] { other };
        page.OnEnter();

        Assert.Equal("Samsung SSD 860", page.DiskModel);
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
    /// ему нечего.
    /// </summary>
    [Fact]
    public void Пока_на_экран_не_вошли_дальше_нельзя()
    {
        var page = new ConfirmViewModel(WholeDisk());

        Assert.False(page.CanGoNext);
    }

    /// <summary>
    /// Заголовок и слово на кнопке читаются ключами и обязаны говорить
    /// на выбранном языке, а не застывать на языке по умолчанию.
    /// </summary>
    [Fact]
    public void Заголовок_и_кнопка_меняются_с_языком()
    {
        var loc = CoreLocalization.Localization.Current;
        try
        {
            var page = Screen(WholeDisk());

            loc.Language = CoreLocalization.Language.Russian;
            Assert.Equal("Проверьте и подтвердите", page.Title);
            Assert.Equal("Установить", page.NextTitle);

            loc.Language = CoreLocalization.Language.English;
            Assert.Equal("Review and confirm", page.Title);
            Assert.Equal("Install", page.NextTitle);
        }
        finally
        {
            loc.Language = CoreLocalization.Language.Russian;
        }
    }

    /// <summary>
    /// PlanEffect и Trouble хранят состояние (стирание диска, потерянный
    /// выбор), а не готовую строку: сводка собирается один раз при входе,
    /// но язык можно сменить и после, и текст обязан заговорить по-новому.
    /// </summary>
    [Fact]
    public void Предупреждение_о_стирании_говорит_на_выбранном_языке()
    {
        var loc = CoreLocalization.Localization.Current;
        try
        {
            var page = Screen(WholeDisk());

            loc.Language = CoreLocalization.Language.Russian;
            Assert.Contains("безвозвратно", page.PlanEffect, StringComparison.Ordinal);

            loc.Language = CoreLocalization.Language.English;
            Assert.Contains("permanently", page.PlanEffect, StringComparison.Ordinal);
        }
        finally
        {
            loc.Language = CoreLocalization.Language.Russian;
        }
    }

    [Fact]
    public void Потерянный_выбор_говорит_на_выбранном_языке()
    {
        var loc = CoreLocalization.Localization.Current;
        try
        {
            var page = Screen(new FakeChoice());

            loc.Language = CoreLocalization.Language.Russian;
            Assert.Equal("Выбор потерялся: вернитесь назад и укажите, что ставим и куда.", page.Trouble);

            loc.Language = CoreLocalization.Language.English;
            Assert.Equal("The selection was lost: go back and choose what to install and where.", page.Trouble);
        }
        finally
        {
            loc.Language = CoreLocalization.Language.Russian;
        }
    }
}
