import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  PurchaseRequestActionRequest,
  PurchaseRequestAuditResponse,
  PurchaseRequestHistoryResponse,
  PurchaseRequestListItemResponse,
  PurchaseRequestOrganizationScopeResponse,
  PurchaseRequestResponse,
  PurchaseRequestStatus,
  PurchaseRequestWriteRequest,
} from './purchase-request.model';

@Injectable({ providedIn: 'root' })
export class PurchaseRequestService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/procurement/purchase-requests';
  private readonly organizationScopesPath = '/procurement/organization-scopes';

  list(status?: PurchaseRequestStatus | ''): Observable<PurchaseRequestListItemResponse[]> {
    const path = status
      ? `${this.basePath}?${new HttpParams().set('status', status).toString()}`
      : this.basePath;
    return this.api.get<PurchaseRequestListItemResponse[]>(path);
  }

  organizationScopes(): Observable<PurchaseRequestOrganizationScopeResponse[]> {
    return this.api.get<PurchaseRequestOrganizationScopeResponse[]>(this.organizationScopesPath);
  }

  get(id: string): Observable<PurchaseRequestResponse> {
    return this.api.get<PurchaseRequestResponse>(`${this.basePath}/${id}`);
  }

  history(id: string): Observable<PurchaseRequestHistoryResponse[]> {
    return this.api.get<PurchaseRequestHistoryResponse[]>(`${this.basePath}/${id}/history`);
  }

  audit(id: string): Observable<PurchaseRequestAuditResponse[]> {
    return this.api.get<PurchaseRequestAuditResponse[]>(`${this.basePath}/${id}/audit`);
  }

  create(payload: PurchaseRequestWriteRequest): Promise<PurchaseRequestResponse> {
    return this.mutate<PurchaseRequestResponse>(this.basePath, payload);
  }

  edit(id: string, payload: PurchaseRequestWriteRequest, version: string): Promise<PurchaseRequestResponse> {
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/edit`, payload, version);
  }

  submit(id: string, version: string): Promise<PurchaseRequestResponse> {
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/submit`, {}, version);
  }

  approve(id: string, version: string): Promise<PurchaseRequestResponse> {
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/approve`, {}, version);
  }

  reject(id: string, version: string, reason: string): Promise<PurchaseRequestResponse> {
    const payload: PurchaseRequestActionRequest = { reason };
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/reject`, payload, version);
  }

  returnForChange(id: string, version: string, reason: string): Promise<PurchaseRequestResponse> {
    const payload: PurchaseRequestActionRequest = { reason };
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/return-for-change`, payload, version);
  }

  cancel(id: string, version: string, reason?: string): Promise<PurchaseRequestResponse> {
    const payload: PurchaseRequestActionRequest = { reason: reason?.trim() || null };
    return this.mutate<PurchaseRequestResponse>(`${this.basePath}/${id}/cancel`, payload, version);
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
    return globalThis.crypto?.randomUUID?.() ?? `purchase-request-${Date.now().toString(36)}`;
  }
}
