import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { InventoryValuationService } from './inventory-valuation.service';

describe('InventoryValuationService', () => {
  let service: InventoryValuationService;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(InventoryValuationService);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    vi.spyOn(auth, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads the summary, immutable history, pending events, and Finance handoff facts through the bounded routes', () => {
    service.summary({ companyId: 'company-a', warehouseId: 'warehouse-a' }).subscribe((records) => expect(records).toEqual([]));
    expect(httpMock.expectOne('/api/v1/inventory/valuation/summary?companyId=company-a&warehouseId=warehouse-a').request.method).toBe('GET');

    service.history({ companyId: 'company-a' }).subscribe((records) => expect(records).toEqual([]));
    expect(httpMock.expectOne('/api/v1/inventory/valuation/history?companyId=company-a').request.method).toBe('GET');

    service.pending({ companyId: 'company-a' }).subscribe((records) => expect(records).toEqual([]));
    expect(httpMock.expectOne('/api/v1/inventory/valuation/pending?companyId=company-a').request.method).toBe('GET');

    service.financeHandoffs({ companyId: 'company-a' }).subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/inventory/valuation/finance-handoffs?companyId=company-a').flush([]);
    httpMock.match((request) => request.url.includes('/summary') || request.url.includes('/history') || request.url.includes('/pending')).forEach((request) => request.flush([]));
  });

  it('processes a server-scoped valuation run with antiforgery and idempotency headers', async () => {
    const result = service.process({ companyId: 'company-a', warehouseId: 'warehouse-a' });
    await Promise.resolve();
    const request = httpMock.expectOne('/api/v1/inventory/valuation/process');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ companyId: 'company-a', warehouseId: 'warehouse-a' });
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    request.flush({ appliedCount: 1 });
    await expect(result).resolves.toEqual({ appliedCount: 1 });
  });
});
