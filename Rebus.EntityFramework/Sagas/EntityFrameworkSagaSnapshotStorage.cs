using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebus.Auditing.Sagas;
using Rebus.EntityFramework.Sagas;
using Rebus.Exceptions;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Sagas;

public class EntityFrameworkSagaSnapshotStorage(SagasDbContextFactory dbContextFactory) : ISagaSnapshotStorage
{
    public async Task Save(ISagaData sagaData, Dictionary<string, string> sagaAuditMetadata)
    {
        await using var db = dbContextFactory.Create();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var existingSnapshot =
                await db.SagaSnapshots.SingleOrDefaultAsync(s =>
                    s.Id == sagaData.Id && s.Revision == sagaData.Revision);
            if (sagaData.Revision == 0)
            {
                // In this case I found that a snapshot is taken of the saga data before the saga itself is stored in the
                // database. Then, once the initial saga is stored it attempts to store the snapshot again, but the snapshot
                // already exists with the same revision. So here we are making sure that we don't attempt to store a
                // snapshot with the same revision, but only for the initial snapshot, otherwise we throw an exception.
                if (existingSnapshot != null)
                {
                    db.SagaSnapshots.Remove(existingSnapshot);
                }
            }
            else if (sagaData.Revision != 0 && existingSnapshot != null &&
                     existingSnapshot.Revision == sagaData.Revision)
            {
                throw new RebusApplicationException(
                    $"Attempted to save a saga [{sagaData.Id}] snapshot with the same revision [{sagaData.Revision}] as the existing snapshot!");
            }

            await db.SagaSnapshots.AddAsync(new SagaSnapshot()
            {
                Id = sagaData.Id,
                Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
                Metadata = JsonSerializer.Serialize(sagaAuditMetadata),
                Revision = sagaData.Revision
            });
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            Console.WriteLine(e.ToString());
            throw;
        }
    }
}