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
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _dbContext.DisposeAsync();
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
