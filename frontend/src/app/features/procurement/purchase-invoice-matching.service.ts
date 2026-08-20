import { Injectable, inject } from '@angular/core';
import { HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  PurchaseInvoiceMatchAuditResponse,
  PurchaseInvoiceMatchEvaluateRequest,
  PurchaseInvoiceMatchHistoryResponse,
  PurchaseInvoiceMatchListItemResponse,
  PurchaseInvoiceMatchResponse,
  PurchaseInvoiceMatchResolveRequest,
  PurchaseInvoiceMatchResult,
} from './purchase-invoice-matching.model';

@Injectable({ providedIn: 'root' })
export class PurchaseInvoiceMatchingService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement';

  list(handoffId?: string, result?: PurchaseInvoiceMatchResult): Promise<PurchaseInvoiceMatchListItemResponse[]> {
    const params = new URLSearchParams();
    if (handoffId) params.set('handoffId', handoffId);
    if (result) params.set('result', result);
    const suffix = params.toString() ? `?${params.toString()}` : '';
    return firstValueFrom(this.api.get<PurchaseInvoiceMatchListItemResponse[]>(`${this.basePath}/purchase-invoice-matches${suffix}`));
  }

  get(id: string): Promise<PurchaseInvoiceMatchResponse> {
    return firstValueFrom(this.api.get<PurchaseInvoiceMatchResponse>(`${this.basePath}/purchase-invoice-matches/${id}`));
  }

  history(id: string): Promise<PurchaseInvoiceMatchHistoryResponse[]> {
    return firstValueFrom(this.api.get<PurchaseInvoiceMatchHistoryResponse[]>(`${this.basePath}/purchase-invoice-matches/${id}/history`));
  }

  audit(id: string): Promise<PurchaseInvoiceMatchAuditResponse[]> {
    return firstValueFrom(this.api.get<PurchaseInvoiceMatchAuditResponse[]>(`${this.basePath}/purchase-invoice-matches/${id}/audit`));
  }

  async evaluate(handoffId: string, handoffVersion: string, payload: PurchaseInvoiceMatchEvaluateRequest = {}): Promise<PurchaseInvoiceMatchResponse> {
    return this.mutate<PurchaseInvoiceMatchResponse>(`${this.basePath}/purchase-invoice-handoffs/${handoffId}/evaluate-match`, payload, handoffVersion);
  }

  async resolve(id: string, matchVersion: string, payload: PurchaseInvoiceMatchResolveRequest): Promise<PurchaseInvoiceMatchResponse> {
    return this.mutate<PurchaseInvoiceMatchResponse>(`${this.basePath}/purchase-invoice-matches/${id}/resolve-exception`, payload, matchVersion);
  }

  private async mutate<T>(path: string, payload: unknown, version: string): Promise<T> {
    const antiforgeryReady = await this.auth.bootstrapAntiforgery();
    if (!antiforgeryReady) throw { status: 403, error: { code: 'antiforgery_failed' } };
    let headers = this.auth.requestHeaders().set('If-Match', `"${version}"`);
    headers = headers.set('Idempotency-Key', crypto.randomUUID());
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }
}
