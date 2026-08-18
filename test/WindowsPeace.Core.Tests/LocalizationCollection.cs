using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Общая коллекция для тестов ядра, которые трогают <c>Localization.Current</c> —
/// либо переключают его язык напрямую, либо читают значение, зависящее
/// от текущего языка (<c>ByteSize.Format</c>, отказы и предупреждения
/// <c>SelectionRules</c>, предпросмотр разметки <c>DeploymentPlanner</c>,
/// причины «не проверено» <c>FileSystemContentInspector</c>/<c>PartitionInfo</c>,
/// сводка <c>DiskDescription</c>). Служба — общий на весь процесс одиночка
/// (раздел 9 архитектуры: язык — сквозное состояние, как культура потока),
/// поэтому классы с пометкой <c>[Collection(Name)]</c> xUnit гоняет
/// последовательно друг с другом, а остальные тесты сборки — как обычно,
/// параллельно. Тот же приём уже применён в WindowsPeace.Setup.Tests.
///
/// Новый тест, который переключает язык или проверяет зависящий от языка
/// вывод ядра, обязан получить ту же пометку — иначе гонка вернётся тихо,
/// без предупреждения от компилятора.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LocalizationCollection
{
    public const string Name = "Localization.Current";
}
