using System.Threading.Channels;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class CommunicationDispatchPulse
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public void Pulse() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        try
        {
            if (await _channel.Reader.WaitToReadAsync(linked.Token))
                Drain();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Idle timeout; poll the outbox again.
        }
    }

    private void Drain()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }
}
