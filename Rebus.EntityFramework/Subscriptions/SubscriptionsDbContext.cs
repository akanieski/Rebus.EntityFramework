using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rebus.Logging;

namespace Rebus.EntityFramework.Subscriptions;

public class EntityFrameworkSubscriptionOptions
{
    public int TopicLength { get; set; } = 200;
    public int AddressLength { get; set; } = 200;
    public bool IsCentralized { get; set; } = true;
}

public record SubscriptionsStorageNamingConfiguration
{
    public string PrimaryKeyName { get; init; } = "{0}PrimaryKey";
    public string TopicColumnName { get; init; } = "Topic";
    public string AddressColumnName { get; init; } = "Address";
    public string SchemaName { get; init; } = "Rebus";
    
    public string SubscriptionsTableName { get; init; } = "Subscriptions";
    
}
public partial class SubscriptionsDbContext(ILog logger, Action<DbContextOptionsBuilder>? optionsBuilderSetup = null, SubscriptionsStorageNamingConfiguration? namingConfiguration = null, EntityFrameworkSubscriptionOptions? options = null) : DbContext
{
    private readonly SubscriptionsStorageNamingConfiguration _namingConfiguration = namingConfiguration ?? new SubscriptionsStorageNamingConfiguration();
    private readonly EntityFrameworkSubscriptionOptions _options = options ?? new EntityFrameworkSubscriptionOptions();

    public SubscriptionsDbContext() : this(null!, null)
    {
    }
    
    public virtual DbSet<Subscription> Subscriptions { get; set; }
    
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
        
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable(_namingConfiguration.SubscriptionsTableName);
            entity.HasKey(e => new { e.Topic, e.Address }).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.SubscriptionsTableName));
            
            entity.Property(e => e.Topic).HasColumnName(_namingConfiguration.TopicColumnName).HasMaxLength(_options.TopicLength);
            entity.Property(e => e.Address).HasColumnName(_namingConfiguration.AddressColumnName).HasMaxLength(_options.AddressLength);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

public partial class Subscription
{
    public string Topic { get; set; } = null!;
    public string Address { get; set; } = null!;
}