using System;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>Чем закончилась операция.</summary>
public enum OperationOutcome
{
    /// <summary>Область закрыта без объявления исхода — это дефект в коде вызывающего.</summary>
    Abandoned = 0,
    Success,
    Failure,
    TimedOut,
}

/// <summary>Одна запись журнала. Плоская и машиночитаемая.</summary>
public sealed class OperationRecord
{
    public OperationRecord(
        DateTimeOffset startedAt,
        string component,
        string operation,
        TimeSpan duration,
        OperationOutcome outcome,
        string? reason)
    {
        StartedAt = startedAt;
        Component = component;
        Operation = operation;
        Duration = duration;
        Outcome = outcome;
        Reason = reason;
    }

    public DateTimeOffset StartedAt { get; }
    public string Component { get; }
    public string Operation { get; }
    public TimeSpan Duration { get; }
    public OperationOutcome Outcome { get; }
    public string? Reason { get; }
}

/// <summary>Приёмник записей журнала.</summary>
public interface IOperationLog
{
    void Write(OperationRecord record);
}
