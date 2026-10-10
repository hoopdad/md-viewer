using System.Collections.Concurrent;
using MdViewer.App;
using MdViewer.Core;

namespace MdViewer.Core.Tests;

#pragma warning disable xUnit1031 // Named mutex ownership requires acquisition and disposal on the same thread.

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void ForwardingWaitsForTheNewWindowHandler()
    {
        var instanceName = $"md-viewer-test-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceName);
        using var secondary = new SingleInstanceCoordinator(instanceName);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new TaskCompletionSource<LaunchRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = primary.ListenAsync(request =>
        {
            received.SetResult(request);
            return handled.Task;
        }, cancellation.Token);
        var request = new LaunchRequest(@"C:\documents\x.md", null);

        try
        {
            Assert.True(primary.IsPrimary);
            Assert.False(secondary.IsPrimary);
            var forwarding = secondary.ForwardAsync(request, cancellation.Token);
            Assert.Equal(request, received.Task.WaitAsync(cancellation.Token).GetAwaiter().GetResult());
            Assert.False(forwarding.IsCompleted);
            handled.SetResult();
            Assert.True(forwarding.GetAwaiter().GetResult());
        }
        finally
        {
            cancellation.Cancel();
            handled.TrySetResult();
            listener.GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void ConcurrentLaunchesAreAllHandledExactlyOnce()
    {
        var instanceName = $"md-viewer-test-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceName);
        using var secondary = new SingleInstanceCoordinator(instanceName);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new ConcurrentBag<LaunchRequest>();
        var listener = primary.ListenAsync(request =>
        {
            received.Add(request);
            return Task.CompletedTask;
        }, cancellation.Token);
        var requests = Enumerable.Range(0, 10)
            .Select(index => new LaunchRequest($@"C:\documents\{index}.md", null))
            .ToArray();

        try
        {
            var forwarded = Task.WhenAll(requests.Select(request =>
                secondary.ForwardAsync(request, cancellation.Token))).GetAwaiter().GetResult();
            Assert.All(forwarded, Assert.True);
            Assert.Equal(requests.OrderBy(request => request.FilePath),
                received.OrderBy(request => request.FilePath));
        }
        finally
        {
            cancellation.Cancel();
            listener.GetAwaiter().GetResult();
        }
    }
}

#pragma warning restore xUnit1031
