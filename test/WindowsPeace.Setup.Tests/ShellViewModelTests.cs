using System;
using System.Collections.Generic;
using WindowsPeace.Setup.Shell;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Tests;

/// <summary>Страница, которая называет кнопку перехода своим словом.</summary>
internal sealed class NamedNextPage : IWizardPage
{
    public string Title => "Проверьте и подтвердите";

    public string NextTitle => "Установить";

    public bool CanGoNext => false;

    public event EventHandler? CanGoNextChanged
    {
        add { }
        remove { }
    }

    public void OnEnter()
    {
    }
}

/// <summary>Страница, с которой возвращаться уже некуда: работа началась.</summary>
internal sealed class NoWayBackPage : IWizardPage
{
    public string Title => "Установка";

    public bool CanGoBack => false;

    public bool CanGoNext => true;

    public event EventHandler? CanGoNextChanged
    {
        add { }
        remove { }
    }

    public void OnEnter()
    {
    }
}

[Collection(LocalizationCollection.Name)]
public class ShellViewModelTests
{
    private static ShellViewModel Shell(params IWizardPage[] pages)
        => new(new WizardNavigator(new List<IWizardPage>(pages)), () => { });

    /// <summary>
    /// «Далее» — то, что подходит большинству экранов, и его не приходится
    /// повторять в каждом. Название кнопки по умолчанию берётся из службы
    /// локализации, поэтому язык здесь закреплён явно: другие тесты в сборке
    /// переключают его же общий экземпляр.
    /// </summary>
    [Fact]
    public void Обычная_страница_не_называет_кнопку_и_получает_Далее()
    {
        CoreLocalization.Localization.Current.Language = CoreLocalization.Language.Russian;

        Assert.Equal("Далее", Shell(new FakePage("Диски"), new FakePage("Дальше")).NextTitle);
    }

    /// <summary>
    /// Экран, после которого начинается работа с диском, обязан сказать об этом
    /// на кнопке. Оболочка при этом ничего не знает про экраны поимённо.
    /// </summary>
    [Fact]
    public void Название_кнопки_берётся_у_текущей_страницы_и_меняется_при_переходе()
    {
        var shell = Shell(new FakePage("Диски"), new NamedNextPage());
        var changed = 0;
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.NextTitle))
            {
                changed++;
            }
        };

        shell.NextCommand.Execute(null);

        Assert.Equal("Установить", shell.NextTitle);
        Assert.Equal(1, changed);
    }

    /// <summary>
    /// Выход есть с любого экрана и всегда один и тот же. В WinPE окно занимает
    /// весь экран и крестика на нём нет: не предложи мастер выход сам —
    /// человеку осталось бы выключить машину из розетки.
    /// </summary>
    [Fact]
    public void Выход_из_мастера_есть_на_каждом_экране()
    {
        var closed = 0;
        var shell = new ShellViewModel(
            new WizardNavigator(new List<IWizardPage> { new FakePage("Диски"), new NoWayBackPage() }),
            () => closed++);

        Assert.True(shell.CloseCommand.CanExecute(null));
        shell.CloseCommand.Execute(null);

        shell.NextCommand.Execute(null);

        Assert.True(shell.CloseCommand.CanExecute(null));
        shell.CloseCommand.Execute(null);

        Assert.Equal(2, closed);
    }

    /// <summary>
    /// Спека: «Назад» доступна на экранах 1–3 и недоступна на 4–5. Решает
    /// сама страница: оболочка про экраны поимённо не знает.
    /// </summary>
    [Fact]
    public void Страница_может_запретить_возврат_назад()
    {
        var shell = Shell(new FakePage("Диски"), new NoWayBackPage());

        shell.NextCommand.Execute(null);

        Assert.False(shell.BackCommand.CanExecute(null));
    }
}
