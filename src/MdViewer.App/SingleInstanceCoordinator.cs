using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using MdViewer.Core;

namespace MdViewer.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\md-viewer-single-instance";
    private const string PipeName = "md-viewer-single-instance";
    private const int MaxRequestBytes = 128 * 1024;

    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
    }

    public bool IsPrimary => _ownsMutex;

    public async Task<bool> ForwardAsync(LaunchRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(100, cancellationToken).ConfigureAwait(false);
                await WriteRequestAsync(client, request, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                if (attempt == 19)
                {
                    return false;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (attempt == 19)
                {
                    return false;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public async Task ListenAsync(
        Func<LaunchRequest, Task> handleRequest,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var request = await ReadRequestAsync(server, cancellationToken).ConfigureAwait(false);
                await handleRequest(request).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException exception)
            {
                Debug.WriteLine($"Unable to receive a launch request: {exception.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private static async Task WriteRequestAsync(
        Stream stream,
        LaunchRequest request,
        CancellationToken cancellationToken)
    {
        var filePath = request.FilePath ?? string.Empty;
        var error = request.Error ?? string.Empty;
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(filePath);
            writer.Write(error);
        }

        if (payload.Length > MaxRequestBytes)
        {
            throw new IOException("The launch request is too large.");
        }

        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, checked((int)payload.Length));
        await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload.GetBuffer().AsMemory(0, checked((int)payload.Length)), cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LaunchRequest> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length is <= 0 or > MaxRequestBytes)
        {
            throw new IOException("The launch request has an invalid length.");
        }

        var requestBytes = new byte[length];
        await stream.ReadExactlyAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        using var payload = new MemoryStream(requestBytes, writable: false);
        using var reader = new BinaryReader(payload, System.Text.Encoding.UTF8, leaveOpen: false);
        var filePath = reader.ReadString();
        var error = reader.ReadString();
        return new LaunchRequest(
            string.IsNullOrEmpty(filePath) ? null : filePath,
            string.IsNullOrEmpty(error) ? null : error);
    }
}
