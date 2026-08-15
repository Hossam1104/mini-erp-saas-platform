import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PurchaseRequestResponse, PurchaseRequestWriteRequest } from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';

describe('PurchaseRequestService', () => {
  let service: PurchaseRequestService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  const mockRequest: PurchaseRequestResponse = {
    id: 'pr-1',
    tenantId: '11111111-1111-1111-1111-111111111111',
    companyId: 'company-1',
    branchId: null,
    requesterId: 'requester-1',
    status: 'Draft',
    purpose: 'Office supplies',
    lines: [],
    approval: null,
    createdAt: '2026-08-13T10:00:00Z',
    updatedAt: '2026-08-13T10:00:00Z',
    submittedAt: null,
    approvedAt: null,
    cancelledAt: null,
    version: 'AQIDBAUGBwg=',
    canEdit: true,
    canSubmit: true,
    canApprove: false,
    canReject: false,
    canReturnForChange: false,
    canCancel: true,
  };

  const writePayload: PurchaseRequestWriteRequest = {
    companyId: 'company-1',
    branchId: null,
    purpose: 'Office supplies',
    lines: [
      { productId: 'product-1', unitOfMeasureId: 'unit-1', quantity: 5, needByDate: '2026-09-01', purpose: null },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PurchaseRequestService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists purchase requests with an optional status filter', () => {
    service.list('Draft').subscribe((result) => {
      expect(result).toEqual([]);
    });

    const req = httpMock.expectOne('/api/v1/procurement/purchase-requests?status=Draft');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('lists purchase requests without a status filter', () => {
    service.list().subscribe((result) => {
      expect(result).toEqual([]);
    });

    const req = httpMock.expectOne('/api/v1/procurement/purchase-requests');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('gets detail, history, and audit for a purchase request', () => {
    service.get('pr-1').subscribe((record) => {
      expect(record.companyId).toBe('company-1');
    });
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1').flush(mockRequest);

    service.history('pr-1').subscribe((entries) => {
      expect(entries).toEqual([]);
    });
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/history').flush([]);

    service.audit('pr-1').subscribe((entries) => {
      expect(entries).toEqual([]);
    });
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/audit').flush([]);
  });

  it('creates a purchase request with antiforgery and idempotency headers', async () => {
    const createPromise = service.create(writePayload);
    await Promise.resolve();

    const req = httpMock.expectOne('/api/v1/procurement/purchase-requests');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(mockRequest);

    const result = await createPromise;
    expect(result.id).toBe('pr-1');
  });

  it('edits a purchase request attaching the If-Match version header', async () => {
    const editPromise = service.edit('pr-1', writePayload, 'AQIDBAUGBwg=');
    await Promise.resolve();

    const req = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/edit');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    req.flush(mockRequest);

    await editPromise;
  });

  it('submits, approves, rejects, returns, and cancels using the version header', async () => {
    const submitPromise = service.submit('pr-1', 'AQIDBAUGBwg=');
    await Promise.resolve();
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/submit').flush({ ...mockRequest, status: 'PendingApproval' });
    await submitPromise;

    const approvePromise = service.approve('pr-1', 'AQIDBAUGBwg=');
    await Promise.resolve();
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/approve').flush({ ...mockRequest, status: 'Approved' });
    await approvePromise;

    const rejectPromise = service.reject('pr-1', 'AQIDBAUGBwg=', 'Budget exceeded');
    await Promise.resolve();
    const rejectReq = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/reject');
    expect(rejectReq.request.body).toEqual({ reason: 'Budget exceeded' });
    rejectReq.flush({ ...mockRequest, status: 'Rejected' });
    await rejectPromise;

    const returnPromise = service.returnForChange('pr-1', 'AQIDBAUGBwg=', 'Missing quantity');
    await Promise.resolve();
    httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/return-for-change').flush({ ...mockRequest, status: 'ReturnedForChange' });
    await returnPromise;

    const cancelPromise = service.cancel('pr-1', 'AQIDBAUGBwg=');
    await Promise.resolve();
    const cancelReq = httpMock.expectOne('/api/v1/procurement/purchase-requests/pr-1/cancel');
    expect(cancelReq.request.body).toEqual({ reason: null });
    cancelReq.flush({ ...mockRequest, status: 'Cancelled' });
    await cancelPromise;
  });
});
