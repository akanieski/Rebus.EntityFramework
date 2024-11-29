using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Sagas;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.Subscriptions;

public class SubscriptionsDbContextFactory
{
    private bool _isCreated = false;
    private readonly IResolutionContext _resolutionContext;
    private readonly Action<DbContextOptionsBuilder> _optionsBuilderSetup;
    private readonly SubscriptionsStorageNamingConfiguration? _storageNamingConfig;

    public SubscriptionsDbContextFactory(IResolutionContext resolutionContext, Action<DbContextOptionsBuilder> optionsBuilderSetup, SubscriptionsStorageNamingConfiguration? storageNamingConfig = null)
    {
        _resolutionContext = resolutionContext;
        _optionsBuilderSetup = optionsBuilderSetup;
        _storageNamingConfig = storageNamingConfig;
    }

    public SubscriptionsDbContext Create()
    {
        var loggerFactory = _resolutionContext.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<SubscriptionsDbContext>();
        var db = new SubscriptionsDbContext(logger, _optionsBuilderSetup, _storageNamingConfig ?? new ());
        
        if (!_isCreated) 
        {
            db.Initialize().Wait();
            _isCreated = true;
        }
            
        return db;
    }
}