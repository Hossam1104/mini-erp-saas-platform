import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  SupplierQuotationActionRequest,
  SupplierQuotationAuditResponse,
  SupplierQuotationComparisonResponse,
  SupplierQuotationHistoryResponse,
  SupplierQuotationListItemResponse,
  SupplierQuotationResponse,
  SupplierQuotationWriteRequest,
  SupplierSourceDecisionHistoryResponse,
  SupplierSourceDecisionResponse,
} from './supplier-quotation.model';

@Injectable({ providedIn: 'root' })
export class SupplierQuotationService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(purchaseRequestId: string): Observable<SupplierQuotationListItemResponse[]> {
    return this.api.get<SupplierQuotationListItemResponse[]>(`${this.basePath}/purchase-requests/${purchaseRequestId}/quotations`);
  }

  get(id: string): Observable<SupplierQuotationResponse> {
    return this.api.get<SupplierQuotationResponse>(`${this.basePath}/quotations/${id}`);
  }

  history(id: string): Observable<SupplierQuotationHistoryResponse[]> {
    return this.api.get<SupplierQuotationHistoryResponse[]>(`${this.basePath}/quotations/${id}/history`);
  }

  audit(id: string): Observable<SupplierQuotationAuditResponse[]> {
    return this.api.get<SupplierQuotationAuditResponse[]>(`${this.basePath}/quotations/${id}/audit`);
  }

  comparison(purchaseRequestId: string): Observable<SupplierQuotationComparisonResponse> {
    return this.api.get<SupplierQuotationComparisonResponse>(`${this.basePath}/purchase-requests/${purchaseRequestId}/quotation-comparison`);
  }

  sourceDecision(purchaseRequestId: string): Observable<SupplierSourceDecisionResponse> {
    return this.api.get<SupplierSourceDecisionResponse>(`${this.basePath}/purchase-requests/${purchaseRequestId}/source-decision`);
  }

  sourceDecisionHistory(purchaseRequestId: string): Observable<SupplierSourceDecisionHistoryResponse[]> {
    return this.api.get<SupplierSourceDecisionHistoryResponse[]>(`${this.basePath}/purchase-requests/${purchaseRequestId}/source-decision/history`);
  }

  create(purchaseRequestId: string, payload: SupplierQuotationWriteRequest): Promise<SupplierQuotationResponse> {
    return this.mutate<SupplierQuotationResponse>(`${this.basePath}/purchase-requests/${purchaseRequestId}/quotations`, payload);
  }

  edit(id: string, payload: SupplierQuotationWriteRequest, version: string): Promise<SupplierQuotationResponse> {
    return this.mutate<SupplierQuotationResponse>(`${this.basePath}/quotations/${id}/edit`, payload, version);
  }

  submit(id: string, version: string): Promise<SupplierQuotationResponse> {
    return this.mutate<SupplierQuotationResponse>(`${this.basePath}/quotations/${id}/submit`, {} , version);
  }

  withdraw(id: string, version: string, reason?: string): Promise<SupplierQuotationResponse> {
    const payload: SupplierQuotationActionRequest = { reason: reason?.trim() || null };
    return this.mutate<SupplierQuotationResponse>(`${this.basePath}/quotations/${id}/withdraw`, payload, version);
  }

  disqualify(id: string, version: string, reason: string): Promise<SupplierQuotationResponse> {
    const payload: SupplierQuotationActionRequest = { reason: reason.trim() };
    return this.mutate<SupplierQuotationResponse>(`${this.basePath}/quotations/${id}/disqualify`, payload, version);
  }

  recordSourceDecision(
    purchaseRequestId: string,
    selectedQuotationId: string,
    rationale: string,
    expectedVersion: string,
  ): Promise<SupplierSourceDecisionResponse> {
    return this.mutate<SupplierSourceDecisionResponse>(
      `${this.basePath}/purchase-requests/${purchaseRequestId}/source-decision`,
      { selectedQuotationId, rationale },
      expectedVersion,
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
    return globalThis.crypto?.randomUUID?.() ?? `supplier-quotation-${Date.now().toString(36)}`;
  }
}
