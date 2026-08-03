using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

internal sealed class TenantPersistenceSession : ITenantPersistenceSession
{
    private readonly TenantPersistenceDbContext _dbContext;

    public TenantPersistenceSession(TenantPersistenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        Records = new TenantRecordRepository(_dbContext);
    }

    public ITenantRecordRepository Records { get; }

    internal DbContext DbContext => _dbContext;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsyncCore(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    internal Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsyncCore(acceptAllChangesOnSuccess, cancellationToken);
    }

    internal int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    internal int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return _dbContext.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (TenantPersistenceViolationException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw TenantPersistenceViolationException.Deny(
                TenantPersistenceViolationCode.PersistenceConflict,
                _dbContext.TenantContext,
                TenantPersistenceOperation.SaveChanges);
        }
        catch (DbUpdateException)
        {
            throw TenantPersistenceViolationException.Deny(
                TenantPersistenceViolationCode.PersistenceConflict,
                _dbContext.TenantContext,
                TenantPersistenceOperation.SaveChanges);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _dbContext.DisposeAsync();
    }

    private async Task<int> SaveChangesAsyncCore(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
        }
        catch (TenantPersistenceViolationException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw TenantPersistenceViolationException.Deny(
                TenantPersistenceViolationCode.PersistenceConflict,
                _dbContext.TenantContext,
                TenantPersistenceOperation.SaveChanges);
        }
        catch (DbUpdateException)
        {
            throw TenantPersistenceViolationException.Deny(
                TenantPersistenceViolationCode.PersistenceConflict,
                _dbContext.TenantContext,
                TenantPersistenceOperation.SaveChanges);
        }
    }

    private sealed class TenantRecordRepository : ITenantRecordRepository
    {
        private readonly TenantPersistenceDbContext _dbContext;

        public TenantRecordRepository(TenantPersistenceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Add(TenantOwnedRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _dbContext.TenantOwnedRecords.Add(record);
        }

        public async Task<IReadOnlyList<TenantOwnedRecord>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.TenantOwnedRecords
                .AsNoTracking()
                .OrderBy(item => item.BusinessKey)
                .ToListAsync(cancellationToken);
        }

        public Task<TenantOwnedRecord?> FindAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.TenantOwnedRecords
                .SingleOrDefaultAsync(item => item.Id == recordId, cancellationToken);
        }
    }
}
