using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Sagas;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.Transports;

public class TransportsDbContextFactory
{
    private bool _isCreated = false;
    private readonly IResolutionContext _resolutionContext;
    private readonly Action<DbContextOptionsBuilder> _optionsBuilderSetup;
    private readonly TransportsStorageNamingConfiguration? _storageNamingConfig;

    public TransportsDbContextFactory(IResolutionContext resolutionContext, Action<DbContextOptionsBuilder> optionsBuilderSetup, TransportsStorageNamingConfiguration? storageNamingConfig = null)
    {
        _resolutionContext = resolutionContext;
        _optionsBuilderSetup = optionsBuilderSetup;
        _storageNamingConfig = storageNamingConfig;
    }

    public TransportsDbContext Create()
    {
        var loggerFactory = _resolutionContext.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<TransportsDbContext>();
        var db = new TransportsDbContext(logger, _optionsBuilderSetup, _storageNamingConfig ?? new ());
        
        if (!_isCreated) 
        {
            db.Initialize().Wait();
            _isCreated = true;
        }
            
        return db;
    }
}