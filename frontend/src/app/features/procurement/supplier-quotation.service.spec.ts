import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { SupplierQuotationService } from './supplier-quotation.service';

describe('SupplierQuotationService', () => {
  let service: SupplierQuotationService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(SupplierQuotationService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads quotation list, comparison, source decision, and immutable decision history from the Phase-C routes', () => {
    service.list('pr-1').subscribe((records) => expect(records).toEqual([]));
    const list = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/quotations');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    service.comparison('pr-1').subscribe((comparison) => expect(comparison.purchaseRequestId).toBe('pr-1'));
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/quotation-comparison').flush({ purchaseRequestId: 'pr-1' });

    service.sourceDecision('pr-1').subscribe((decision) => expect(decision).toBeNull());
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/source-decision').flush(null);

    service.sourceDecisionHistory('pr-1').subscribe((history) => expect(history).toEqual([]));
    const history = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/source-decision/history');
    expect(history.request.method).toBe('GET');
    history.flush([]);
  });

  it('sends lifecycle and edit mutations with antiforgery, idempotency, and If-Match headers', async () => {
    const editPromise = service.edit('sq-1', { supplierId: 'supplier-1' } as never, 'AQIDBAUGBwg=');
    await Promise.resolve();
    const edit = httpMock.expectOne('/api/v1/procurement/quotations/sq-1/edit');
    expect(edit.request.method).toBe('POST');
    expect(edit.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    expect(edit.request.headers.has('Idempotency-Key')).toBe(true);
    edit.flush({ id: 'sq-1' });
    await editPromise;

    const submitPromise = service.submit('sq-1', 'AQIDBAUGBwg=');
    await Promise.resolve();
    const submit = httpMock.expectOne('/api/v1/procurement/quotations/sq-1/submit');
    expect(submit.request.body).toEqual({});
    expect(submit.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    submit.flush({ id: 'sq-1', status: 'Submitted' });
    await submitPromise;

    const disqualifyPromise = service.disqualify('sq-1', 'AQIDBAUGBwg=', 'Incomplete coverage');
    await Promise.resolve();
    const disqualify = httpMock.expectOne('/api/v1/procurement/quotations/sq-1/disqualify');
    expect(disqualify.request.body).toEqual({ reason: 'Incomplete coverage' });
    disqualify.flush({ id: 'sq-1', status: 'Disqualified' });
    await disqualifyPromise;
  });

  it('records source decisions with the supplied expectedVersion concurrency token', async () => {
    const firstDecisionPromise = service.recordSourceDecision('pr-1', 'sq-1', 'Best same-currency coverage', 'PRVERSION');
    await Promise.resolve();

    const firstRequest = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/source-decision');
    expect(firstRequest.request.method).toBe('POST');
    expect(firstRequest.request.body).toEqual({ selectedQuotationId: 'sq-1', rationale: 'Best same-currency coverage' });
    expect(firstRequest.request.headers.get('If-Match')).toBe('"PRVERSION"');
    expect(firstRequest.request.headers.has('Idempotency-Key')).toBe(true);
    firstRequest.flush({ id: 'decision-1', selectedQuotationId: 'sq-1', version: 'DECISION-V1' });
    await firstDecisionPromise;

    const reselectionPromise = service.recordSourceDecision('pr-1', 'sq-2', 'Superceding decision', 'DECISION-V1');
    await Promise.resolve();

    const reselectionRequest = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/source-decision');
    expect(reselectionRequest.request.method).toBe('POST');
    expect(reselectionRequest.request.body).toEqual({ selectedQuotationId: 'sq-2', rationale: 'Superceding decision' });
    expect(reselectionRequest.request.headers.get('If-Match')).toBe('"DECISION-V1"');
    reselectionRequest.flush({ id: 'decision-2', selectedQuotationId: 'sq-2', version: 'DECISION-V2' });
    await reselectionPromise;
  });

  it('fails closed when antiforgery bootstrap cannot be established', async () => {
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(false);

    await expect(service.submit('sq-1', 'version')).rejects.toMatchObject({ status: 403 });
    httpMock.expectNone('/api/v1/procurement/quotations/sq-1/submit');
  });
});
