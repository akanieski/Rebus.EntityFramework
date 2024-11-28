using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Reflection;
using Rebus.Exceptions;
using Rebus.Sagas;

namespace Rebus.EntityFramework;

public class EntityFrameworkSagaStorage(RebusDbContext db) : ISagaStorage
{
    #region Private / Static Members ...

    private bool _isCreated = false;
    static readonly string IdPropertyName = Reflect.Path<ISagaData>(d => d.Id);
    string GetSagaTypeName(Type sagaDataType)
    {
        return sagaDataType.FullName ?? throw new RebusApplicationException($"Saga data type {sagaDataType} does not have a fully qualified name. See https://learn.microsoft.com/en-us/dotnet/api/System.Type.FullName for more information");
    }

    static List<KeyValuePair<string, string>> GetPropertiesToIndex(ISagaData sagaData, IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        return correlationProperties
            .Select(p => p.PropertyName)
            .Select(path =>
            {
                var value = Reflect.Value(sagaData, path);

                return new KeyValuePair<string, string>(path, value?.ToString() ?? string.Empty);
            })
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Where(kvp => kvp.Value != null)
            .ToList();
    }

    static Guid? ToGuid(object propertyValue)
    {
        if (ReferenceEquals(propertyValue, null)) return null;

        if (propertyValue is string stringPropertyValue)
        {
            if (string.IsNullOrWhiteSpace(stringPropertyValue)) return null;

            return Guid.TryParse(stringPropertyValue, out var result)
                ? result
                : throw new FormatException($"Could not parse the string '{stringPropertyValue}' into a Guid");
        }

        return (Guid)Convert.ChangeType(propertyValue, typeof(Guid));
    }
    #endregion
    
    public async Task<ISagaData> Find(Type sagaDataType, string propertyName, object propertyValue)
    {
        if (!_isCreated)
        {
            await db.Database.EnsureCreatedAsync();
            _isCreated = true;
        }
        
        var sagaTypeName = GetSagaTypeName(sagaDataType);
        var propertyValueString = propertyValue.ToString();

        Saga? existingSaga = null;
        if (propertyName.Equals(IdPropertyName, StringComparison.OrdinalIgnoreCase))
        {
            var lookupKey = ToGuid(propertyValue);
            existingSaga = await db.Sagas.SingleOrDefaultAsync(s => s.Id == lookupKey);
        }
        else
        {
            var existingSagaIndex = await db.SagaIndexes
                .Where(i => i.SagaType == sagaTypeName)
                .Where(i => i.Key == propertyName)
                .Where(i => i.Value == propertyValueString)
                .SingleOrDefaultAsync();
            
            if (existingSagaIndex == null)
                return null!;
            
            existingSaga = await db.Sagas.FindAsync(existingSagaIndex.SagaId);
        }

        if (existingSaga == null)
        {
            return null!;
        }

        var sagaDataString = System.Text.Encoding.UTF8.GetString(existingSaga.Data);
        var deserializedSagaData = JsonSerializer.Deserialize(sagaDataString, sagaDataType);
        
        if (!sagaDataType.IsInstanceOfType(deserializedSagaData))
        {
            return null!;
        }
        
        return (ISagaData)deserializedSagaData;
    }

    public async Task Insert(ISagaData sagaData, IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        db.ChangeTracker.Clear();
        if (sagaData == null)
        {
            throw new InvalidOperationException($"Saga data is null!");
        }
        if (sagaData.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"Saga data {sagaData.GetType()} has an uninitialized Id property!");
        }
        if (sagaData.Revision != 0)
        {
            throw new InvalidOperationException($"Attempted to insert saga data with ID {sagaData.Id} and revision {sagaData.Revision}, but revision must be 0 on first insert!");
        }
        
        await db.Sagas.AddAsync(new Saga()
        {
            Id = sagaData.Id,
            Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
            Revision = sagaData.Revision
        });

        await db.SagaIndexes.AddRangeAsync(GetPropertiesToIndex(sagaData, correlationProperties).Select(cp => new SagaIndex()
        {
            Key = cp.Key,
            SagaId = sagaData.Id,
            SagaType = GetSagaTypeName(sagaData.GetType()),
            Value = cp.Value
        }));
        
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task Update(ISagaData sagaData, IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        db.ChangeTracker.Clear();
        var oldRevision = sagaData.Revision;
        sagaData.Revision += 1;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var existingIndexes = await db.SagaIndexes.Where(si => si.SagaId == sagaData.Id).ToListAsync();
            if (existingIndexes.Any())
                db.SagaIndexes.RemoveRange(existingIndexes);
            
            await db.SaveChangesAsync();
            
            var existingSaga = await db.Sagas.SingleOrDefaultAsync(s => s.Id == sagaData.Id && s.Revision == oldRevision);
            if (existingSaga != null)
            {
                // Here we remove the existing saga and its indexes, and then add the new version
                // We do this because in EF you cannot increment the revision as it has to be part of a clustered key
                db.Remove(existingSaga);
                await db.SaveChangesAsync();
            }
            
            var newVersionOfSaga = new Saga()
            {
                Id = sagaData.Id,
                Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
                Revision = sagaData.Revision
            };
            await db.Sagas.AddAsync(newVersionOfSaga);
            
            await db.SagaIndexes.AddRangeAsync(GetPropertiesToIndex(sagaData, correlationProperties).Select(cp => new SagaIndex()
            {
                Key = cp.Key,
                SagaId = sagaData.Id,
                SagaType = GetSagaTypeName(sagaData.GetType()),
                Value = cp.Value
            }));
            
            await db.SaveChangesAsync();

            await transaction.CommitAsync();
            
            db.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new RebusApplicationException(ex, $"An error occurred while updating the saga [{sagaData.Id}] data with revision [{sagaData.Revision}]");
        }
    }

    public async Task Delete(ISagaData sagaData)
    {
        var existingSaga = await db.Sagas
            .SingleOrDefaultAsync(s => s.Id == sagaData.Id && s.Revision == sagaData.Revision);
        if (existingSaga != null)
        {
            db.Sagas.Remove(existingSaga);
        }
        
        var existingSagaIndexes = await db.SagaIndexes
            .Where(s => s.SagaId == sagaData.Id).ToListAsync();
        db.SagaIndexes.RemoveRange(existingSagaIndexes);
        
        await db.SaveChangesAsync();
        sagaData.Revision++;
    }
}
