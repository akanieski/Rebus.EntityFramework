using Microsoft.EntityFrameworkCore;
using Rebus.Auditing.Sagas;
using Rebus.Config;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Config;

public static class EntityFrameworkConfigurationExtensions
{
    /// <summary>
    /// Setup the RebusDbContext with the specified options builder setup and storage naming configuration
    /// </summary>
    /// <param name="c"></param>
    /// <param name="optionsBuilderSetup"></param>
    /// <param name="storageNamingConfig"></param>
    /// <returns></returns>
    public static RebusDbContext SetupRebusContext(IResolutionContext c,
        Action<DbContextOptionsBuilder> optionsBuilderSetup,
        RebusStorageNamingConfiguration? storageNamingConfig = null)
    {
        var loggerFactory = c.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<RebusDbContext>();
        var db = new RebusDbContext(logger, optionsBuilderSetup, storageNamingConfig ?? new ());

        db.Initialize().Wait();

        return db;
    }

    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaSnapshotStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, RebusStorageNamingConfiguration? storageNamingConfig = null)
    {
        configurer.Register(c =>
            new EntityFrameworkSagaSnapshotStorage(SetupRebusContext(c, optionsBuilderSetup, storageNamingConfig)));
    }

    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, RebusStorageNamingConfiguration? storageNamingConfig = null)
    {
        
        configurer.Register(c =>
            new EntityFrameworkSagaStorage(SetupRebusContext(c, optionsBuilderSetup, storageNamingConfig)));
    }
    
}
    