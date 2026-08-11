using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Перечисление дисков. За интерфейсом — чтобы правила выбора
/// проверялись на слепках, а не на настоящем железе.
/// </summary>
public interface IDiskEnumerator
{
    DiskSnapshot Enumerate(CancellationToken cancellationToken);
}
