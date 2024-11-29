using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Sagas;
using Rebus.Sagas.Exclusive;

namespace Rebus.EntityFramework.ExclusiveAccess;


public static partial class ExclusiveAccessConfig
{
    public static void EnforceExclusiveAccessWithEntityFramework(this StandardConfigurer<ISagaStorage> configurer,  
        Action<DbContextOptionsBuilder> optionsBuilderSetup, 
        ExclusiveAccessStorageNamingConfiguration? storageNamingConfig = null)
    {
        configurer.EnforceExclusiveAccess(context => new EntityFrameworkExclusiveAccess(
            new ExclusiveAccessDbContextFactory(context, optionsBuilderSetup, storageNamingConfig)));
    }
}