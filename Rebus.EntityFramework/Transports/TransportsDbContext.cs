using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rebus.Logging;

namespace Rebus.EntityFramework;

public record TransportsStorageNamingConfiguration
{
    public string IdColumnName { get; init; } = "Id";
    public string PrimaryKeyName { get; init; } = "{0}PrimaryKey";
    public string SchemaName { get; init; } = "Rebus";
    
    public string TransportQueueTableName { get; init; } = "TransportQueue";
    public string AddressColumnName { get; init; } = "Address";
    public string BodyColumnName { get; init; } = "Body";
    public string PriorityColumnName { get; init; } = "Priority";
    public string ExpirationColumnName { get; init; } = "Expiration";
    public string VisibleColumnName { get; init; } = "Visible";
    public string HeadersColumnName { get; init; } = "Headers";
    public string ExpirationIndexName { get; init; } = "Index_Expiration_TransportQueue";
    public string AddressIndexName { get; init; } = "Index_Address_TransportQueue";
    public string DequeueIndexName { get; init; } = "Index_Dequeue_TransportQueue";
    public string ReceiveIndexName { get; init; } = "Index_Receive_TransportQueue";
    
}
public partial class TransportsDbContext(ILog logger, Action<DbContextOptionsBuilder>? optionsBuilderSetup = null, TransportsStorageNamingConfiguration? namingConfiguration = null) : DbContext
{
    private TransportsStorageNamingConfiguration? _namingConfiguration = namingConfiguration;

    public TransportsDbContext() : this(null!, null)
    {
        _namingConfiguration ??= new TransportsStorageNamingConfiguration();
    }
    
    public virtual DbSet<TransportQueue> TransportQueues { get; set; }
    
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
        _namingConfiguration ??= new TransportsStorageNamingConfiguration();
        
        modelBuilder.HasDefaultSchema(_namingConfiguration.SchemaName);
        
        modelBuilder.Entity<TransportQueue>(entity =>
        {
            entity.HasKey(e => new { e.Id }).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.TransportQueueTableName));
            entity.ToTable(_namingConfiguration.TransportQueueTableName);
            entity.HasIndex(e => new { e.Priority, e.Visible, e.Id }, _namingConfiguration.DequeueIndexName).IsDescending(true, false, false);
            entity.HasIndex(e => e.Expiration, _namingConfiguration.ExpirationIndexName);
            entity.HasIndex(e => new { e.Expiration, e.Visible }, _namingConfiguration.ReceiveIndexName);
            entity.Property(e => e.Priority).HasColumnName(_namingConfiguration.PriorityColumnName);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName(_namingConfiguration.IdColumnName);
            entity.Property(e => e.Address).HasColumnName(_namingConfiguration.AddressColumnName);
            entity.HasIndex(e => e.Address, _namingConfiguration.AddressIndexName);
            entity.Property(e => e.Body).HasColumnName(_namingConfiguration.BodyColumnName);
            entity.Property(e => e.Expiration).HasColumnName(_namingConfiguration.ExpirationColumnName);
            entity.Property(e => e.Headers).HasColumnName(_namingConfiguration.HeadersColumnName);
            entity.Property(e => e.Visible).HasColumnName(_namingConfiguration.VisibleColumnName);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

public partial class TransportQueue
{
    public int Id { get; set; }
    
    /// <summary>
    /// This differs from the traditional Rebus.SqlServer implementation in that it stores all queues in a single table
    /// to make design time work easier with EFCore.
    /// </summary>
    public string Address { get; set; } = null!;

    public int Priority { get; set; }

    public DateTime Expiration { get; set; }

    public DateTime Visible { get; set; }

    public byte[] Headers { get; set; } = null!;

    public byte[] Body { get; set; } = null!;
}