import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  MasterDataAuditEntry,
  MasterDataRecord,
  MasterDataResourceKey,
  MasterDataWritePayload,
  RESOURCE_DEFINITIONS,
  TaxCalculationRequest,
  TaxCalculationResponse,
  resourceDefinition,
  ExchangeRateReferenceResponse,
} from './master-data.models';

@Injectable({ providedIn: 'root' })
export class MasterDataService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);

  list(resource: MasterDataResourceKey): Observable<MasterDataRecord[]> {
    return this.api.get<MasterDataRecord[]>(resourceDefinition(resource).endpoint);
  }

  get(resource: MasterDataResourceKey, id: string): Observable<MasterDataRecord> {
    return this.api.get<MasterDataRecord>(`${resourceDefinition(resource).endpoint}/${id}`);
  }

  audit(resource: MasterDataResourceKey, id: string): Observable<MasterDataAuditEntry[]> {
    return this.api.get<MasterDataAuditEntry[]>(`${resourceDefinition(resource).endpoint}/${id}/audit`);
  }

  calculateTax(id: string, request: TaxCalculationRequest): Observable<TaxCalculationResponse> {
    return this.api.post<TaxCalculationResponse>(`${resourceDefinition('taxes').endpoint}/${id}/calculate`, request);
  }

  referenceExchangeRate(id: string, effectiveOn: string): Observable<ExchangeRateReferenceResponse> {
    return this.api.get<ExchangeRateReferenceResponse>(`${resourceDefinition('exchange-rates').endpoint}/${id}/reference?effectiveOn=${encodeURIComponent(effectiveOn)}`);
  }

  async create(resource: MasterDataResourceKey, payload: MasterDataWritePayload): Promise<MasterDataRecord> {
    return this.mutate(resourceDefinition(resource).endpoint, payload);
  }

  async edit(
    resource: MasterDataResourceKey,
    id: string,
    payload: MasterDataWritePayload,
    version: string,
  ): Promise<MasterDataRecord> {
    return this.mutate(`${resourceDefinition(resource).endpoint}/${id}/edit`, payload, version);
  }

  async lifecycle(
    resource: MasterDataResourceKey,
    id: string,
    action: 'deactivate' | 'reactivate',
    version: string,
    reason?: string,
  ): Promise<MasterDataRecord> {
    return this.mutate(
      `${resourceDefinition(resource).endpoint}/${id}/${action}`,
      action === 'deactivate' ? { reason: reason?.trim() || null } : {},
      version,
    );
  }

  private async mutate<T extends MasterDataRecord>(path: string, payload: unknown, version?: string): Promise<T> {
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
    return globalThis.crypto?.randomUUID?.() ?? `master-data-${Date.now().toString(36)}`;
  }
}

export function masterDataResourcePaths(): string[] {
  return RESOURCE_DEFINITIONS.map((definition) => definition.endpoint);
}
