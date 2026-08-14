import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import { MasterDataAuditEntry } from './master-data.models';
import {
  PriceListPriceVersionRecord,
  PriceListPriceWriteRequest,
  PriceListRecord,
  PriceListReferenceQuery,
  PriceListReferenceRecord,
  PriceListWriteRequest,
} from './price-list.model';

@Injectable({ providedIn: 'root' })
export class PriceListService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly basePath = '/master-data/price-lists';

  list(search?: string): Observable<PriceListRecord[]> {
    const path = search && search.trim().length > 0
      ? `${this.basePath}?q=${encodeURIComponent(search.trim())}`
      : this.basePath;
    return this.api.get<PriceListRecord[]>(path);
  }

  get(id: string): Observable<PriceListRecord> {
    return this.api.get<PriceListRecord>(`${this.basePath}/${id}`);
  }

  history(id: string): Observable<PriceListPriceVersionRecord[]> {
    return this.api.get<PriceListPriceVersionRecord[]>(`${this.basePath}/${id}/history`);
  }

  create(payload: PriceListWriteRequest): Promise<PriceListRecord> {
    return this.mutate<PriceListRecord>(this.basePath, payload);
  }

  edit(id: string, payload: PriceListWriteRequest, version: string): Promise<PriceListRecord> {
    return this.mutate<PriceListRecord>(`${this.basePath}/${id}/edit`, payload, version);
  }

  appendPrice(id: string, payload: PriceListPriceWriteRequest, version: string): Promise<PriceListRecord> {
    return this.mutate<PriceListRecord>(`${this.basePath}/${id}/prices`, payload, version);
  }

  deactivate(id: string, version: string): Promise<PriceListRecord> {
    return this.mutate<PriceListRecord>(`${this.basePath}/${id}/deactivate`, {}, version);
  }

  reactivate(id: string, version: string): Promise<PriceListRecord> {
    return this.mutate<PriceListRecord>(`${this.basePath}/${id}/reactivate`, {}, version);
  }

  resolveReference(query: PriceListReferenceQuery): Observable<PriceListReferenceRecord> {
    let params = new HttpParams()
      .set('productId', query.productId)
      .set('unitOfMeasureId', query.unitOfMeasureId)
      .set('currencyId', query.currencyId)
      .set('effectiveOn', query.effectiveOn);

    if (query.priceListId) {
      params = params.set('priceListId', query.priceListId);
    }
    if (query.customerId) {
      params = params.set('customerId', query.customerId);
    }
    if (query.organizationScopeKind) {
      params = params.set('organizationScopeKind', query.organizationScopeKind);
    }
    if (query.organizationScopeId) {
      params = params.set('organizationScopeId', query.organizationScopeId);
    }

    return this.api.get<PriceListReferenceRecord>(`${this.basePath}/reference?${params.toString()}`);
  }

  audit(id: string): Observable<MasterDataAuditEntry[]> {
    return this.api.get<MasterDataAuditEntry[]>(`${this.basePath}/${id}/audit`);
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
    return globalThis.crypto?.randomUUID?.() ?? `price-list-${Date.now().toString(36)}`;
  }
}
