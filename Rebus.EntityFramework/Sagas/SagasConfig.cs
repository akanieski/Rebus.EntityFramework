using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.EntityFramework.Transports;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Time;
using Rebus.Transport;

namespace Rebus.EntityFramework.Sagas;

public static partial class SagasConfig
{
    /// <summary>
    /// Configures Rebus to use EntityFramework as its transport
    /// </summary>
    /// <param name="configurer">Static to extend</param>
    /// <param name="storageNamingConfig"></param>
    /// <param name="transportOptions">Options controlling the transport setup</param>
    /// <param name="inputQueueName">Queue name to process messages from</param>
    /// <param name="optionsBuilderSetup"></param>
    public static void UseEntityFramework(this StandardConfigurer<ITransport> configurer,  
        Action<DbContextOptionsBuilder> optionsBuilderSetup, 
        TransportsStorageNamingConfiguration? storageNamingConfig = null, 
        EntityFrameworkTransportOptions? transportOptions = null)
    {
        configurer.Register(context => new EntityFrameworkTransport(
            new TransportsDbContextFactory(context, optionsBuilderSetup, storageNamingConfig),
            context.Get<IRebusLoggerFactory>().GetLogger<TransportsDbContext>(),
            context.Get<IRebusTime>(),
            context.Get<IAsyncTaskFactory>(),
            transportOptions ?? new EntityFrameworkTransportOptions()));
    }
}