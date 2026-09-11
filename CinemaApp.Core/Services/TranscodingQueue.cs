using System.Threading.Channels;
using CinemaApp.Core.DTOs;

namespace CinemaApp.Core.Services;

public class TranscodingQueue : ITranscodingQueue
{
    private readonly Channel<TranscodingJob> _channel;

    public TranscodingQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<TranscodingJob>(options);
    }

    public async ValueTask QueueJobAsync(TranscodingJob job, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public async ValueTask<TranscodingJob> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
