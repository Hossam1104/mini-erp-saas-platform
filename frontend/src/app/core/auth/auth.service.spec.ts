import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from '../api/auth.interceptor';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('bootstraps a server-issued session with credentials enabled', async () => {
    const result = service.ensureSession();
    const request = http.expectOne('/api/v1/auth/session');
    expect(request.request.withCredentials).toBe(true);
    request.flush({
      authenticated: true,
      actorId: 'actor-1',
      sessionId: 'session-1',
      lifecycleState: 'Active',
      absoluteExpiresAt: null,
      selectedPath: null,
      selectedTenantId: null,
      selectedContextId: null,
      selectionVersion: 0,
    });

    await expect(result).resolves.toBe(true);
    expect(service.status()).toBe('authenticated');
    expect(service.session()?.actorId).toBe('actor-1');
  });

  it('returns to an anonymous safe boundary on a rejected session bootstrap', async () => {
    const result = service.ensureSession();
    const request = http.expectOne('/api/v1/auth/session');
    request.flush({ code: 'authentication_failed' }, { status: 401, statusText: 'Unauthorized' });

    await expect(result).resolves.toBe(false);
    expect(service.status()).toBe('anonymous');
    expect(service.session()).toBeNull();
  });

  it('keeps antiforgery material in memory and exposes it only as a request header', async () => {
    const result = service.bootstrapAntiforgery();
    const request = http.expectOne('/api/v1/auth/antiforgery');
    request.flush({ status: 'issued' }, { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) });

    await expect(result).resolves.toBe(true);
    expect(service.requestHeaders().get('X-CSRF-TOKEN')).toBe('memory-token');
  });
});
