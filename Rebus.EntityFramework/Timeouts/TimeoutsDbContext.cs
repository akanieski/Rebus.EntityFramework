using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rebus.Logging;

namespace Rebus.EntityFramework.Timeouts;

public class EntityFrameworkTimeoutOptions
{
    public int MaxHeadersLength { get; init; } = -1;
    public int MaxBodyLength { get; init; } = -1;
}

public record TimeoutsStorageNamingConfiguration
{
    public string PrimaryKeyName { get; init; } = "{0}PrimaryKey";
    public string HeadersColumnName { get; init; } = "Headers";
    public string DueTimeColumnName { get; init; } = "DueTime";
    public string BodyColumnName { get; init; } = "Body";
    public string IdColumnName { get; init; } = "Id";
    public string SchemaName { get; init; } = "Rebus";
    
    public string TimeoutsTableName { get; init; } = "Timeouts";
    public string DueTimeIndexName { get; init; } = "Index_Timeouts_DueTime";
    
    
}
public partial class TimeoutsDbContext(ILog logger, Action<DbContextOptionsBuilder>? optionsBuilderSetup = null, TimeoutsStorageNamingConfiguration? namingConfiguration = null, EntityFrameworkTimeoutOptions? options = null) : DbContext
{
    private readonly TimeoutsStorageNamingConfiguration _namingConfiguration = namingConfiguration ?? new TimeoutsStorageNamingConfiguration();
    private readonly EntityFrameworkTimeoutOptions _options = options ?? new EntityFrameworkTimeoutOptions();

    public TimeoutsDbContext() : this(null!, null)
    {
    }
    
    public virtual DbSet<Timeout> Timeouts { get; set; }
    
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
        modelBuilder.HasDefaultSchema(_namingConfiguration.SchemaName);
        
        modelBuilder.Entity<Timeout>(entity =>
        {
            entity.ToTable(_namingConfiguration.TimeoutsTableName);
            entity.HasKey(e => e.Id).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.TimeoutsTableName));
            
            entity.Property(e => e.Id).HasColumnName(_namingConfiguration.IdColumnName).ValueGeneratedOnAdd().IsRequired();
            entity.Property(e => e.DueTime).HasColumnName(_namingConfiguration.DueTimeColumnName);
            entity.HasIndex(e => e.DueTime).HasDatabaseName(_namingConfiguration.DueTimeIndexName);
            entity.Property(e => e.Headers).HasColumnName(_namingConfiguration.HeadersColumnName).HasMaxLength(_options.MaxHeadersLength);
            entity.Property(e => e.Body).HasColumnName(_namingConfiguration.BodyColumnName).HasMaxLength(_options.MaxBodyLength);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

public partial class Timeout
{
    public long Id { get; set; }
    public DateTime DueTime { get; set; }
    public string Headers { get; set; } = null!;
    public byte[] Body { get; set; } = null!;
}