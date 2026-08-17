import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  PurchaseOrderActionRequest,
  PurchaseOrderAuditResponse,
  PurchaseOrderConfirmationRequest,
  PurchaseOrderConfirmationResponse,
  PurchaseOrderCreateRequest,
  PurchaseOrderEditRequest,
  PurchaseOrderHistoryResponse,
  PurchaseOrderListItemResponse,
  PurchaseOrderResponse,
  PurchaseOrderSourceOptionResponse,
  PurchaseOrderStatus,
} from './purchase-order.model';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(status?: PurchaseOrderStatus): Observable<PurchaseOrderListItemResponse[]> {
    const suffix = status ? `?status=${encodeURIComponent(status)}` : '';
    return this.api.get<PurchaseOrderListItemResponse[]>(`${this.basePath}/purchase-orders${suffix}`);
  }

  sourceOptions(): Observable<PurchaseOrderSourceOptionResponse[]> {
    return this.api.get<PurchaseOrderSourceOptionResponse[]>(`${this.basePath}/purchase-order-sources`);
  }

  get(id: string): Observable<PurchaseOrderResponse> {
    return this.api.get<PurchaseOrderResponse>(`${this.basePath}/purchase-orders/${id}`);
  }

  confirmations(id: string): Observable<PurchaseOrderConfirmationResponse[]> {
    return this.api.get<PurchaseOrderConfirmationResponse[]>(`${this.basePath}/purchase-orders/${id}/confirmations`);
  }

  history(id: string): Observable<PurchaseOrderHistoryResponse[]> {
    return this.api.get<PurchaseOrderHistoryResponse[]>(`${this.basePath}/purchase-orders/${id}/history`);
  }

  audit(id: string): Observable<PurchaseOrderAuditResponse[]> {
    return this.api.get<PurchaseOrderAuditResponse[]>(`${this.basePath}/purchase-orders/${id}/audit`);
  }

  create(payload: PurchaseOrderCreateRequest): Promise<PurchaseOrderResponse> {
    return this.mutate<PurchaseOrderResponse>(`${this.basePath}/purchase-orders`, payload);
  }

  edit(id: string, payload: PurchaseOrderEditRequest, version: string): Promise<PurchaseOrderResponse> {
    return this.mutate<PurchaseOrderResponse>(`${this.basePath}/purchase-orders/${id}/edit`, payload, version);
  }

  submit(id: string, version: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'submit', version);
  }

  approve(id: string, version: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'approve', version);
  }

  reject(id: string, version: string, reason: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'reject', version, { reason });
  }

  returnForChange(id: string, version: string, reason: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'return-for-change', version, { reason });
  }

  issue(id: string, version: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'issue', version);
  }

  cancel(id: string, version: string, reason?: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'cancel', version, { reason: reason?.trim() || null });
  }

  captureConfirmation(id: string, payload: PurchaseOrderConfirmationRequest, version: string): Promise<PurchaseOrderResponse> {
    return this.mutate<PurchaseOrderResponse>(`${this.basePath}/purchase-orders/${id}/confirmations`, payload, version);
  }

  approveSupplierChange(id: string, version: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'supplier-change/approve', version);
  }

  rejectSupplierChange(id: string, version: string, reason: string): Promise<PurchaseOrderResponse> {
    return this.action(id, 'supplier-change/reject', version, { reason });
  }

  private action(id: string, path: string, version: string, payload: PurchaseOrderActionRequest = {}): Promise<PurchaseOrderResponse> {
    return this.mutate<PurchaseOrderResponse>(`${this.basePath}/purchase-orders/${id}/${path}`, payload, version);
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
    return globalThis.crypto?.randomUUID?.() ?? `purchase-order-${Date.now().toString(36)}`;
  }
}
