import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  SupplierReturnActionRequest,
  SupplierReturnAuditResponse,
  SupplierReturnCreateRequest,
  SupplierReturnEligibleSourceResponse,
  SupplierReturnFinanceReferenceRequest,
  SupplierReturnHistoryResponse,
  SupplierReturnInventoryHandoffRequest,
  SupplierReturnListItemResponse,
  SupplierReturnReportResponse,
  SupplierReturnResponse,
} from './supplier-return.model';

@Injectable({ providedIn: 'root' })
export class SupplierReturnService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(status?: string): Observable<SupplierReturnListItemResponse[]> {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    return this.api.get<SupplierReturnListItemResponse[]>(`${this.basePath}/supplier-returns${query}`);
  }

  eligibleSources(): Observable<SupplierReturnEligibleSourceResponse[]> {
    return this.api.get<SupplierReturnEligibleSourceResponse[]>(`${this.basePath}/supplier-return-sources`);
  }

  report(): Observable<SupplierReturnReportResponse> {
    return this.api.get<SupplierReturnReportResponse>(`${this.basePath}/supplier-returns/report`);
  }

  get(id: string): Observable<SupplierReturnResponse> { return this.api.get<SupplierReturnResponse>(`${this.basePath}/supplier-returns/${id}`); }
  history(id: string): Observable<SupplierReturnHistoryResponse[]> { return this.api.get<SupplierReturnHistoryResponse[]>(`${this.basePath}/supplier-returns/${id}/history`); }
  audit(id: string): Observable<SupplierReturnAuditResponse[]> { return this.api.get<SupplierReturnAuditResponse[]>(`${this.basePath}/supplier-returns/${id}/audit`); }
  create(payload: SupplierReturnCreateRequest): Promise<SupplierReturnResponse> { return this.mutate(`${this.basePath}/supplier-returns`, payload); }

  submit(id: string, version: string, reason?: string): Promise<SupplierReturnResponse> { return this.action(id, 'submit', version, { reason }); }
  approve(id: string, version: string, reason?: string): Promise<SupplierReturnResponse> { return this.action(id, 'approve', version, { reason }); }
  reject(id: string, version: string, reason?: string): Promise<SupplierReturnResponse> { return this.action(id, 'reject', version, { reason }); }
  cancel(id: string, version: string, reason?: string): Promise<SupplierReturnResponse> { return this.action(id, 'cancel', version, { reason }); }
  reverse(id: string, version: string, reason?: string): Promise<SupplierReturnResponse> { return this.action(id, 'reverse', version, { reason }); }
  inventoryHandoff(id: string, version: string, payload: SupplierReturnInventoryHandoffRequest): Promise<SupplierReturnResponse> { return this.action(id, 'inventory-handoff', version, payload); }
  financeReference(id: string, version: string, payload: SupplierReturnFinanceReferenceRequest): Promise<SupplierReturnResponse> { return this.action(id, 'finance-reference', version, payload); }
  correct(id: string, version: string, payload: SupplierReturnCreateRequest): Promise<SupplierReturnResponse> { return this.mutate(`${this.basePath}/supplier-returns/${id}/correct`, payload, version); }

  private async action(id: string, route: string, version: string, payload: SupplierReturnActionRequest | SupplierReturnInventoryHandoffRequest | SupplierReturnFinanceReferenceRequest): Promise<SupplierReturnResponse> {
    return this.mutate(`${this.basePath}/supplier-returns/${id}/${route}`, payload, version);
  }

  private async mutate<T = SupplierReturnResponse>(path: string, payload: unknown, version?: string): Promise<T> {
    if (!await this.auth.bootstrapAntiforgery()) {
      throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    }

    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) headers = headers.set('If-Match', this.quoteVersion(version));
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }

  private quoteVersion(version: string): string { return `"${version.trim().replace(/^"|"$/g, '')}"`; }
  private idempotencyKey(): string { return globalThis.crypto?.randomUUID?.() ?? `supplier-return-${Date.now().toString(36)}`; }
}
