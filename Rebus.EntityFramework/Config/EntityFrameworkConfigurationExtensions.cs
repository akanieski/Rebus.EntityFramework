using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Rebus.Auditing.Sagas;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Config;

public static class EntityFrameworkConfigurationExtensions
{
    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaSnapshotStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, RebusStorageNamingConfiguration? storageNamingConfig = null)
    {
        configurer.Register(c =>
        {
            var loggerFactory = c.Get<IRebusLoggerFactory>();
            var logger = loggerFactory.GetLogger<RebusDbContext>();
            var db = new RebusDbContext(optionsBuilderSetup, storageNamingConfig ?? new ());
            
            db.Database.EnsureCreated();

            try
            {
                // Here we attempt to create tables if they don't exist. This scenario happens when the database is
                // already existing with other tables, but may be missing the Rebus Storage tables. We are ignoring 
                // exceptions here because the tables usually already exist.
                (db.GetService<IDatabaseCreator>() as RelationalDatabaseCreator)!.CreateTables();
            }
            catch (Exception ex)
            {
                logger.Debug($"Rebus tables were not created. This could be because they already exist. Exception: {ex.Message}");
            }
            
            return new EntityFrameworkSagaSnapshotStorage(db);
        });
    }
    /// <summary>
    /// Configures Rebus to use EntityFramework to store saga data snapshots, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ISagaStorage> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, RebusStorageNamingConfiguration? storageNamingConfig = null)
    {
        
        configurer.Register(c =>
        {
            var loggerFactory = c.Get<IRebusLoggerFactory>();
            var logger = loggerFactory.GetLogger<RebusDbContext>();
            var db = new RebusDbContext(optionsBuilderSetup, storageNamingConfig ?? new());

            db.Database.EnsureCreated();

            try
            {
                // Here we attempt to create tables if they don't exist. This scenario happens when the database is
                // already existing with other tables, but may be missing the Rebus Storage tables. We are ignoring 
                // exceptions here because the tables usually already exist.
                (db.GetService<IDatabaseCreator>() as RelationalDatabaseCreator)!.CreateTables();
            }
            catch (Exception ex)
            {
                logger.Debug($"Rebus tables were not created. This could be because they already exist. Exception: {ex.Message}");
            }
            
            return new EntityFrameworkSagaStorage(db);
        });
    }
    
}
    