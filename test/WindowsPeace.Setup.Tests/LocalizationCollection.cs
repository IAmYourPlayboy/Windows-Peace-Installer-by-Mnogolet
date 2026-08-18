using Xunit;

namespace WindowsPeace.Setup.Tests;

/// <summary>
/// Общая коллекция для тестов, которые трогают <c>Localization.Current</c> —
/// либо переключают его язык напрямую, либо читают значение, зависящее
/// от текущего языка (значение по умолчанию <c>IWizardPage.NextTitle</c>,
/// либо <c>LanguageViewModel.Title</c>). Служба — общий на весь процесс
/// одиночка (раздел 9 архитектуры: язык — сквозное состояние, как культура
/// потока), поэтому классы с пометкой <c>[Collection(Name)]</c> xUnit гоняет
/// последовательно друг с другом, а остальные тесты сборки — как обычно,
/// параллельно.
///
/// Сейчас в коллекции: <see cref="LanguageViewModelTests"/>,
/// <see cref="ViewModelBaseLanguageTests"/>, <see cref="WizardFlowTests"/>,
/// <see cref="ShellViewModelTests"/> — они единственные читают или пишут
/// в <c>Localization.Current.Language</c>. Новый тест, который тоже это
/// делает, обязан получить ту же пометку — иначе гонка вернётся тихо,
/// без предупреждения от компилятора.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LocalizationCollection
{
    public const string Name = "Localization.Current";
}
