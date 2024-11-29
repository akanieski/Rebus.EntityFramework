using Microsoft.EntityFrameworkCore;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.ExclusiveAccess;

public class ExclusiveAccessDbContextFactory
{
    private bool _isCreated = false;
    private readonly IResolutionContext _resolutionContext;
    private readonly Action<DbContextOptionsBuilder> _optionsBuilderSetup;
    private readonly ExclusiveAccessStorageNamingConfiguration? _storageNamingConfig;

    public ExclusiveAccessDbContextFactory(IResolutionContext resolutionContext, 
        Action<DbContextOptionsBuilder> optionsBuilderSetup, 
        ExclusiveAccessStorageNamingConfiguration? storageNamingConfig = null)
    {
        _resolutionContext = resolutionContext;
        _optionsBuilderSetup = optionsBuilderSetup;
        _storageNamingConfig = storageNamingConfig;
    }

    public ExclusiveAccessDbContext Create()
    {
        var loggerFactory = _resolutionContext.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<ExclusiveAccessDbContext>();
        var db = new ExclusiveAccessDbContext(logger, _optionsBuilderSetup, _storageNamingConfig ?? new ());
        
        if (!_isCreated) 
        {
            db.Initialize().Wait();
            _isCreated = true;
        }
            
        return db;
    }
}