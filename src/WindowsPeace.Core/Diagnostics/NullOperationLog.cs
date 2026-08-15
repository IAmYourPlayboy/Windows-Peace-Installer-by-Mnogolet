namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Журнал, которому некуда писать. Существует, чтобы отсутствие места для журнала
/// не роняло программу и не требовало проверки на null в каждом вызове.
///
/// Достаётся он редко: <see cref="OperationLogOpener"/> перебирает несколько мест
/// и несколько имён в каждом, и сдаётся только тогда, когда не вышло нигде.
/// Человеку об этом не сообщается — журнал нужен нам, а не ему.
/// </summary>
public sealed class NullOperationLog : IOperationLog
{
    public static readonly NullOperationLog Instance = new();

    private NullOperationLog()
    {
    }

    public void Write(OperationRecord record)
    {
    }
}
