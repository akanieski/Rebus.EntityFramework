using Rebus.Logging;

namespace Rebus.EntityFramework.ExclusiveAccess;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rebus.Logging;
public record ExclusiveAccessStorageNamingConfiguration
{
    public string IdColumnName { get; init; } = "Id";
    public string PrimaryKeyName { get; init; } = "{0}PrimaryKey";
    public string SchemaName { get; init; } = "Rebus";
    
    public string TransportQueueTableName { get; init; } = "ExclusiveAccessLocks";
    
}
public partial class ExclusiveAccessDbContext(ILog logger, Action<DbContextOptionsBuilder>? optionsBuilderSetup = null, ExclusiveAccessStorageNamingConfiguration? namingConfiguration = null) : DbContext
{
    private ExclusiveAccessStorageNamingConfiguration? _namingConfiguration = namingConfiguration;

    public ExclusiveAccessDbContext() : this(null!, null)
    {
        _namingConfiguration ??= new ExclusiveAccessStorageNamingConfiguration();
    }
    
    public virtual DbSet<DistributedLock> ExclusiveAccessLocks { get; set; }
    
    public bool Initialized { get; private set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilderSetup?.Invoke(optionsBuilder);
    }

    public virtual async Task Initialize()
    {
        await Database.EnsureCreatedAsync();

        try
        {
            // Here we attempt to create tables if they don't exist. This scenario happens when the database is
            // already existing with other tables, but may be missing the Rebus Storage tables. We are ignoring 
            // exceptions here because the tables usually already exist.
            await (this.GetService<IDatabaseCreator>() as RelationalDatabaseCreator)!.CreateTablesAsync();
        }
        catch (Exception ex)
        {
            logger.Debug($"Rebus tables were not created. This could be because they already exist. Exception: {ex.Message}");
        }

        Initialized = true;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _namingConfiguration ??= new ExclusiveAccessStorageNamingConfiguration();
        
        modelBuilder.HasDefaultSchema(_namingConfiguration.SchemaName);
        
        modelBuilder.Entity<DistributedLock>()
            .HasKey(l => l.Resource);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
public class DistributedLock
{
    public string Resource { get; set; } = null!;
    public DateTimeOffset AcquiredAt { get; set; }
}