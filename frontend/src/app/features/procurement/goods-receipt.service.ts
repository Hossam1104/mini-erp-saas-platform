import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  GoodsReceiptActionRequest,
  GoodsReceiptAuditResponse,
  GoodsReceiptCreateRequest,
  GoodsReceiptEligibleSourceResponse,
  GoodsReceiptHistoryResponse,
  GoodsReceiptListItemResponse,
  GoodsReceiptResponse,
  GoodsReceiptStatus,
  GoodsReceiptWarehouseOptionResponse,
} from './goods-receipt.model';

@Injectable({ providedIn: 'root' })
export class GoodsReceiptService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(status?: GoodsReceiptStatus, purchaseOrderId?: string): Observable<GoodsReceiptListItemResponse[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (purchaseOrderId) params.set('purchaseOrderId', purchaseOrderId);
    const query = params.toString() ? `?${params.toString()}` : '';
    return this.api.get<GoodsReceiptListItemResponse[]>(`${this.basePath}/goods-receipts${query}`);
  }

  eligibleSources(): Observable<GoodsReceiptEligibleSourceResponse[]> {
    return this.api.get<GoodsReceiptEligibleSourceResponse[]>(`${this.basePath}/goods-receipt-sources`);
  }

  warehouses(): Observable<GoodsReceiptWarehouseOptionResponse[]> {
    return this.api.get<GoodsReceiptWarehouseOptionResponse[]>(`${this.basePath}/warehouses`);
  }

  get(id: string): Observable<GoodsReceiptResponse> {
    return this.api.get<GoodsReceiptResponse>(`${this.basePath}/goods-receipts/${id}`);
  }

  history(id: string): Observable<GoodsReceiptHistoryResponse[]> {
    return this.api.get<GoodsReceiptHistoryResponse[]>(`${this.basePath}/goods-receipts/${id}/history`);
  }

  audit(id: string): Observable<GoodsReceiptAuditResponse[]> {
    return this.api.get<GoodsReceiptAuditResponse[]>(`${this.basePath}/goods-receipts/${id}/audit`);
  }

  create(payload: GoodsReceiptCreateRequest): Promise<GoodsReceiptResponse> {
    return this.mutate<GoodsReceiptResponse>(`${this.basePath}/goods-receipts`, payload);
  }

  cancel(id: string, version: string, reason?: string): Promise<GoodsReceiptResponse> {
    const payload: GoodsReceiptActionRequest = { reason: reason?.trim() || null };
    return this.mutate<GoodsReceiptResponse>(`${this.basePath}/goods-receipts/${id}/cancel`, payload, version);
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
    return globalThis.crypto?.randomUUID?.() ?? `goods-receipt-${Date.now().toString(36)}`;
  }
}
