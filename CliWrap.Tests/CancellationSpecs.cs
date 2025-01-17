﻿using System;
using System.Collections.Generic;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using CliWrap.Buffered;
using CliWrap.EventStream;
using CliWrap.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace CliWrap.Tests;

public class CancellationSpecs
{
    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_an_executing_command_immediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var stdOutLines = new List<string>();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            ) | stdOutLines.Add;

        // Act
        var task = cmd.ExecuteAsync(cts.Token);

        // Assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        ex.CancellationToken.Should().Be(cts.Token);

        ProcessEx.IsRunning(task.ProcessId).Should().BeFalse();

        stdOutLines.Should().NotContainEquivalentOf("Done.");
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_an_executing_command_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var stdOutLines = new List<string>();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            ) | stdOutLines.Add;

        // Act
        var task = cmd.ExecuteAsync(cts.Token);

        // Assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        ex.CancellationToken.Should().Be(cts.Token);

        ProcessEx.IsRunning(task.ProcessId).Should().BeFalse();

        stdOutLines.Should().NotContainEquivalentOf("Done.");
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_an_executing_command_gracefully_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // We need to send the cancellation request right after the process has registered
        // a handler for the interrupt signal, otherwise the default handler will trigger
        // and just kill the process.
        void HandleStdOut(string line)
        {
            if (line.Contains("Sleeping for", StringComparison.OrdinalIgnoreCase))
                cts.CancelAfter(TimeSpan.FromSeconds(0.5));
        }

        var stdOutLines = new List<string>();

        var pipeTarget = PipeTarget.Merge(
            PipeTarget.ToDelegate(HandleStdOut),
            PipeTarget.ToDelegate(stdOutLines.Add)
        );

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            ) | pipeTarget;

        // Act
        var task = cmd.ExecuteAsync(CancellationToken.None, cts.Token);

        // Assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        ex.CancellationToken.Should().Be(cts.Token);

        ProcessEx.IsRunning(task.ProcessId).Should().BeFalse();

        stdOutLines.Should().ContainEquivalentOf("Canceled.");
        stdOutLines.Should().NotContainEquivalentOf("Done.");
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_with_buffering_immediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.ExecuteBufferedAsync(cts.Token)
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_with_buffering_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.ExecuteBufferedAsync(cts.Token)
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_with_buffering_gracefully_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.ExecuteBufferedAsync(
                Console.OutputEncoding,
                Console.OutputEncoding,
                CancellationToken.None,
                cts.Token
            )
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_pull_based_event_stream_immediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in cmd.ListenAsync(cts.Token))
            {
            }
        });

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_pull_based_event_stream_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in cmd.ListenAsync(cts.Token))
            {
            }
        });

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_pull_based_event_stream_gracefully_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in cmd.ListenAsync(
                               Console.OutputEncoding,
                               Console.OutputEncoding,
                               CancellationToken.None,
                               cts.Token))
            {
            }
        });

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_push_based_event_stream_immediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.Observe(cts.Token).ToTask(CancellationToken.None)
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_push_based_event_stream_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.Observe(cts.Token).ToTask(CancellationToken.None)
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task I_can_cancel_a_command_executing_as_a_push_based_event_stream_gracefully_after_a_delay()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(0.5));

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(a => a
                .Add(Dummy.Program.FilePath)
                .Add("sleep")
                .Add("--duration").Add("00:00:20")
            );

        // Act & assert
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cmd.Observe(
                    Console.OutputEncoding,
                    Console.OutputEncoding,
                    CancellationToken.None,
                    cts.Token
                )
                .ToTask(CancellationToken.None)
        );

        ex.CancellationToken.Should().Be(cts.Token);
    }
}