using System;
using System.Collections.Generic;
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests;

internal sealed class RecordingLog : IOperationLog
{
    public List<OperationRecord> Records { get; } = new();

    public void Write(OperationRecord record) => Records.Add(record);
}

public class OperationScopeTests
{
    [Fact]
    public void Успешная_область_оставляет_запись_с_исходом_Success()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Перечисление дисков"))
        {
            scope.Success();
        }

        var record = Assert.Single(log.Records);
        Assert.Equal("Storage", record.Component);
        Assert.Equal("Перечисление дисков", record.Operation);
        Assert.Equal(OperationOutcome.Success, record.Outcome);
        Assert.Null(record.Reason);
    }

    [Fact]
    public void Область_без_явного_исхода_считается_прерванной()
    {
        var log = new RecordingLog();

        using (OperationScope.Start(log, "Storage", "Опрос диска"))
        {
        }

        var record = Assert.Single(log.Records);
        Assert.Equal(OperationOutcome.Abandoned, record.Outcome);
    }

    [Fact]
    public void Отказ_сохраняет_причину()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.Failure("WMI недоступно");
        }

        var record = Assert.Single(log.Records);
        Assert.Equal(OperationOutcome.Failure, record.Outcome);
        Assert.Equal("WMI недоступно", record.Reason);
    }

    [Fact]
    public void Истечение_времени_отличается_от_обычного_отказа()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.TimedOut();
        }

        Assert.Equal(OperationOutcome.TimedOut, Assert.Single(log.Records).Outcome);
    }

    [Fact]
    public void Длительность_измеряется_и_попадает_в_запись()
    {
        var log = new RecordingLog();

        using (var scope = OperationScope.Start(log, "Storage", "Опрос диска"))
        {
            scope.Success();
        }

        Assert.True(Assert.Single(log.Records).Duration >= TimeSpan.Zero);
    }

    [Fact]
    public void Предельные_времена_заданы_явно_и_не_бесконечны()
    {
        Assert.True(Timeouts.DiskEnumeration > TimeSpan.Zero);
        Assert.True(Timeouts.DiskEnumeration < TimeSpan.FromMinutes(5));
        Assert.True(Timeouts.SingleDiskProbe > TimeSpan.Zero);
        Assert.True(Timeouts.ContentInspection > TimeSpan.Zero);
    }
}
