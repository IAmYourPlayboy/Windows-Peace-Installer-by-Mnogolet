using System;
using System.Diagnostics;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Область выполнения операции. Замеряет время и гарантирует запись в журнал
/// даже тогда, когда вызывающий забыл объявить исход — такой случай отмечается
/// отдельным значением Abandoned, чтобы его было видно.
/// </summary>
public sealed class OperationScope : IDisposable
{
    private readonly IOperationLog _log;
    private readonly string _component;
    private readonly string _operation;
    private readonly DateTimeOffset _startedAt;
    private readonly Stopwatch _stopwatch;

    private OperationOutcome _outcome = OperationOutcome.Abandoned;
    private string? _reason;
    private bool _written;

    private OperationScope(IOperationLog log, string component, string operation)
    {
        _log = log;
        _component = component;
        _operation = operation;
        _startedAt = DateTimeOffset.Now;
        _stopwatch = Stopwatch.StartNew();
    }

    public static OperationScope Start(IOperationLog log, string component, string operation)
        => new(log, component, operation);

    public void Success() => Set(OperationOutcome.Success, reason: null);

    public void Failure(string reason) => Set(OperationOutcome.Failure, reason);

    public void TimedOut() => Set(OperationOutcome.TimedOut, "Превышено предельное время");

    private void Set(OperationOutcome outcome, string? reason)
    {
        _outcome = outcome;
        _reason = reason;
    }

    public void Dispose()
    {
        if (_written)
        {
            return;
        }

        _written = true;
        _stopwatch.Stop();
        _log.Write(new OperationRecord(_startedAt, _component, _operation, _stopwatch.Elapsed, _outcome, _reason));
    }
}
