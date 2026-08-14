import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';
import { MasterDataImportFacade } from './master-data-import.facade';
import {
  MasterDataImportBatchResponse,
  MasterDataImportEvidenceResponse,
  MasterDataImportReconciliationResponse,
  MasterDataImportRowResponse,
} from './master-data-import.models';
import { MasterDataImportService } from './master-data-import.service';

const mockReconciliation: MasterDataImportReconciliationResponse = {
  totalRows: 2,
  accepted: 2,
  rejected: 0,
  quarantined: 0,
  committed: 0,
  skipped: 0,
  failed: 0,
  isConsistent: true,
  formula: 'TotalRows = Accepted + Rejected + Quarantined',
};

const mockBatch: MasterDataImportBatchResponse = {
  id: 'batch-1',
  tenantId: 'tenant-1',
  resourceKind: 'ProductCategory',
  source: {
    sourceSystemCategory: 'angular-client',
    sourceFileReference: 'categories.csv',
    batchReference: null,
  },
  duplicatePolicy: 'Reject',
  mode: 'DryRun',
  status: 'Draft',
  submittedActorId: 'actor-1',
  createdAt: '2026-08-14T10:00:00Z',
  startedAt: null,
  completedAt: null,
  correlationId: 'corr-1',
  totalRows: 2,
  acceptedCount: 0,
  rejectedCount: 0,
  quarantinedCount: 0,
  committedCount: 0,
  skippedCount: 0,
  failedCount: 0,
  idempotencyKey: 'idemp-1',
  version: 'ver-1',
  reconciliation: mockReconciliation,
};

const mockRows: MasterDataImportRowResponse[] = [
  {
    id: 'row-1',
    batchId: 'batch-1',
    originalRowNumber: 1,
    replaySequence: 0,
    isCurrent: true,
    resourceKind: 'ProductCategory',
    sourceFields: { code: 'CAT-1', englishname: 'Cat 1' },
    normalizedFields: { code: 'CAT-1', englishname: 'Cat 1' },
    outcome: 'Accepted',
    diagnostics: [],
    highestSeverity: 'Info',
    mutationDisposition: 'Eligible',
    resultingResourceId: 'res-1',
    resultingResourceCode: 'CAT-1',
    replayOfRowId: null,
    originalRowId: null,
    processedAt: '2026-08-14T10:00:00Z',
    version: 'v-1',
  },
];

