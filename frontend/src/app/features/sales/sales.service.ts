import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  SalesActionRequest,
  SalesAuditResponse,
  SalesCreditOverrideRequest,
  SalesCreditResponse,
  SalesHistoryResponse,
  SalesOrderResponse,
  SalesOrderStatus,
  SalesOrderSummaryResponse,
  SalesQuotationCreateRequest,
  SalesQuotationEditRequest,
  SalesQuotationResponse,
  SalesQuotationRevisionResponse,
  SalesQuotationStatus,
  SalesQuotationSummaryResponse,
} from './sales.model';

@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);

  quotations(status?: SalesQuotationStatus | ''): Observable<SalesQuotationSummaryResponse[]> {
    const path = status ? `/sales/quotations?${new HttpParams().set('status', status).toString()}` : '/sales/quotations';
    return this.api.get<SalesQuotationSummaryResponse[]>(path);
  }

  quotation(id: string): Observable<SalesQuotationResponse> { return this.api.get<SalesQuotationResponse>(`/sales/quotations/${id}`); }
  quotationRevisions(id: string): Observable<SalesQuotationRevisionResponse[]> { return this.api.get<SalesQuotationRevisionResponse[]>(`/sales/quotations/${id}/revisions`); }
  quotationHistory(id: string): Observable<SalesHistoryResponse[]> { return this.api.get<SalesHistoryResponse[]>(`/sales/quotations/${id}/history`); }
  quotationAudit(id: string): Observable<SalesAuditResponse[]> { return this.api.get<SalesAuditResponse[]>(`/sales/quotations/${id}/audit`); }
  orders(status?: SalesOrderStatus | ''): Observable<SalesOrderSummaryResponse[]> {
    const path = status ? `/sales/orders?${new HttpParams().set('status', status).toString()}` : '/sales/orders';
    return this.api.get<SalesOrderSummaryResponse[]>(path);
  }
  order(id: string): Observable<SalesOrderResponse> { return this.api.get<SalesOrderResponse>(`/sales/orders/${id}`); }
  orderHistory(id: string): Observable<SalesHistoryResponse[]> { return this.api.get<SalesHistoryResponse[]>(`/sales/orders/${id}/history`); }
  orderAudit(id: string): Observable<SalesAuditResponse[]> { return this.api.get<SalesAuditResponse[]>(`/sales/orders/${id}/audit`); }
  orderCredit(id: string): Observable<SalesCreditResponse> { return this.api.get<SalesCreditResponse>(`/sales/orders/${id}/credit`); }

  createQuotation(payload: SalesQuotationCreateRequest): Promise<SalesQuotationResponse> { return this.mutate('/sales/quotations', payload); }
  editQuotation(id: string, payload: SalesQuotationEditRequest, version: string): Promise<SalesQuotationResponse> { return this.mutate(`/sales/quotations/${id}/edit`, payload, version); }
  convertQuotation(id: string, version: string): Promise<SalesOrderResponse> { return this.mutate(`/sales/quotations/${id}/convert`, {}, version); }

  quotationAction(id: string, action: 'submit' | 'approve' | 'reject' | 'return' | 'send' | 'withdraw' | 'cancel', version: string, reason?: string): Promise<SalesQuotationResponse> {
    return this.mutate(`/sales/quotations/${id}/${action}`, { reason: reason?.trim() || null }, version);
  }

  orderAction(id: string, action: 'submit' | 'approve' | 'reject' | 'return' | 'confirm' | 'cancel', version: string, reason?: string): Promise<SalesOrderResponse> {
    return this.mutate(`/sales/orders/${id}/${action}`, { reason: reason?.trim() || null }, version);
  }

  overrideCredit(id: string, payload: SalesCreditOverrideRequest, version: string): Promise<SalesOrderResponse> { return this.mutate(`/sales/orders/${id}/credit/override`, payload, version); }

  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    const antiforgeryReady = await this.auth.bootstrapAntiforgery();
    if (!antiforgeryReady) throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) headers = headers.set('If-Match', `"${version.trim().replace(/^"|"$/g, '')}"`);
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }

  private idempotencyKey(): string { return globalThis.crypto?.randomUUID?.() ?? `sales-${Date.now().toString(36)}`; }
}
