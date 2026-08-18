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
/// В коллекции — все классы, что читают или пишут
/// <c>Localization.Current.Language</c> (перечислять их здесь поимённо незачем:
/// список устаревает). Новый такой тест обязан получить ту же пометку
/// <c>[Collection(Name)]</c> — иначе гонка вернётся тихо, без предупреждения
/// от компилятора.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LocalizationCollection
{
    public const string Name = "Localization.Current";
}
