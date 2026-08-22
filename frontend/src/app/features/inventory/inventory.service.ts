import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import { InventoryAdjustment, InventoryAdjustmentCreate, InventoryAvailability, InventoryControlAction, InventoryCount, InventoryCountCreate, InventoryCountSubmit, InventoryCustomerReturnBoundary, InventoryMovement, InventoryOpeningBalance, InventoryOpeningCreateRequest, InventoryReasonCategory, InventoryReasonCode, InventoryReasonCodeCreate, InventoryReasonCodeUpdate, InventoryReservation, InventoryReservationCreateRequest, InventoryStockIssue, InventoryStockIssueCreate, InventoryTransfer, InventoryTransferActionRequest, InventoryTransferCreateRequest, InventoryWarehouseOption } from './inventory.model';

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
  transfers(filters: { companyId?: string; branchId?: string } = {}): Observable<InventoryTransfer[]> { return this.api.get<InventoryTransfer[]>(`/inventory/transfers${this.query(filters)}`); }
  customerReturnBoundary(): Observable<InventoryCustomerReturnBoundary> { return this.api.get<InventoryCustomerReturnBoundary>('/inventory/customer-returns/boundary'); }
  reasonCodes(category?: InventoryReasonCategory, includeInactive = false): Observable<InventoryReasonCode[]> { return this.api.get<InventoryReasonCode[]>(`/inventory/reason-codes${this.query({ category, includeInactive: includeInactive ? 'true' : undefined })}`); }
  adjustments(filters: { warehouseId?: string; companyId?: string; branchId?: string } = {}): Observable<InventoryAdjustment[]> { return this.api.get<InventoryAdjustment[]>(`/inventory/adjustments${this.query(filters)}`); }
  counts(filters: { warehouseId?: string; companyId?: string; branchId?: string } = {}): Observable<InventoryCount[]> { return this.api.get<InventoryCount[]>(`/inventory/counts${this.query(filters)}`); }
  counterView(id: string): Observable<InventoryCount> { return this.api.get<InventoryCount>(`/inventory/counts/${id}/counter-view`); }
  stockIssues(filters: { warehouseId?: string; companyId?: string; branchId?: string } = {}): Observable<InventoryStockIssue[]> { return this.api.get<InventoryStockIssue[]>(`/inventory/stock-issues${this.query(filters)}`); }

  async createOpening(payload: InventoryOpeningCreateRequest): Promise<InventoryOpeningBalance> { return this.mutate('/inventory/opening-balances', payload); }
  async validateOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/validate`, version, { reason }); }
  async postOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/post`, version, { reason }); }
  async correctOpening(id: string, version: string, reason?: string): Promise<InventoryOpeningBalance> { return this.action(`/inventory/opening-balances/${id}/correct`, version, { reason }); }
  async createReservation(payload: InventoryReservationCreateRequest): Promise<InventoryReservation> { return this.mutate('/inventory/reservations', payload); }
  async reduceReservation(id: string, version: string, quantity: number, reason?: string): Promise<InventoryReservation> { return this.action(`/inventory/reservations/${id}/reduce`, version, { quantity, reason }); }
  async releaseReservation(id: string, version: string, reason?: string): Promise<InventoryReservation> { return this.action(`/inventory/reservations/${id}/release`, version, { reason }); }
  async postGoodsReceipt(goodsReceiptId: string, goodsReceiptLineId: string, version: string): Promise<unknown> { return this.action(`/inventory/goods-receipts/${goodsReceiptId}/lines/${goodsReceiptLineId}/post`, version, {}); }
  async postSupplierReturn(supplierReturnId: string, version: string): Promise<unknown> { return this.action(`/inventory/supplier-returns/${supplierReturnId}/post`, version, {}); }
  async createTransfer(payload: InventoryTransferCreateRequest): Promise<InventoryTransfer> { return this.mutate('/inventory/transfers', payload); }
  async createReasonCode(payload: InventoryReasonCodeCreate): Promise<InventoryReasonCode> { return this.mutate('/inventory/reason-codes', payload); }
  async updateReasonCode(id: string, version: string, payload: InventoryReasonCodeUpdate): Promise<InventoryReasonCode> {
    if (!await this.auth.bootstrapAntiforgery()) throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey()).set('If-Match', `"${version.replace(/^"|"$/g, '')}"`);
    return firstValueFrom(this.api.post<InventoryReasonCode>(`/inventory/reason-codes/${id}/update`, payload, { headers }));
  }
  async createAdjustment(payload: InventoryAdjustmentCreate): Promise<InventoryAdjustment> { return this.mutate('/inventory/adjustments', payload); }
  async submitAdjustment(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryAdjustment> { return this.action(`/inventory/adjustments/${id}/submit`, version, action); }
  async approveAdjustment(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryAdjustment> { return this.action(`/inventory/adjustments/${id}/approve`, version, action); }
  async postAdjustment(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryAdjustment> { return this.action(`/inventory/adjustments/${id}/post`, version, action); }
  async createCount(payload: InventoryCountCreate): Promise<InventoryCount> { return this.mutate('/inventory/counts', payload); }
  async submitCount(id: string, version: string, payload: InventoryCountSubmit): Promise<InventoryCount> { return this.action(`/inventory/counts/${id}/submit`, version, payload); }
  async approveCount(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryCount> { return this.action(`/inventory/counts/${id}/approve`, version, action); }
  async postCount(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryCount> { return this.action(`/inventory/counts/${id}/post`, version, action); }
  async resnapshotCount(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryCount> { return this.action(`/inventory/counts/${id}/resnapshot`, version, action); }
  async createStockIssue(payload: InventoryStockIssueCreate): Promise<InventoryStockIssue> { return this.mutate('/inventory/stock-issues', payload); }
  async submitStockIssue(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryStockIssue> { return this.action(`/inventory/stock-issues/${id}/submit`, version, action); }
  async approveStockIssue(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryStockIssue> { return this.action(`/inventory/stock-issues/${id}/approve`, version, action); }
  async postStockIssue(id: string, version: string, action: InventoryControlAction = {}): Promise<InventoryStockIssue> { return this.action(`/inventory/stock-issues/${id}/post`, version, action); }
  async correctMovement(id: string, version: string, reasonCode: string, reason?: string): Promise<InventoryMovement> { return this.action(`/inventory/ledger/${id}/correct`, version, { reasonCode, reason }); }
  async completeDirectTransfer(id: string, version: string, action: InventoryTransferActionRequest = {}): Promise<InventoryTransfer> { return this.action(`/inventory/transfers/${id}/complete-direct`, version, action); }
  async shipTransfer(id: string, version: string, action: InventoryTransferActionRequest = {}): Promise<InventoryTransfer> { return this.action(`/inventory/transfers/${id}/ship`, version, action); }
  async receiveTransfer(id: string, version: string, action: InventoryTransferActionRequest): Promise<InventoryTransfer> { return this.action(`/inventory/transfers/${id}/receive`, version, action); }
  async resolveTransferShortage(id: string, version: string, action: InventoryTransferActionRequest): Promise<InventoryTransfer> { return this.action(`/inventory/transfers/${id}/resolve-shortage`, version, action); }
  async cancelTransfer(id: string, version: string, action: InventoryTransferActionRequest = {}): Promise<InventoryTransfer> { return this.action(`/inventory/transfers/${id}/cancel`, version, action); }

  private async action<T>(path: string, version: string, payload: unknown): Promise<T> { return this.mutate(path, payload, version); }
  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    if (!await this.auth.bootstrapAntiforgery()) throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) headers = headers.set('If-Match', `"${version.replace(/^"|"$/g, '')}"`);
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }
}
