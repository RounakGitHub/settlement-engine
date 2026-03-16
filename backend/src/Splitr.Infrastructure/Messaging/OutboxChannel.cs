using System.Threading.Channels;

namespace Splitr.Infrastructure.Messaging;

/// <summary>
/// In-memory channel used to signal the outbox publisher that new events are available.
/// The signal is best-effort — if the process crashes before the publisher reads it,
/// the startup sweep will catch unpublished events.
/// </summary>
public class OutboxChannel
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public ChannelReader<bool> Reader => _channel.Reader;

    /// <summary>
    /// Signal that new outbox events are available. Fire-and-forget — never blocks.
    /// Duplicate signals are dropped since the publisher always sweeps all pending events.
    /// </summary>
    public void Signal() => _channel.Writer.TryWrite(true);
}
