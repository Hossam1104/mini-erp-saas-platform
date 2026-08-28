import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { SalesQuotationCreateRequest } from './sales.model';
import { SalesService } from './sales.service';

describe('SalesService', () => {
  let service: SalesService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  const payload: SalesQuotationCreateRequest = {
    companyId: 'company-1',
    branchId: 'branch-1',
    customerId: 'customer-1',
    quotationDate: '2026-08-28',
    validUntil: '2026-09-30',
    currencyId: 'currency-1',
    priceListId: 'price-list-1',
    customerContactId: null,
    notes: 'B2B quote',
    customerReference: 'RFQ-1',
    lines: [{ productId: 'product-1', unitOfMeasureId: 'uom-1', quantity: 2, discountPercent: 0, unitPriceOverride: null, notes: null }],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SalesService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('lists quotations and orders with server status filters', () => {
    service.quotations('PendingApproval').subscribe(records => expect(records).toEqual([]));
    const quotations = httpMock.expectOne('/api/v1/sales/quotations?status=PendingApproval');
    expect(quotations.request.method).toBe('GET');
    quotations.flush([]);

    service.orders().subscribe(records => expect(records).toEqual([]));
    const orders = httpMock.expectOne('/api/v1/sales/orders');
    expect(orders.request.method).toBe('GET');
    orders.flush([]);
  });

  it('creates a quotation with antiforgery and idempotency headers', async () => {
    const promise = service.createQuotation(payload);
    await Promise.resolve();
    const request = httpMock.expectOne('/api/v1/sales/quotations');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    expect(request.request.headers.has('If-Match')).toBe(false);
    request.flush({ id: 'quotation-1' });
    await promise;
  });

  it('attaches optimistic concurrency tokens to edit and conversion', async () => {
    const edit = service.editQuotation('quotation-1', { ...payload }, 'AQIDBAUGBwg=');
    await Promise.resolve();
    const editRequest = httpMock.expectOne('/api/v1/sales/quotations/quotation-1/edit');
    expect(editRequest.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    editRequest.flush({ id: 'quotation-1' });
    await edit;

    const convert = service.convertQuotation('quotation-1', 'AQIDBAUGBwg=');
    await Promise.resolve();
    const convertRequest = httpMock.expectOne('/api/v1/sales/quotations/quotation-1/convert');
    expect(convertRequest.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    convertRequest.flush({ id: 'order-1' });
    await convert;
  });

  it('sends explicit credit override reason and expiry with concurrency protection', async () => {
    const override = service.overrideCredit('order-1', { reason: 'Finance-approved exception', expiresAt: '2026-09-01T12:00:00.000Z', scope: 'company', sourceReference: 'memo-1' }, 'AQIDBAUGBwg=');
    await Promise.resolve();
    const request = httpMock.expectOne('/api/v1/sales/orders/order-1/credit/override');
    expect(request.request.body.reason).toBe('Finance-approved exception');
    expect(request.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    request.flush({ id: 'order-1' });
    await override;
  });
});
