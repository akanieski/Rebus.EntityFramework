using Microsoft.EntityFrameworkCore;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.Sagas;

public class SagasDbContextFactory
{
    private bool _isCreated = false;
    private readonly IResolutionContext _resolutionContext;
    private readonly Action<DbContextOptionsBuilder> _optionsBuilderSetup;
    private readonly SagaStorageNamingConfiguration? _storageNamingConfig;

    public SagasDbContextFactory(IResolutionContext resolutionContext, Action<DbContextOptionsBuilder> optionsBuilderSetup, SagaStorageNamingConfiguration? storageNamingConfig = null)
    {
        _resolutionContext = resolutionContext;
        _optionsBuilderSetup = optionsBuilderSetup;
        _storageNamingConfig = storageNamingConfig;
    }

    public SagasDbContext Create()
    {
        var loggerFactory = _resolutionContext.Get<IRebusLoggerFactory>();
        var logger = loggerFactory.GetLogger<SagasDbContext>();
        var db = new SagasDbContext(logger, _optionsBuilderSetup, _storageNamingConfig ?? new ());

        if (!_isCreated) 
        {
            db.Initialize().Wait();
            _isCreated = true;
        }
            
        return db;
    }
}