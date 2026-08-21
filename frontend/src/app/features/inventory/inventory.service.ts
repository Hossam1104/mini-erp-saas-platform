import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import { InventoryAvailability, InventoryMovement, InventoryOpeningBalance, InventoryOpeningCreateRequest, InventoryReservation, InventoryReservationCreateRequest, InventoryWarehouseOption } from './inventory.model';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private idempotencyKey(): string { return globalThis.crypto?.randomUUID?.() ?? `inventory-${Date.now().toString(36)}`; }
  private query(values: Record<string, string | null | undefined>): string { const params = Object.entries(values).filter(([, value]) => value).map(([key, value]) => `${key}=${encodeURIComponent(value!)}`); return params.length ? `?${params.join('&')}` : ''; }

  warehouses(companyId?: string, branchId?: string): Observable<InventoryWarehouseOption[]> { return this.api.get<InventoryWarehouseOption[]>(`/inventory/warehouses${this.query({ companyId, branchId })}`); }
  ledger(filters: { warehouseId?: string; companyId?: string; branchId?: string; productId?: string } = {}): Observable<InventoryMovement[]> { return this.api.get<InventoryMovement[]>(`/inventory/ledger${this.query(filters)}`); }
  availability(filters: { warehouseId: string; companyId: string; branchId?: string | null; productId: string; unitOfMeasureId: string; trackingIdentity?: string | null }): Observable<InventoryAvailability> { return this.api.get<InventoryAvailability>(`/inventory/availability${this.query(filters)}`); }
  openings(filters: { warehouseId?: string; companyId?: string; branchId?: string } = {}): Observable<InventoryOpeningBalance[]> { return this.api.get<InventoryOpeningBalance[]>(`/inventory/opening-balances${this.query(filters)}`); }
  reservations(filters: { warehouseId?: string; companyId?: string; branchId?: string; productId?: string } = {}): Observable<InventoryReservation[]> { return this.api.get<InventoryReservation[]>(`/inventory/reservations${this.query(filters)}`); }

  async createOpening(payload: InventoryOpeningCreateRequest): Promise<InventoryOpeningBalance> { return this.mutate('/inventory/opening-balances', payload); }
  async validateOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/validate`, version, { reason }); }
  async postOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/post`, version, { reason }); }
  async correctOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/correct`, version, { reason }); }
  async createReservation(payload: InventoryReservationCreateRequest): Promise<InventoryReservation> { return this.mutate('/inventory/reservations', payload); }
  async reduceReservation(id: string, version: string, quantity: number, reason?: string): Promise<InventoryReservation> { return this.action(`/inventory/reservations/${id}/reduce`, version, { quantity, reason }); }
  async releaseReservation(id: string, version: string, reason?: string): Promise<InventoryReservation> { return this.action(`/inventory/reservations/${id}/release`, version, { reason }); }

  private async action<T>(path: string, version: string, payload: unknown): Promise<T> { return this.mutate(path, payload, version); }
  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    if (!await this.auth.bootstrapAntiforgery()) throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) headers = headers.set('If-Match', `"${version.replace(/^"|"$/g, '')}"`);
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }
}
