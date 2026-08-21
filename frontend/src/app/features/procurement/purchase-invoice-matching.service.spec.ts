import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PurchaseInvoiceMatchingService } from './purchase-invoice-matching.service';

describe('PurchaseInvoiceMatchingService', () => {
  let service: PurchaseInvoiceMatchingService;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PurchaseInvoiceMatchingService);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    vi.spyOn(auth, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('lists matching evaluations with server-owned filters', async () => {
    const request = service.list('handoff-1', 'ExceptionHold');
    const http = httpMock.expectOne('/api/v1/procurement/purchase-invoice-matches?handoffId=handoff-1&result=ExceptionHold');
    expect(http.request.method).toBe('GET');
    http.flush([]);
    await expect(request).resolves.toEqual([]);
  });

  it('evaluates and resolves with antiforgery, idempotency, and optimistic concurrency headers', async () => {
    const evaluatePayload = { exchangeRateReference: { exchangeRateId: 'rate-1' } };
    const evaluate = service.evaluate('handoff-1', 'V1', evaluatePayload);
    await Promise.resolve();
    const evaluation = httpMock.expectOne('/api/v1/procurement/purchase-invoice-handoffs/handoff-1/evaluate-match');
    expect(evaluation.request.headers.get('If-Match')).toBe('"V1"');
    expect(evaluation.request.headers.has('Idempotency-Key')).toBe(true);
    expect(evaluation.request.body).toEqual(evaluatePayload);
    evaluation.flush({ id: 'match-1', result: 'ExceptionHold' });
    await evaluate;

    const resolve = service.resolve('match-1', 'V2', { reason: 'Reviewed supporting evidence' });
    await Promise.resolve();
    const resolution = httpMock.expectOne('/api/v1/procurement/purchase-invoice-matches/match-1/resolve-exception');
    expect(resolution.request.headers.get('If-Match')).toBe('"V2"');
    expect(resolution.request.headers.has('Idempotency-Key')).toBe(true);
    resolution.flush({ id: 'match-1', result: 'ResolvedException' });
    await resolve;
  });

  it('fails closed when antiforgery bootstrap is unavailable', async () => {
    vi.spyOn(auth, 'bootstrapAntiforgery').mockResolvedValue(false);
    await expect(service.resolve('match-1', 'V1', { reason: 'No mutation' })).rejects.toMatchObject({ status: 403 });
    httpMock.expectNone('/api/v1/procurement/purchase-invoice-matches/match-1/resolve-exception');
  });
});
