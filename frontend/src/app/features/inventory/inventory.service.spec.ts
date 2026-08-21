import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { InventoryService } from './inventory.service';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 1,
};

describe('InventoryService', () => {
  let auth: AuthService;
  let service: InventoryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        InventoryService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(InventoryService);
    http = TestBed.inject(HttpTestingController);
    auth.acceptServerSession(session);
  });

  afterEach(() => http.verify());

  it('keeps availability queries server-readable and does not send a client Tenant id', () => {
    service.availability({
      warehouseId: 'warehouse-1',
      companyId: 'company-1',
      branchId: null,
      productId: 'product-1',
      unitOfMeasureId: 'unit-1',
      trackingIdentity: 'LOT-1',
    }).subscribe();

    const request = http.expectOne('/api/v1/inventory/availability?warehouseId=warehouse-1&companyId=company-1&productId=product-1&unitOfMeasureId=unit-1&trackingIdentity=LOT-1');
    expect(request.request.method).toBe('GET');
    expect(request.request.urlWithParams).not.toContain('tenantId');
    request.flush({});
  });

  it('sends antiforgery, idempotency, and If-Match evidence for opening actions', async () => {
    const saving = service.validateOpening('opening-1', 'AQ==', 'Validated by Inventory');

    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    const request = http.expectOne('/api/v1/inventory/opening-balances/opening-1/validate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Validated by Inventory' });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('memory-token');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.headers.get('If-Match')).toBe('"AQ=="');
    request.flush({ id: 'opening-1', version: 'Ag==' });

    await expect(saving).resolves.toMatchObject({ id: 'opening-1', version: 'Ag==' });
  });
});
