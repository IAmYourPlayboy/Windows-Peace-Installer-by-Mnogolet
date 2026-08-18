using System;
using WindowsPeace.Setup.Pages;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Tests;

/// <summary>
/// Два последних экрана — каркасы: работа за ними придёт на шаге В. Проверяется
/// то, что от них требуется уже сейчас: честность и запрет вернуться назад,
/// когда возвращаться уже некуда.
/// </summary>
[Collection(LocalizationCollection.Name)]
public class LastPagesTests
{
    [Fact]
    public void С_экрана_установки_назад_нельзя()
    {
        var page = new ProgressViewModel();

        Assert.False(page.CanGoBack);
    }

    [Fact]
    public void С_экрана_завершения_назад_нельзя_и_вперёд_тоже()
    {
        var page = new DoneViewModel();

        Assert.False(page.CanGoBack);
        Assert.False(page.CanGoNext);
    }

    /// <summary>
    /// Поддельная полоска прогресса не рисуется, и обещать работу, которой ещё
    /// нет, экран не должен: он говорит прямо, что ничего не записывает.
    /// </summary>
    [Fact]
    public void Экран_установки_не_притворяется_работающим()
    {
        var page = new ProgressViewModel();

        Assert.Contains("не записывает", page.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Журнал нужен нам, а не человеку. Решение автора: про журнал ему
    /// не рассказываем вовсе — ни где он лежит, ни что он есть.
    /// </summary>
    [Fact]
    public void Про_журнал_человеку_не_рассказывают()
    {
        var page = new DoneViewModel();

        Assert.DoesNotContain("журнал", page.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Заголовки и объяснения читаются ключами и говорят на выбранном языке.</summary>
    [Fact]
    public void Заголовки_и_объяснения_меняются_с_языком()
    {
        var loc = CoreLocalization.Localization.Current;
        try
        {
            var progress = new ProgressViewModel();
            var done = new DoneViewModel();

            loc.Language = CoreLocalization.Language.Russian;
            Assert.Equal("Установка", progress.Title);
            Assert.Contains("не записывает", progress.Explanation, StringComparison.Ordinal);
            Assert.Equal("Готово", done.Title);
            Assert.Contains("итог установки", done.Explanation, StringComparison.Ordinal);

            loc.Language = CoreLocalization.Language.English;
            Assert.Equal("Installation", progress.Title);
            Assert.Contains("writes nothing", progress.Explanation, StringComparison.Ordinal);
            Assert.Equal("Done", done.Title);
            Assert.Contains("installation result", done.Explanation, StringComparison.Ordinal);
        }
        finally
        {
            loc.Language = CoreLocalization.Language.Russian;
        }
    }
}
