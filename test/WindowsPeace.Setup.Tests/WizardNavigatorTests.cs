using System;
using System.Collections.Generic;
using WindowsPeace.Setup.Shell;
using Xunit;

namespace WindowsPeace.Setup.Tests;

internal sealed class FakePage : IWizardPage
{
    private bool _canGoNext = true;

    public FakePage(string title) => Title = title;

    public string Title { get; }

    public bool CanGoNext
    {
        get => _canGoNext;
        set
        {
            _canGoNext = value;
            CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanGoNextChanged;

    public int EnterCount { get; private set; }

    public void OnEnter() => EnterCount++;
}

public class WizardNavigatorTests
{
    private static WizardNavigator Navigator(params IWizardPage[] pages) => new(new List<IWizardPage>(pages));

    [Fact]
    public void Первая_страница_становится_текущей_сразу()
    {
        var first = new FakePage("Диски");
        var navigator = Navigator(first, new FakePage("Дальше"));

        Assert.Same(first, navigator.Current);
        Assert.Equal(1, first.EnterCount);
    }

    [Fact]
    public void Назад_с_первой_страницы_невозможно()
    {
        var navigator = Navigator(new FakePage("Диски"), new FakePage("Дальше"));

        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void Переход_вперёд_меняет_текущую_страницу_и_сообщает_об_этом()
    {
        var second = new FakePage("Дальше");
        var navigator = Navigator(new FakePage("Диски"), second);
        var notified = 0;
        navigator.CurrentChanged += (_, _) => notified++;

        navigator.GoNext();

        Assert.Same(second, navigator.Current);
        Assert.Equal(1, notified);
        Assert.Equal(1, second.EnterCount);
    }

    [Fact]
    public void Назад_возвращает_на_предыдущую_страницу()
    {
        var first = new FakePage("Диски");
        var navigator = Navigator(first, new FakePage("Дальше"));

        navigator.GoNext();
        navigator.GoBack();

        Assert.Same(first, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void Вперёд_с_последней_страницы_ничего_не_ломает()
    {
        var navigator = Navigator(new FakePage("Диски"));

        navigator.GoNext();

        Assert.False(navigator.CanGoNext);
    }

    [Fact]
    public void Готовность_страницы_управляет_возможностью_идти_дальше()
    {
        var first = new FakePage("Диски") { CanGoNext = false };
        var navigator = Navigator(first, new FakePage("Дальше"));

        Assert.False(navigator.CanGoNext);

        first.CanGoNext = true;

        Assert.True(navigator.CanGoNext);
    }

    [Fact]
    public void Изменение_готовности_страницы_поднимает_событие_навигатора()
    {
        var first = new FakePage("Диски") { CanGoNext = false };
        var navigator = Navigator(first, new FakePage("Дальше"));
        var notified = 0;
        navigator.CanGoNextChanged += (_, _) => notified++;

        first.CanGoNext = true;

        Assert.Equal(1, notified);
    }

    [Fact]
    public void Пустой_список_страниц_недопустим()
    {
        Assert.Throws<ArgumentException>(() => new WizardNavigator(new List<IWizardPage>()));
    }

    [Fact]
    public void О_входе_на_первую_страницу_сообщается_сразу()
    {
        var entered = new List<string>();
        var first = new FakePage("Что ставим");
        _ = new WizardNavigator(new List<IWizardPage> { first, new FakePage("Куда") },
            page => entered.Add(page.Title));

        Assert.Equal(new[] { "Что ставим" }, entered);
    }

    [Fact]
    public void О_входе_на_каждую_страницу_сообщается_при_переходах()
    {
        var entered = new List<string>();
        var navigator = new WizardNavigator(
            new List<IWizardPage> { new FakePage("Что ставим"), new FakePage("Куда"), new FakePage("Проверьте") },
            page => entered.Add(page.Title));

        navigator.GoNext();
        navigator.GoNext();
        navigator.GoBack();

        Assert.Equal(new[] { "Что ставим", "Куда", "Проверьте", "Куда" }, entered);
    }

    [Fact]
    public void Холостой_переход_о_входе_не_сообщает()
    {
        var entered = new List<string>();
        var navigator = new WizardNavigator(
            new List<IWizardPage> { new FakePage("Одна") },
            page => entered.Add(page.Title));

        navigator.GoBack();
        navigator.GoNext();

        // Только вход на первую страницу при создании: уходить некуда.
        Assert.Equal(new[] { "Одна" }, entered);
    }
}
