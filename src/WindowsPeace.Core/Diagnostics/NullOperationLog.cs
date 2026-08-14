namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Журнал, которому некуда писать. Существует, чтобы отсутствие места для журнала
/// не роняло программу и не требовало проверки на null в каждом вызове.
/// О том, что журнала нет, человеку сообщается на экране — молча это не проходит.
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
