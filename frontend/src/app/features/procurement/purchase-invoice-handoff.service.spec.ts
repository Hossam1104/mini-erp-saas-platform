import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PurchaseInvoiceHandoffService } from './purchase-invoice-handoff.service';

describe('PurchaseInvoiceHandoffService', () => {
  let service: PurchaseInvoiceHandoffService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PurchaseInvoiceHandoffService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads eligible sources and Tenant-scoped Purchase Invoice Handoffs', () => {
    service.eligibleSources().subscribe((sources) => expect(sources).toEqual([]));
    const sources = httpMock.expectOne('/api/v1/procurement/invoice-handoff-sources');
    expect(sources.request.method).toBe('GET');
    sources.flush([]);

    service.list('Recorded', 'po-1').subscribe((records) => expect(records).toEqual([]));
    const list = httpMock.expectOne('/api/v1/procurement/invoice-handoffs?status=Recorded&purchaseOrderId=po-1');
    expect(list.request.method).toBe('GET');
    list.flush([]);
  });

  it('reads Purchase Invoice Handoff detail, lifecycle history, and audit evidence', () => {
    service.get('pih-1').subscribe((record) => expect(record.id).toBe('pih-1'));
    httpMock.expectOne('/api/v1/procurement/invoice-handoffs/pih-1').flush({ id: 'pih-1' });

    service.history('pih-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/invoice-handoffs/pih-1/history').flush([]);

    service.audit('pih-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/invoice-handoffs/pih-1/audit').flush([]);
  });

  it('creates purchase invoice handoff with antiforgery and idempotency headers', async () => {
    const payload = {
      purchaseOrderId: 'po-1',
      supplierInvoiceReference: 'INV-12345',
      supplierInvoiceDate: '2026-08-19',
      notes: 'Test handoff',
      sources: [{ goodsReceiptId: 'gr-1', goodsReceiptLineId: 'grl-1', quantity: 2 }],
    };

    const createPromise = service.create(payload);
    await Promise.resolve();
    const create = httpMock.expectOne('/api/v1/procurement/invoice-handoffs');
    expect(create.request.body).toEqual(payload);
    expect(create.request.headers.has('Idempotency-Key')).toBe(true);
    create.flush({ id: 'pih-1' });
    await createPromise;
  });

  it('cancels purchase invoice handoff with optimistic concurrency If-Match header', async () => {
    const cancelPromise = service.cancel('pih-1', 'V1', 'Wrong invoice number');
    await Promise.resolve();
    const cancel = httpMock.expectOne('/api/v1/procurement/invoice-handoffs/pih-1/cancel');
    expect(cancel.request.headers.get('If-Match')).toBe('"V1"');
    expect(cancel.request.headers.has('Idempotency-Key')).toBe(true);
    expect(cancel.request.body).toEqual({ reason: 'Wrong invoice number' });
    cancel.flush({ id: 'pih-1', status: 'Cancelled' });
    await cancelPromise;
  });

  it('fails closed when antiforgery bootstrap is unavailable', async () => {
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(false);
    await expect(service.cancel('pih-1', 'V1')).rejects.toMatchObject({ status: 403 });
    httpMock.expectNone('/api/v1/procurement/invoice-handoffs/pih-1/cancel');
  });
});
