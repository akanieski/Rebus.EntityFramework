using System.Data;
using Microsoft.EntityFrameworkCore;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Subscriptions;

namespace Rebus.EntityFramework.Subscriptions;

public class EntityFrameworkSubscriptionStorage(
    SubscriptionsDbContextFactory databaseContextFactory,
    EntityFrameworkSubscriptionOptions options) : ISubscriptionStorage
{
    public bool IsCentralized { get; } = options.IsCentralized;

    public async Task<IReadOnlyList<string>> GetSubscriberAddresses(string topic)
    {
        await using var databaseContext = databaseContextFactory.Create();
        return await databaseContext.Subscriptions
            .Where(s => s.Topic == topic)
            .Select(s => s.Address)
            .ToListAsync();
    }

    public async Task RegisterSubscriber(string topic, string subscriberAddress)
    {
        await using var databaseContext = databaseContextFactory.Create();
        await using var transaction =
            await databaseContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (await databaseContext.Subscriptions.AnyAsync(s => s.Topic == topic && s.Address == subscriberAddress))
            {
                return;
            }

            await databaseContext.AddAsync(new Subscription
            {
                Topic = topic,
                Address = subscriberAddress
            });
            await databaseContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UnregisterSubscriber(string topic, string subscriberAddress)
    {
        await using var databaseContext = databaseContextFactory.Create();
        await databaseContext.Subscriptions
            .Where(s => s.Topic == topic && s.Address == subscriberAddress)
            .ExecuteDeleteAsync();
    }

}