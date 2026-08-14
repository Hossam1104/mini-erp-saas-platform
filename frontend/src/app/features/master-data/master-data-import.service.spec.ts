import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, afterEach, expect, it, vi } from 'vitest';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { AuthService } from '../../core/auth/auth.service';
import {
  MasterDataImportBatchRequest,
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
  id: 'batch-guid-1',
  tenantId: 'tenant-guid-1',
  resourceKind: 'ProductCategory',
  source: {
    sourceSystemCategory: 'angular-client',
    sourceFileReference: 'categories.csv',
    batchReference: 'BATCH-001',
  },
  duplicatePolicy: 'Reject',
  mode: 'DryRun',
  status: 'Draft',
  submittedActorId: 'actor-guid-1',
  createdAt: '2026-08-14T10:00:00Z',
  startedAt: null,
  completedAt: null,
  correlationId: 'corr-guid-1',
  totalRows: 2,
  acceptedCount: 0,
  rejectedCount: 0,
  quarantinedCount: 0,
  committedCount: 0,
  skippedCount: 0,
  failedCount: 0,
  idempotencyKey: 'idemp-1',
  version: 'AQIDBA==',
  reconciliation: mockReconciliation,
};

const mockRow: MasterDataImportRowResponse = {
  id: 'row-guid-1',
  batchId: 'batch-guid-1',
  originalRowNumber: 1,
  replaySequence: 0,
  isCurrent: true,
  resourceKind: 'ProductCategory',
  sourceFields: { code: 'CAT-01', englishname: 'Beverages' },
  normalizedFields: { code: 'CAT-01', englishname: 'Beverages' },
  outcome: 'Accepted',
  diagnostics: [],
  highestSeverity: 'Info',
  mutationDisposition: 'Eligible',
  resultingResourceId: 'res-guid-1',
  resultingResourceCode: 'CAT-01',
  replayOfRowId: null,
  originalRowId: null,
  processedAt: '2026-08-14T10:01:00Z',
  version: 'BQYHCA==',
};

describe('MasterDataImportService', () => {
  let service: MasterDataImportService;
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MasterDataImportService);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    vi.spyOn(auth, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
  });

  it('lists all import batches via GET /api/v1/master-data/imports', () => {
    let result: MasterDataImportBatchResponse[] | undefined;
    service.listBatches().subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports');
    expect(req.request.method).toBe('GET');
    req.flush([mockBatch]);

    expect(result).toEqual([mockBatch]);
  });

  it('reads single batch detail via GET /api/v1/master-data/imports/:batchId', () => {
    let result: MasterDataImportBatchResponse | undefined;
    service.getBatch('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1');
    expect(req.request.method).toBe('GET');
    req.flush(mockBatch);

    expect(result).toEqual(mockBatch);
  });

  it('reads status via GET /api/v1/master-data/imports/:batchId/status', () => {
    let result: { id: string; status: string; correlationId: string; version: string } | undefined;
    service.getStatus('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/status');
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'batch-guid-1', status: 'Draft', correlationId: 'c1', version: 'v1' });

    expect(result).toEqual({ id: 'batch-guid-1', status: 'Draft', correlationId: 'c1', version: 'v1' });
  });

  it('reads row outcomes via GET /api/v1/master-data/imports/:batchId/rows', () => {
    let result: MasterDataImportRowResponse[] | undefined;
    service.getRows('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/rows');
    expect(req.request.method).toBe('GET');
    req.flush([mockRow]);

    expect(result).toEqual([mockRow]);
  });

  it('reads reconciliation via GET /api/v1/master-data/imports/:batchId/reconciliation', () => {
    let result: MasterDataImportReconciliationResponse | undefined;
    service.getReconciliation('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/reconciliation');
    expect(req.request.method).toBe('GET');
    req.flush(mockReconciliation);

    expect(result).toEqual(mockReconciliation);
  });

  it('reads audit entries via GET /api/v1/master-data/imports/:batchId/audit', () => {
    let result: unknown[] | undefined;
    service.getAudit('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/audit');
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        evidenceId: 'e1',
        occurredAt: '2026-08-14T10:00:00Z',
        operationId: 'master-data.import.create',
        correlationId: 'c1',
        tenantId: 't1',
        actorId: 'a1',
        batchId: 'batch-guid-1',
        rowId: null,
        originalRowNumber: null,
        resourceKind: 'ProductCategory',
        outcome: 'created',
        sourceReference: 'ref-1',
        detail: 'Batch created',
      },
    ]);

    expect(result).toHaveLength(1);
  });

  it('reads evidence bundle via GET /api/v1/master-data/imports/:batchId/evidence', () => {
    let result: MasterDataImportEvidenceResponse | undefined;
    service.getEvidence('batch-guid-1').subscribe((res) => (result = res));

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/evidence');
    expect(req.request.method).toBe('GET');
    req.flush({
      batch: mockBatch,
      rows: [mockRow],
      audit: [],
      reconciliation: mockReconciliation,
    });

    expect(result?.batch.id).toBe('batch-guid-1');
    expect(result?.rows).toHaveLength(1);
  });

  it('creates import batch via POST /api/v1/master-data/imports with Idempotency-Key header', async () => {
    const request: MasterDataImportBatchRequest = {
      resourceKind: 'ProductCategory',
      duplicatePolicy: 'Reject',
      mode: 'DryRun',
      rows: [{ rowNumber: 1, fields: { code: 'CAT-01', englishname: 'Beverages' } }],
    };

    const promise = service.createBatch(request);
    await Promise.resolve();

    const req = http.expectOne('/api/v1/master-data/imports');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush(mockBatch);

    const result = await promise;
    expect(result).toEqual(mockBatch);
  });

  it('simulates batch via POST /api/v1/master-data/imports/:batchId/simulate with Idempotency-Key header', async () => {
    const promise = service.simulate('batch-guid-1');
    await Promise.resolve();

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/simulate');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush({ ...mockBatch, status: 'Validated' });

    const result = await promise;
    expect(result.status).toBe('Validated');
  });

  it('executes batch via POST /api/v1/master-data/imports/:batchId/execute with If-Match and Idempotency-Key', async () => {
    const promise = service.execute('batch-guid-1', 'AQIDBA==');
    await Promise.resolve();

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/execute');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    expect(req.request.headers.get('If-Match')).toBe('"AQIDBA=="');
    req.flush({ ...mockBatch, status: 'Completed', mode: 'Commit' });

    const result = await promise;
    expect(result.status).toBe('Completed');
  });

  it('replays quarantined row via POST /api/v1/master-data/imports/:batchId/rows/:rowId/replay with If-Match', async () => {
    const promise = service.replayQuarantinedRow(
      'batch-guid-1',
      'row-guid-1',
      { correctedFields: { code: 'CAT-01-FIXED' } },
      'AQIDBA==',
    );
    await Promise.resolve();

    const req = http.expectOne('/api/v1/master-data/imports/batch-guid-1/rows/row-guid-1/replay');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"AQIDBA=="');
    expect(req.request.body).toEqual({ correctedFields: { code: 'CAT-01-FIXED' } });
    req.flush({ ...mockBatch, status: 'Validated' });

    const result = await promise;
    expect(result.status).toBe('Validated');
  });

  it('fails with antiforgery error when antiforgery bootstrap fails', async () => {
    vi.spyOn(auth, 'bootstrapAntiforgery').mockResolvedValue(false);

    await expect(service.simulate('batch-guid-1')).rejects.toMatchObject({
      status: 403,
      error: { code: 'antiforgery_failed' },
    });
  });
});