describe('MasterDataImportFacade', () => {
  let facade: MasterDataImportFacade;
  let serviceMock: Partial<Record<keyof MasterDataImportService, any>>;

  beforeEach(() => {
    serviceMock = {
      listBatches: vi.fn().mockReturnValue(of([mockBatch])),
      getBatch: vi.fn().mockReturnValue(of(mockBatch)),
      getStatus: vi.fn().mockReturnValue(of({ id: 'batch-1', status: 'Draft', correlationId: 'c', version: 'v' })),
      getRows: vi.fn().mockReturnValue(of(mockRows)),
      getReconciliation: vi.fn().mockReturnValue(of(mockReconciliation)),
      getAudit: vi.fn().mockReturnValue(of([])),
      getEvidence: vi.fn().mockReturnValue(
        of<MasterDataImportEvidenceResponse>({
          batch: mockBatch,
          rows: mockRows,
          audit: [],
          reconciliation: mockReconciliation,
        }),
      ),
      createBatch: vi.fn().mockResolvedValue(mockBatch),
      simulate: vi.fn().mockResolvedValue({ ...mockBatch, status: 'Validated' }),
      execute: vi.fn().mockResolvedValue({ ...mockBatch, status: 'Completed', mode: 'Commit' }),
      replayQuarantinedRow: vi.fn().mockResolvedValue({ ...mockBatch, status: 'Validated' }),
    };

    TestBed.configureTestingModule({
      providers: [
        MasterDataImportFacade,
        { provide: MasterDataImportService, useValue: serviceMock },
      ],
    });

    facade = TestBed.inject(MasterDataImportFacade);
  });

  it('starts in clean initial state', () => {
    expect(facade.selectedResourceKind()).toBeNull();
    expect(facade.duplicatePolicy()).toBe('Reject');
    expect(facade.importMode()).toBe('DryRun');
    expect(facade.fileMetadata()).toBeNull();
    expect(facade.parseResult()).toBeNull();
    expect(facade.currentBatch()).toBeNull();
    expect(facade.batchRows()).toEqual([]);
    expect(facade.isReadyToSubmit()).toBe(false);
    expect(facade.busy()).toBe(false);
    expect(facade.error()).toBeNull();
  });

  it('orchestrates end-to-end import flow: file -> parse -> mapping -> create batch -> simulate -> execute', async () => {
    // 1. Select resource
    facade.selectResource('ProductCategory');
    expect(facade.selectedResourceKind()).toBe('ProductCategory');

    // 2. Load and parse CSV
    const csv = 'code,englishName\nCAT-01,Beverages\nCAT-02,Food';
    facade.loadFileContent(csv, 'categories.csv');

    expect(facade.fileMetadata()).toEqual({
      name: 'categories.csv',
      size: csv.length,
      format: 'csv',
    });
    expect(facade.parseResult()?.valid).toBe(true);
    expect(facade.parseResult()?.totalRows).toBe(2);

    // 3. Verify auto-mapping
    expect(facade.columnMappings()).toEqual([
      { sourceColumn: 'code', targetField: 'code' },
      { sourceColumn: 'englishName', targetField: 'englishName' },
    ]);
    expect(facade.mappingValidation()?.valid).toBe(true);
    expect(facade.normalizedRows()).toHaveLength(2);
    expect(facade.isReadyToSubmit()).toBe(true);

    // 4. Create batch
    const created = await facade.createBatch('IMPORT-REF-01');
    expect(created).toEqual(mockBatch);
    expect(serviceMock.createBatch).toHaveBeenCalled();
    expect(facade.currentBatch()).toEqual(mockBatch);
    expect(facade.batchRows()).toEqual(mockRows);

    // 5. Simulate batch
    const simulated = await facade.simulateBatch();
    expect(simulated?.status).toBe('Validated');
    expect(facade.currentBatch()?.status).toBe('Validated');

    // 6. Execute batch (set mode to Commit first to satisfy canExecute)
    facade.setImportMode('Commit');
    facade.currentBatch.set({ ...simulated!, mode: 'Commit' });
    expect(facade.canExecute()).toBe(true);

    const executed = await facade.executeBatch();
    expect(executed?.status).toBe('Completed');
    expect(facade.currentBatch()?.status).toBe('Completed');
  });

  it('handles malformed file parsing gracefully and sets error state', () => {
    facade.selectResource('ProductCategory');
    facade.loadFileContent('code,name\n"unclosed quote', 'broken.csv');

    expect(facade.parseResult()?.valid).toBe(false);
    expect(facade.isReadyToSubmit()).toBe(false);
    expect(facade.error()?.code).toBe('import_row_invalid');
  });

  it('blocks batch creation when required column is unmapped', async () => {
    facade.selectResource('Product');
    // Missing categoryId and baseUnitOfMeasureId
    facade.loadFileContent('sku,name\nSKU-1,Valve', 'products.csv');

    expect(facade.mappingValidation()?.valid).toBe(false);
    expect(facade.isReadyToSubmit()).toBe(false);

    const batch = await facade.createBatch();
    expect(batch).toBeNull();
    expect(facade.error()?.code).toBe('validation_failed');
    expect(serviceMock.createBatch).not.toHaveBeenCalled();
  });

  it('handles backend API errors and converts them to safe UI errors', async () => {
    serviceMock.createBatch!.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 409,
        error: { code: 'import_concurrency_conflict' },
      }),
    );

    facade.selectResource('ProductCategory');
    facade.loadFileContent('code,englishName\nCAT-1,Name', 'cats.csv');

    const result = await facade.createBatch();
    expect(result).toBeNull();
    expect(facade.error()?.code).toBe('import_concurrency_conflict');
    expect(facade.busy()).toBe(false);
  });

  it('replays a quarantined row and updates the batch state', async () => {
    facade.currentBatch.set(mockBatch);

    const replayed = await facade.replayRow('row-1', { englishName: 'Fixed Name' });
    expect(replayed?.status).toBe('Validated');
    expect(serviceMock.replayQuarantinedRow).toHaveBeenCalledWith(
      'batch-1',
      'row-1',
      { correctedFields: { englishName: 'Fixed Name' } },
      'ver-1',
    );
  });

  it('loads batch evidence bundle', async () => {
    await facade.loadBatchEvidence('batch-1');

    expect(facade.currentBatch()).toEqual(mockBatch);
    expect(facade.batchRows()).toEqual(mockRows);
    expect(facade.batchReconciliation()).toEqual(mockReconciliation);
  });

  it('refreshes batch list for workspace review', async () => {
    await facade.refreshBatchList();

    expect(facade.batchList()).toEqual([mockBatch]);
    expect(serviceMock.listBatches).toHaveBeenCalled();
  });

  it('resets all state back to initial on reset()', () => {
    facade.selectResource('ProductCategory');
    facade.loadFileContent('code,name\nC1,N1', 'c.csv');
    facade.currentBatch.set(mockBatch);

    facade.reset();

    expect(facade.selectedResourceKind()).toBeNull();
    expect(facade.fileMetadata()).toBeNull();
    expect(facade.parseResult()).toBeNull();
    expect(facade.currentBatch()).toBeNull();
    expect(facade.batchRows()).toEqual([]);
  });
});
