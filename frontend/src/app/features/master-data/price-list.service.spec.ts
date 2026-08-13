import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PriceListService } from './price-list.service';
import { PriceListRecord, PriceListPriceVersionRecord, PriceListReferenceRecord } from './price-list.model';

describe('PriceListService', () => {
  let service: PriceListService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  const mockPriceList: PriceListRecord = {
    id: 'pl-1',
    tenantId: '11111111-1111-1111-1111-111111111111',
    code: 'PL-USD-STANDARD',
    englishName: 'Standard USD Price List',
    arabicName: 'قائمة الأسعار القياسية بالدولار',
    currencyId: 'curr-usd',
    currencyCode: 'USD',
    customerId: null,
    organizationScopeKind: null,
    organizationScopeId: null,
    priority: 10,
    lifecycleState: 'Active',
    currentVersionNumber: 1,
    prices: [],
    version: 'AQIDBAUGBwg=',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PriceListService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists price lists with optional search query', () => {
    service.list('standard').subscribe((result) => {
      expect(result).toEqual([mockPriceList]);
    });

    const req = httpMock.expectOne('/api/v1/master-data/price-lists?q=standard');
    expect(req.request.method).toBe('GET');
    req.flush([mockPriceList]);
  });

  it('gets detail and history for a price list', () => {
    service.get('pl-1').subscribe((record) => {
      expect(record.code).toBe('PL-USD-STANDARD');
    });

    const req1 = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1');
    expect(req1.request.method).toBe('GET');
    req1.flush(mockPriceList);

    const mockPriceVersion: PriceListPriceVersionRecord = {
      id: 'pv-1',
      versionNumber: 1,
      productId: 'prod-1',
      productSku: 'SKU-001',
      unitOfMeasureId: 'uom-1',
      unitOfMeasureCode: 'PCS',
      currencyId: 'curr-usd',
      currencyCode: 'USD',
      customerId: null,
      organizationScopeKind: null,
      organizationScopeId: null,
      priority: 10,
      effectiveFrom: '2026-01-01',
      effectiveTo: null,
      price: 100.0,
      priceScale: 2,
      provenance: 'Configured',
      sourceReference: 'REF-100',
      version: 'AQIDBAUGBwg=',
    };

    service.history('pl-1').subscribe((history) => {
      expect(history.length).toBe(1);
      expect(history[0].productSku).toBe('SKU-001');
    });

    const req2 = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/history');
    expect(req2.request.method).toBe('GET');
    req2.flush([mockPriceVersion]);
  });

  it('creates price list with antiforgery and idempotency headers', async () => {
    const createPromise = service.create({
      code: 'PL-USD-STANDARD',
      englishName: 'Standard USD Price List',
      arabicName: 'قائمة الأسعار القياسية بالدولار',
      currencyId: 'curr-usd',
      customerId: null,
      organizationScopeKind: null,
      organizationScopeId: null,
      priority: 10,
    });

    await Promise.resolve();

    const req = httpMock.expectOne('/api/v1/master-data/price-lists');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush(mockPriceList);

    const result = await createPromise;
    expect(result.id).toBe('pl-1');
  });

  it('edits price list attaching If-Match version header', async () => {
    const editPromise = service.edit(
      'pl-1',
      {
        code: 'PL-USD-STANDARD',
        englishName: 'Updated USD Price List',
        arabicName: 'قائمة الأسعار القياسية',
        currencyId: 'curr-usd',
        customerId: null,
        organizationScopeKind: null,
        organizationScopeId: null,
        priority: 5,
      },
      'AQIDBAUGBwg=',
    );

    await Promise.resolve();

    const req = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/edit');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush({ ...mockPriceList, priority: 5 });

    const result = await editPromise;
    expect(result.priority).toBe(5);
  });

  it('appends price version with required version and mutation headers', async () => {
    const appendPromise = service.appendPrice(
      'pl-1',
      {
        productId: 'prod-1',
        unitOfMeasureId: 'uom-1',
        effectiveFrom: '2026-01-01',
        effectiveTo: null,
        price: 150.0,
        priceScale: 2,
        provenance: 'Manual',
        sourceReference: 'Manual price adjustment',
      },
      'AQIDBAUGBwg=',
    );

    await Promise.resolve();

    const req = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/prices');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    req.flush(mockPriceList);

    const result = await appendPromise;
    expect(result.id).toBe('pl-1');
  });

  it('deactivates and reactivates price list passing version header', async () => {
    const deactivatePromise = service.deactivate('pl-1', 'AQIDBAUGBwg=');
    await Promise.resolve();

    const reqDeact = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/deactivate');
    expect(reqDeact.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    reqDeact.flush({ ...mockPriceList, lifecycleState: 'Inactive' });

    const deactResult = await deactivatePromise;
    expect(deactResult.lifecycleState).toBe('Inactive');

    const reactivatePromise = service.reactivate('pl-1', 'AQIDBAUGBwg=');
    await Promise.resolve();

    const reqReact = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/reactivate');
    expect(reqReact.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    reqReact.flush({ ...mockPriceList, lifecycleState: 'Active' });

    const reactResult = await reactivatePromise;
    expect(reactResult.lifecycleState).toBe('Active');
  });

  it('resolves pricing reference via query parameters', () => {
    const mockReference: PriceListReferenceRecord = {
      priceListId: 'pl-1',
      tenantId: '11111111-1111-1111-1111-111111111111',
      priceListCode: 'PL-USD-STANDARD',
      priceVersionId: 'pv-1',
      priceVersionNumber: 1,
      productId: 'prod-1',
      productSku: 'SKU-001',
      unitOfMeasureId: 'uom-1',
      unitOfMeasureCode: 'PCS',
      currencyId: 'curr-usd',
      currencyCode: 'USD',
      customerId: null,
      organizationScopeKind: null,
      organizationScopeId: null,
      priority: 10,
      effectiveOn: '2026-08-13',
      effectiveFrom: '2026-01-01',
      effectiveTo: null,
      price: 100.0,
      priceScale: 2,
      provenance: 'Configured',
      sourceReference: 'REF-100',
      referenceValue: '100.00 USD',
      priceVersion: 'AQIDBAUGBwg=',
      masterVersion: 'AQIDBAUGBwg=',
    };

    service
      .resolveReference({
        productId: 'prod-1',
        unitOfMeasureId: 'uom-1',
        currencyId: 'curr-usd',
        effectiveOn: '2026-08-13',
      })
      .subscribe((ref) => {
        expect(ref.priceListCode).toBe('PL-USD-STANDARD');
        expect(ref.price).toBe(100.0);
      });

    const req = httpMock.expectOne((request) => request.url.startsWith('/api/v1/master-data/price-lists/reference'));
    expect(req.request.method).toBe('GET');
    expect(req.request.url).toContain('productId=prod-1');
    expect(req.request.url).toContain('unitOfMeasureId=uom-1');
    expect(req.request.url).toContain('currencyId=curr-usd');
    expect(req.request.url).toContain('effectiveOn=2026-08-13');
    req.flush(mockReference);
  });

  it('fetches audit evidence for price list', () => {
    service.audit('pl-1').subscribe((entries) => {
      expect(entries.length).toBe(1);
      expect(entries[0].operationId).toBe('master-data.price-list.create');
    });

    const req = httpMock.expectOne('/api/v1/master-data/price-lists/pl-1/audit');
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        evidenceId: 'ev-1',
        occurredAt: '2026-08-13T10:00:00Z',
        operationId: 'master-data.price-list.create',
        correlationId: 'corr-1',
        tenantId: '11111111-1111-1111-1111-111111111111',
        actorId: 'actor-1',
        sessionId: 'sess-1',
        authorizationPath: 'ordinary',
        operation: 'create',
        policyOutcome: 'allowed',
        decision: 'Success',
        reason: 'allowed',
        beforeSummary: null,
        afterSummary: 'Code: PL-USD-STANDARD',
        approverId: null,
      },
    ]);
  });
});
