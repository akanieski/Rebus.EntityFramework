using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using Rebus.EntityFramework.Transports;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Sagas;
using Rebus.Serialization;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Transport;

namespace Rebus.EntityFramework;

public class EntityFrameworkTransportOptions
{
    public string MagicExternalTimeoutManagerAddress { get; set; }
    public TimeSpan ExpiredMessagesCleanupInterval { get; set; } = TimeSpan.FromSeconds(20);
}
public class EntityFrameworkTransport(
    TransportsDbContextFactory DatabaseContextFactory,
    ILog Log,
    IRebusTime RebusTime,
    IAsyncTaskFactory asyncTaskFactory,
    EntityFrameworkTransportOptions Options) : ITransport, IInitializable, IDisposable
{
    static readonly HeaderSerializer HeaderSerializer = new();
    readonly AsyncBottleneck _bottleneck = new(20);
    private IAsyncTask _expiredMessagesCleanupTask;

    public void CreateQueue(string address)
    {
        // Create a new queue is not really necessary with the EF transport. EF transport stores all messages in a
        // single table indexed by address. So no need to create queues in advance.
    }

    /// <summary>
    /// Gets the address a message will actually be sent to. Handles deferred messsages.
    /// </summary>
    protected string GetDestinationAddressToUse(string destinationAddress, TransportMessage message)
    {
        return string.Equals(destinationAddress, Options.MagicExternalTimeoutManagerAddress, StringComparison.OrdinalIgnoreCase)
            ? GetDeferredRecipient(message)
            : destinationAddress;
    }

    static string GetDeferredRecipient(TransportMessage message)
    {
        if (message.Headers.TryGetValue(Headers.DeferredRecipient, out var destination))
        {
            return destination;
        }

        throw new InvalidOperationException($"Attempted to defer message, but no '{Headers.DeferredRecipient}' header was on the message");
    }

    public async Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
    {
        await using var databaseContext = DatabaseContextFactory.Create();
        
        var headers = message.Headers.Clone();

        var destinationAddressToUse = GetDestinationAddressToUse(destinationAddress, message);
        var priority = GetMessagePriority(headers);
        var visible = GetInitialVisibilityDelay(headers);
        var ttl = GetTtl(headers);

        // must be last because the other functions on the headers might change them
        var serializedHeaders = HeaderSerializer.Serialize(headers);
        
        var transportMessage = new TransportQueue()
        {
            Visible = DateTime.UtcNow.Add(visible),
            Expiration = DateTime.UtcNow.Add(ttl),
            Address = destinationAddressToUse,
            Priority = priority,
            Headers = serializedHeaders,
            Body = message.Body
        };
        await databaseContext.TransportQueues.AddAsync(transportMessage);
        await databaseContext.SaveChangesAsync();
    }


    /// <summary>
    /// Receives the next message by querying the input queue table for a message with a recipient matching this transport's <see cref="Address"/>
    /// </summary>
    public async Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
    {
        using (await _bottleneck.Enter(cancellationToken).ConfigureAwait(false))
        {
            return await ReceiveInternal(context, cancellationToken).ConfigureAwait(false);
        }
    }

    protected async Task<TransportMessage> ReceiveInternal(ITransactionContext context, CancellationToken cancellationToken)
    {
        await using var databaseContext = DatabaseContextFactory.Create();
        
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var message = await databaseContext.TransportQueues
                .Where(m => m.Visible < now && m.Expiration > now)
                .OrderByDescending(m => m.Priority)
                    .ThenBy(m => m.Visible)
                        .ThenBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (message == null)
                return null!;
            
            databaseContext.TransportQueues.Remove(message);
            await databaseContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            
            return new TransportMessage(HeaderSerializer.Deserialize(message.Headers), message.Body);
        }
        catch(Exception ex)
        {
            Log.Error(ex.ToString());
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public string Address { get; } = DatabaseContextFactory.Create().TransportQueues.EntityType.GetTableName()!;
    
    public void Initialize()
    {
        using var databaseContext = DatabaseContextFactory.Create();
        databaseContext.Initialize().Wait();
        
        var cleanupInterval = Options.ExpiredMessagesCleanupInterval;
        var intervalSeconds = (int)cleanupInterval.TotalSeconds;

        _expiredMessagesCleanupTask = asyncTaskFactory.Create("ExpiredMessagesCleanup", PerformExpiredMessagesCleanupCycle, intervalSeconds: intervalSeconds);
    }

    protected async Task PerformExpiredMessagesCleanupCycle()
    {
        await using var databaseContext = DatabaseContextFactory.Create();
        
        var sw = Stopwatch.StartNew();
        var deleteCount = await databaseContext.TransportQueues
            .Where(t => t.Expiration < DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync();
        databaseContext.ChangeTracker.Clear();
        sw.Stop();
        
        if (deleteCount > 0)
        {
            Log.Info("Performed expired messages cleanup in {cleanupTimeSeconds} - {expiredMessageCount} expired messages with recipient {queueName} were deleted",
                sw.Elapsed.TotalSeconds, deleteCount, Address);
        }
    }

    public void Dispose()
    {
        // I'm not sure its safe to dispose the RebusDbContext here or if it gets re-used outside of this transport
    }
    

    /// <summary>
    /// Special message priority header that can be used with the <see cref="SqlServerTransport"/>. The value must be an <see cref="Int32"/>
    /// </summary>
    public const string MessagePriorityHeaderKey = "rbs2-msg-priority";
    static int GetMessagePriority(Dictionary<string, string> headers)
    {
        var valueOrNull = headers.GetValueOrNull(MessagePriorityHeaderKey);
        if (valueOrNull == null) return 0;

        try
        {
            return int.Parse(valueOrNull);
        }
        catch (Exception exception)
        {
            throw new FormatException($"Could not parse '{valueOrNull}' into an Int32!", exception);
        }
    }
    
    TimeSpan GetInitialVisibilityDelay(IDictionary<string, string> headers)
    {
        //if (_nativeTimeoutManagerDisabled)
        //{
        //    return TimeSpan.Zero;
        //}

        if (!headers.TryGetValue(Headers.DeferredUntil, out var deferredUntilDateTimeOffsetString))
        {
            return TimeSpan.Zero;
        }

        var deferredUntilTime = deferredUntilDateTimeOffsetString.ToDateTimeOffset();

        headers.Remove(Headers.DeferredUntil);

        var visibilityDelay = deferredUntilTime - RebusTime.Now;

        return visibilityDelay;
    }

    static TimeSpan GetTtl(IReadOnlyDictionary<string, string> headers)
    {
        const int defaultTtlSecondsAbout60Years = int.MaxValue;

        if (!headers.TryGetValue(Headers.TimeToBeReceived, out var timeToBeReceivedStr))
        {
            return TimeSpan.FromSeconds(defaultTtlSecondsAbout60Years);
        }

        var timeToBeReceived = TimeSpan.Parse(timeToBeReceivedStr);

        return timeToBeReceived;
    }
    
}