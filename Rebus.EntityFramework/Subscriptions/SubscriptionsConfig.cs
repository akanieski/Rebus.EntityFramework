using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Sagas;
using Rebus.Auditing.Sagas;
using Rebus.Config;
using Rebus.EntityFramework.Subscriptions;
using Rebus.Logging;
using Rebus.Sagas;
using Rebus.Subscriptions;
using Rebus.Threading;
using Rebus.Time;

namespace Rebus.EntityFramework.Subscriptions;

public static partial class SubscriptionsConfig
{
    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISubscriptionStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, 
        SubscriptionsStorageNamingConfiguration? storageNamingConfig = null, 
        EntityFrameworkSubscriptionOptions? options = null)
    {
        configurer.Register(c =>
            new EntityFrameworkSubscriptionStorage(
                new SubscriptionsDbContextFactory(c, optionsBuilderSetup, storageNamingConfig), 
                options ?? new EntityFrameworkSubscriptionOptions()));
    }
}

    