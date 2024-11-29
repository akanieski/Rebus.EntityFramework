using System.Data;
using Microsoft.EntityFrameworkCore;
using Rebus.ExclusiveLocks;

namespace Rebus.EntityFramework.ExclusiveAccess;

public class EntityFrameworkExclusiveAccess(ExclusiveAccessDbContextFactory contextFactory)
    : IExclusiveAccessLock
{
    public async Task<bool> AcquireLockAsync(string key, CancellationToken cancellationToken)
    {
        var context = contextFactory.Create();
        
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existingLock = await context.ExclusiveAccessLocks.FindAsync([key], cancellationToken: cancellationToken);
            if (existingLock == null)
            {
                var newLock = new DistributedLock
                {
                    Resource = key,
                    AcquiredAt = DateTimeOffset.UtcNow
                };
                context.ExclusiveAccessLocks.Add(newLock);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken)
    {
        var context = contextFactory.Create();
        
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existingLock = await context.ExclusiveAccessLocks.FindAsync([key], cancellationToken);
            if (existingLock == null)
            {
                var newLock = new DistributedLock
                {
                    Resource = key,
                    AcquiredAt = DateTimeOffset.UtcNow
                };
                context.ExclusiveAccessLocks.Add(newLock);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> ReleaseLockAsync(string key)
    {
        var context = contextFactory.Create();
        
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var existingLock = await context.ExclusiveAccessLocks.FindAsync(key);
            if (existingLock != null)
            {
                context.ExclusiveAccessLocks.Remove(existingLock);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return true;
    }
}