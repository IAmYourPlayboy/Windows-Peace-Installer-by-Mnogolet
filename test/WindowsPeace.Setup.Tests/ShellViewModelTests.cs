using System;
using System.Collections.Generic;
using WindowsPeace.Setup.Shell;
using Xunit;

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

public class ShellViewModelTests
{
    private static ShellViewModel Shell(params IWizardPage[] pages)
        => new(new WizardNavigator(new List<IWizardPage>(pages)));

    /// <summary>
    /// «Далее» — то, что подходит большинству экранов, и его не приходится
    /// повторять в каждом.
    /// </summary>
    [Fact]
    public void Обычная_страница_не_называет_кнопку_и_получает_Далее()
    {
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
}
