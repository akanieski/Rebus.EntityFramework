using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Timeouts;

namespace Rebus.EntityFramework.Timeouts;

public static partial class TimeoutsConfig
{
    /// <summary>
    /// Configures Rebus to use EntityFramework to store timeouts, using the specified table to store the data
    /// </summary>
    public static void StoreInEntityFramework(this StandardConfigurer<ITimeoutManager> configurer,
        Action<DbContextOptionsBuilder> optionsBuilderSetup, 
        TimeoutsStorageNamingConfiguration? storageNamingConfig = null, 
        EntityFrameworkTimeoutOptions? options = null)
    {
        configurer.Register(c =>
            new EntityFrameworkTimeoutManager(
                new TimeoutsDbContextFactory(c, optionsBuilderSetup, storageNamingConfig), 
                options ?? new EntityFrameworkTimeoutOptions()));
    }
}

    