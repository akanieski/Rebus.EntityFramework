using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Reflection;
using Rebus.Exceptions;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Sagas;

public class EntityFrameworkSagaStorage(SagasDbContextFactory dbContextFactory) : ISagaStorage
{
    #region Private / Static Members ...

    private bool _isCreated = false;
    static readonly string IdPropertyName = Reflect.Path<ISagaData>(d => d.Id);
    
    /// <summary>
    /// Get the Saga's CLR Type name. If it has a type version, remove it. Special thanks to @xhafan for spotting this.
    /// https://github.com/rebus-org/Rebus.PostgreSql/pull/50/commits/9ae65ae5e34f21d11d8b7418f1f4f0e054883756
    /// </summary>
    /// <param name="sagaDataType"></param>
    /// <returns></returns>
    /// <exception cref="RebusApplicationException"></exception>
    string GetSagaTypeName(Type sagaDataType)
    {
        return sagaDataType.FullName != null 
            ? Regex.Replace(sagaDataType.FullName, @"Version=\d+\.\d+\.\d+\.\d+, ", "")
            : throw new RebusApplicationException($"Saga data type {sagaDataType} does not have a fully qualified name. See https://learn.microsoft.com/en-us/dotnet/api/System.Type.FullName for more information");
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
        await using var db = dbContextFactory.Create();

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
        await using var db = dbContextFactory.Create();

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
        await using var db = dbContextFactory.Create();

        var oldRevision = sagaData.Revision;
        sagaData.Revision += 1;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var existingRevision = await db.Sagas.Where(s => s.Id == sagaData.Id && s.Revision == oldRevision).SingleOrDefaultAsync();
            
            await db.SagaIndexes
                .Where(si => si.SagaId == sagaData.Id).ExecuteDeleteAsync();
            await db.Sagas.Where(s => s.Id == sagaData.Id && s.Revision == oldRevision).ExecuteDeleteAsync();
            
            var newVersionOfSaga = new Saga()
            {
                Id = sagaData.Id,
                Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
                Revision = existingRevision == null ? sagaData.Revision + 1 : existingRevision.Revision + 1
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
        await using var db = dbContextFactory.Create();

        await db.Sagas.Where(s => s.Id == sagaData.Id && s.Revision == sagaData.Revision)
            .ExecuteDeleteAsync();
        
        await db.SagaIndexes
            .Where(s => s.SagaId == sagaData.Id)
            .ExecuteDeleteAsync();
        
        sagaData.Revision++;
    }
}
