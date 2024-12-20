using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rebus.Logging;

namespace Rebus.EntityFramework.Sagas;

public record SagaStorageNamingConfiguration
{
    public string SagasTableName { get; init; } = "Sagas";
    public string SagaIndexesTableName { get; init; } = "SagaIndexes";
    public string SagaSnapshotsTableName { get; init; } = "SagaSnapshots";
    public string IdColumnName { get; init; } = "Id";
    public string MetadataColumnName { get; init; } = "Metadata";
    public string DataColumnName { get; init; } = "Data";
    public string KeyColumnName { get; init; } = "Key";
    public string ValueColumnName { get; init; } = "Value";
    public string SagaTypeColumnName { get; init; } = "SagaType";
    public string SagaIdColumnName { get; init; } = "SagaId";
    public string RevisionColumnName { get; init; } = "Revision";
    public string PrimaryKeyName { get; init; } = "{0}PrimaryKey";
    public string SagaIndexesToSagaIdIndexName { get; init; } = "SagaIndexesSagaIdIndex";
    public string SchemaName { get; init; } = "Rebus";
    
}
public partial class SagasDbContext(ILog logger, Action<DbContextOptionsBuilder>? optionsBuilderSetup = null, SagaStorageNamingConfiguration? namingConfiguration = null) : DbContext
{
    private SagaStorageNamingConfiguration? _namingConfiguration = namingConfiguration;

    public SagasDbContext() : this(null!, null)
    {
        _namingConfiguration ??= new SagaStorageNamingConfiguration();
    }
    public virtual DbSet<Saga> Sagas { get; set; }
    public virtual DbSet<SagaIndex> SagaIndexes { get; set; }
    public virtual DbSet<SagaSnapshot> SagaSnapshots { get; set; }
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
        _namingConfiguration ??= new SagaStorageNamingConfiguration();
        
        modelBuilder.HasDefaultSchema(_namingConfiguration.SchemaName);
        
        modelBuilder.Entity<Saga>(entity =>
        {
            entity.HasKey(e => new {e.Id, e.Revision}).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.SagasTableName));
            entity.ToTable(_namingConfiguration.SagasTableName);
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName(_namingConfiguration.IdColumnName);
            entity.Property(e => e.Data).HasColumnName(_namingConfiguration.DataColumnName);
            entity.Property(e => e.Revision).HasColumnName(_namingConfiguration.RevisionColumnName);
        });
        
        modelBuilder.Entity<SagaSnapshot>(entity =>
        {
            entity.HasKey(e => new {e.Id, e.Revision}).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.SagaSnapshotsTableName));
            entity.ToTable(_namingConfiguration.SagaSnapshotsTableName);
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName(_namingConfiguration.IdColumnName);
            entity.Property(e => e.Data).HasColumnName(_namingConfiguration.DataColumnName);
            entity.Property(e => e.Metadata).HasColumnName(_namingConfiguration.MetadataColumnName);
            entity.Property(e => e.Revision).HasColumnName(_namingConfiguration.RevisionColumnName);
        });

        modelBuilder.Entity<SagaIndex>(entity =>
        {
            entity.HasKey(e => new { e.Key, e.Value, e.SagaType }).HasName(string.Format(_namingConfiguration.PrimaryKeyName, _namingConfiguration.SagaIndexesTableName));
            entity.ToTable(_namingConfiguration.SagaIndexesTableName);
            entity.HasIndex(e => e.SagaId, _namingConfiguration.SagaIndexesToSagaIdIndexName);
            entity.Property(e => e.Key).HasColumnName(_namingConfiguration.KeyColumnName);
            entity.Property(e => e.Value).HasColumnName(_namingConfiguration.ValueColumnName);
            entity.Property(e => e.SagaType).HasColumnName(_namingConfiguration.SagaTypeColumnName);
            entity.Property(e => e.SagaId).HasColumnName(_namingConfiguration.SagaIdColumnName);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
public partial class Saga
{
    public Guid Id { get; set; }

    public int Revision { get; set; }

    public byte[] Data { get; set; } = null!;
}
public partial class SagaSnapshot
{
    public Guid Id { get; set; }

    public int Revision { get; set; }

    public byte[] Data { get; set; } = null!;
    
    public string Metadata { get; set; } = null!;
}
public partial class SagaIndex
{
    public string SagaType { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public Guid SagaId { get; set; }
}

