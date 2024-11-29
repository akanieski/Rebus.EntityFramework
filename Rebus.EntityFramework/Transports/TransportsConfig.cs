using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Sagas;
using Rebus.Auditing.Sagas;
using Rebus.Config;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Config;

public static partial class TransportsConfig
{
    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaSnapshotStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, SagaStorageNamingConfiguration? storageNamingConfig = null)
    {
        configurer.Register(c =>
            new EntityFrameworkSagaSnapshotStorage(new SagasDbContextFactory(c, optionsBuilderSetup, storageNamingConfig)));
    }

    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, SagaStorageNamingConfiguration? storageNamingConfig = null)
    {
        
        configurer.Register(c =>
            new EntityFrameworkSagaStorage(new SagasDbContextFactory(c, optionsBuilderSetup, storageNamingConfig)));
    }
}

    