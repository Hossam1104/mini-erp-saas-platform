import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  PurchaseInvoiceHandoffActionRequest,
  PurchaseInvoiceHandoffAuditResponse,
  PurchaseInvoiceHandoffCreateRequest,
  PurchaseInvoiceHandoffEligibleSourceResponse,
  PurchaseInvoiceHandoffHistoryResponse,
  PurchaseInvoiceHandoffListItemResponse,
  PurchaseInvoiceHandoffResponse,
  PurchaseInvoiceHandoffStatus,
} from './purchase-invoice-handoff.model';

@Injectable({ providedIn: 'root' })
export class PurchaseInvoiceHandoffService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(status?: PurchaseInvoiceHandoffStatus, purchaseOrderId?: string): Observable<PurchaseInvoiceHandoffListItemResponse[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (purchaseOrderId) params.set('purchaseOrderId', purchaseOrderId);
    const query = params.toString() ? `?${params.toString()}` : '';
    return this.api.get<PurchaseInvoiceHandoffListItemResponse[]>(`${this.basePath}/purchase-invoice-handoffs${query}`);
  }

  eligibleSources(): Observable<PurchaseInvoiceHandoffEligibleSourceResponse[]> {
    return this.api.get<PurchaseInvoiceHandoffEligibleSourceResponse[]>(`${this.basePath}/purchase-invoice-handoff-sources`);
  }

  get(id: string): Observable<PurchaseInvoiceHandoffResponse> {
    return this.api.get<PurchaseInvoiceHandoffResponse>(`${this.basePath}/purchase-invoice-handoffs/${id}`);
  }

  history(id: string): Observable<PurchaseInvoiceHandoffHistoryResponse[]> {
    return this.api.get<PurchaseInvoiceHandoffHistoryResponse[]>(`${this.basePath}/purchase-invoice-handoffs/${id}/history`);
  }

  audit(id: string): Observable<PurchaseInvoiceHandoffAuditResponse[]> {
    return this.api.get<PurchaseInvoiceHandoffAuditResponse[]>(`${this.basePath}/purchase-invoice-handoffs/${id}/audit`);
  }

  create(payload: PurchaseInvoiceHandoffCreateRequest): Promise<PurchaseInvoiceHandoffResponse> {
    return this.mutate<PurchaseInvoiceHandoffResponse>(`${this.basePath}/purchase-invoice-handoffs`, payload);
  }

  cancel(id: string, version: string, reason?: string): Promise<PurchaseInvoiceHandoffResponse> {
    const payload: PurchaseInvoiceHandoffActionRequest = { reason: reason?.trim() || null };
    return this.mutate<PurchaseInvoiceHandoffResponse>(`${this.basePath}/purchase-invoice-handoffs/${id}/cancel`, payload, version);
  }

  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    const antiforgeryReady = await this.auth.bootstrapAntiforgery();
    if (!antiforgeryReady) {
      throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
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
    return globalThis.crypto?.randomUUID?.() ?? `invoice-handoff-${Date.now().toString(36)}`;
  }
}
