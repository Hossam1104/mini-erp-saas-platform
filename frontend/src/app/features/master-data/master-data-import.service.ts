import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  MasterDataImportAuditResponse,
  MasterDataImportBatchRequest,
  MasterDataImportBatchResponse,
  MasterDataImportEvidenceResponse,
  MasterDataImportReconciliationResponse,
  MasterDataImportReplayRequest,
  MasterDataImportRowResponse,
  MasterDataImportStatusResponse,
} from './master-data-import.models';

@Injectable({ providedIn: 'root' })
export class MasterDataImportService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/master-data/imports';

  /**
   * Lists all import batches for the current ambient tenant.
   */
  listBatches(): Observable<MasterDataImportBatchResponse[]> {
    return this.api.get<MasterDataImportBatchResponse[]>(this.basePath);
  }

  /**
   * Retrieves detail for a specific import batch.
   */
  getBatch(batchId: string): Observable<MasterDataImportBatchResponse> {
    return this.api.get<MasterDataImportBatchResponse>(`${this.basePath}/${batchId}`);
  }

  /**
   * Retrieves the current durable status for a specific import batch.
   */
  getStatus(batchId: string): Observable<MasterDataImportStatusResponse> {
    return this.api.get<MasterDataImportStatusResponse>(`${this.basePath}/${batchId}/status`);
  }

  /**
   * Retrieves all row outcomes for a specific import batch.
   */
  getRows(batchId: string): Observable<MasterDataImportRowResponse[]> {
    return this.api.get<MasterDataImportRowResponse[]>(`${this.basePath}/${batchId}/rows`);
  }

  /**
   * Retrieves the deterministic reconciliation report for a specific import batch.
   */
  getReconciliation(batchId: string): Observable<MasterDataImportReconciliationResponse> {
    return this.api.get<MasterDataImportReconciliationResponse>(
      `${this.basePath}/${batchId}/reconciliation`,
    );
  }

  /**
   * Retrieves the audit entries associated with an import batch.
   */
  getAudit(batchId: string): Observable<MasterDataImportAuditResponse[]> {
    return this.api.get<MasterDataImportAuditResponse[]>(`${this.basePath}/${batchId}/audit`);
  }

  /**
   * Retrieves the complete consolidated evidence bundle for an import batch.
   */
  getEvidence(batchId: string): Observable<MasterDataImportEvidenceResponse> {
    return this.api.get<MasterDataImportEvidenceResponse>(`${this.basePath}/${batchId}/evidence`);
  }

  /**
   * Creates a structured import batch.
   */
  createBatch(request: MasterDataImportBatchRequest): Promise<MasterDataImportBatchResponse> {
    return this.mutate<MasterDataImportBatchResponse>(this.basePath, request);
  }

  /**
   * Runs dry-run simulation for an existing Draft import batch without mutating business tables.
   */
  simulate(batchId: string): Promise<MasterDataImportBatchResponse> {
    return this.mutate<MasterDataImportBatchResponse>(`${this.basePath}/${batchId}/simulate`, {});
  }

  /**
   * Executes a validated import batch committing eligible rows to business persistence.
   * Requires the expected version token for optimistic concurrency.
   */
  execute(batchId: string, version: string): Promise<MasterDataImportBatchResponse> {
    return this.mutate<MasterDataImportBatchResponse>(
      `${this.basePath}/${batchId}/execute`,
      {},
      version,
    );
  }

  /**
   * Replays a quarantined row with corrected fields.
   * Requires the batch's expected version token for optimistic concurrency.
   */
  replayQuarantinedRow(
    batchId: string,
    rowId: string,
    request: MasterDataImportReplayRequest,
    version: string,
  ): Promise<MasterDataImportBatchResponse> {
    return this.mutate<MasterDataImportBatchResponse>(
      `${this.basePath}/${batchId}/rows/${rowId}/replay`,
      request,
      version,
    );
  }

  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    const antiforgeryReady = await this.auth.bootstrapAntiforgery();
    if (!antiforgeryReady) {
      throw new HttpErrorResponse({
        status: 403,
        statusText: 'Antiforgery validation failed',
        error: { code: 'antiforgery_failed' },
      });
    }

    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) {
      headers = headers.set('If-Match', this.quoteVersion(version));
    }

    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }

  private quoteVersion(version: string): string {
    const normalized = version.trim().replace(/^"|"$/g, '');
    return `"${normalized}"`;
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.() ?? `import-${Date.now().toString(36)}`;
  }
}
