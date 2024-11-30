using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Sagas;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.Timeouts;

public class TimeoutsDbContextFactory
{
    private bool _isCreated = false;
    private readonly IResolutionContext _resolutionContext;
    private readonly Action<DbContextOptionsBuilder> _optionsBuilderSetup;
    private readonly TimeoutsStorageNamingConfiguration? _storageNamingConfig;

    public TimeoutsDbContextFactory(IResolutionContext resolutionContext, Action<DbContextOptionsBuilder> optionsBuilderSetup, TimeoutsStorageNamingConfiguration? storageNamingConfig = null)
    {
        _resolutionContext = resolutionContext;
        _optionsBuilderSetup = optionsBuilderSetup;
        _storageNamingConfig = storageNamingConfig;
    }

    public TimeoutsDbContext Create()
    {
        var loggerFactory = _resolutionContext.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<TimeoutsDbContext>();
        var db = new TimeoutsDbContext(logger, _optionsBuilderSetup, _storageNamingConfig ?? new ());
        
        if (!_isCreated) 
        {
            db.Initialize().Wait();
            _isCreated = true;
        }
            
        return db;
    }
}