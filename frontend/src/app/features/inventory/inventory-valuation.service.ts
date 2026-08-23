import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  InventoryFinanceValuationHandoff,
  InventoryValuationEvent,
  InventoryValuationPolicy,
  InventoryValuationProcessRequest,
  InventoryValuationReconciliation,
  InventoryValuationState,
} from './inventory-valuation.model';

export interface InventoryValuationFilters {
  companyId: string;
  branchId?: string | null;
  warehouseId?: string | null;
  productId?: string | null;
  unitOfMeasureId?: string | null;
  trackingIdentity?: string | null;
  fromSequence?: number | null;
  toSequence?: number | null;
}

@Injectable({ providedIn: 'root' })
export class InventoryValuationService {
  private readonly api = inject(ApiClientService);
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  policies(companyId: string): Observable<InventoryValuationPolicy[]> {
    return this.api.get<InventoryValuationPolicy[]>(`/inventory/valuation/policies?companyId=${encodeURIComponent(companyId)}`);
  }

  states(filters: InventoryValuationFilters): Observable<InventoryValuationState[]> {
    return this.api.get<InventoryValuationState[]>(`/inventory/valuation/states${this.query(filters)}`);
  }

  summary(filters: InventoryValuationFilters): Observable<InventoryValuationReconciliation[]> {
    return this.api.get<InventoryValuationReconciliation[]>(`/inventory/valuation/summary${this.query(filters)}`);
  }

  reconciliation(filters: InventoryValuationFilters): Observable<InventoryValuationReconciliation[]> {
    return this.api.get<InventoryValuationReconciliation[]>(`/inventory/valuation/reconciliation${this.query(filters)}`);
  }

  history(filters: InventoryValuationFilters): Observable<InventoryValuationEvent[]> {
    return this.api.get<InventoryValuationEvent[]>(`/inventory/valuation/history${this.query(filters)}`);
  }

  pending(filters: InventoryValuationFilters): Observable<InventoryValuationEvent[]> {
    return this.api.get<InventoryValuationEvent[]>(`/inventory/valuation/pending${this.query(filters)}`);
  }

  financeHandoffs(filters: InventoryValuationFilters): Observable<InventoryFinanceValuationHandoff[]> {
    return this.api.get<InventoryFinanceValuationHandoff[]>(`/inventory/valuation/finance-handoffs${this.query(filters)}`);
  }

  export(filters: InventoryValuationFilters): Observable<Blob> {
    return this.http.get(`/api/v1/inventory/valuation/export${this.query(filters)}`, { withCredentials: true, responseType: 'blob' });
  }

  async process(payload: InventoryValuationProcessRequest): Promise<unknown> {
    if (!await this.auth.bootstrapAntiforgery()) {
      throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    }
    const headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    return firstValueFrom(this.api.post<unknown>('/inventory/valuation/process', payload, { headers }));
  }

  private query(values: InventoryValuationFilters): string {
    const params = Object.entries(values)
      .filter(([, value]) => value !== undefined && value !== null && value !== '')
      .map(([key, value]) => `${key}=${encodeURIComponent(String(value))}`);
    return params.length ? `?${params.join('&')}` : '';
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.() ?? `inventory-valuation-${Date.now().toString(36)}`;
  }
}
