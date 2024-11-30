using System.Data;
using Microsoft.EntityFrameworkCore;
using Rebus.Serialization;
using Rebus.Timeouts;

namespace Rebus.EntityFramework.Timeouts;

public class EntityFrameworkTimeoutManager(
    TimeoutsDbContextFactory databaseContextFactory,
    EntityFrameworkTimeoutOptions options) : ITimeoutManager
{
    readonly DictionarySerializer _dictionarySerializer = new();
    
    public async Task Defer(DateTimeOffset approximateDueTime, Dictionary<string, string> headers, byte[] body)
    {
        await using var db = databaseContextFactory.Create();
        await db.Timeouts.AddAsync(new Timeout
        {
            DueTime = approximateDueTime.ToUniversalTime().DateTime,
            Headers = _dictionarySerializer.SerializeToString(headers),
            Body = body
        });
        await db.SaveChangesAsync();
    }

    public async Task<DueMessagesResult> GetDueMessages()
    {
        await using var db = databaseContextFactory.Create();
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var now = DateTime.UtcNow;
            var dueMessages = await db.Timeouts
                .Where(t => t.DueTime <= now)
                .ToListAsync();
            
            return new DueMessagesResult(dueMessages.Select(t =>
            {
                long messageId = t.Id;
                return new DueMessage(
                    _dictionarySerializer.DeserializeFromString(t.Headers),
                    t.Body, () => CompleteMessage(messageId)
                );
            }).ToList());
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task CompleteMessage(long messageId)
    {
        await using var db = databaseContextFactory.Create();
        await db.Timeouts.Where(t => t.Id == messageId).ExecuteDeleteAsync();
    }
}