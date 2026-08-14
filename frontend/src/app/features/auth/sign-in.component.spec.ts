import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { FoundationSessionResponse } from '../../core/api/foundation.models';
import { SignInComponent } from './sign-in.component';

const moduleRegistration = {
  module: 'platform-administration',
  name: 'Platform Administration',
  boundary: 'platform',
  registered: true,
  masterData: { module: 'master-data-catalog', name: 'Master Data Catalog', boundary: 'master-data', registered: true },
  businessParties: { module: 'business-parties', name: 'Business Parties', boundary: 'business-parties', registered: true },
};

const authenticatedSession: FoundationSessionResponse = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: null,
  selectionVersion: 1,
};

describe('SignInComponent', () => {
  let fixture: ComponentFixture<SignInComponent>;
  let component: SignInComponent;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SignInComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    fixture = TestBed.createComponent(SignInComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
  });

  function tick(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  async function flushPreflight(response: object = moduleRegistration, status = 200): Promise<void> {
    const request = http.expectOne('/api/v1/module-registration');
    if (status === 200) {
      request.flush(response);
    } else {
      request.flush(response, { status, statusText: 'Error' });
    }
    await tick();
  }

  it('prefills the Development username and never prefills the password', async () => {
    fixture.detectChanges();
    await flushPreflight();
    expect(component.form.controls.login.value).toBe('admin@minierp.local');
    expect(component.form.controls.password.value).toBe('');
  });

  it('runs the Development API identity preflight on init and reports connected', async () => {
    fixture.detectChanges();
    await flushPreflight();
    expect(component.devApiStatus()).toBe('connected');
    expect(component.blockedByDevPreflight()).toBe(false);
  });

  it('blocks sign-in submission when the Development API preflight identifies a different service', async () => {
    fixture.detectChanges();
    await flushPreflight({ ...moduleRegistration, module: 'unrelated-service' });
    expect(component.devApiStatus()).toBe('unavailable');

    component.form.controls.password.setValue('anything');
    await component.submit();

    http.expectNone('/api/v1/auth/sign-in');
  });

  it('permits sign-in once the Development API preflight succeeds', async () => {
    fixture.detectChanges();
    await flushPreflight();
    component.form.controls.password.setValue('correct-password');

    const submitPromise = component.submit();
    const signInRequest = http.expectOne('/api/v1/auth/sign-in');
    signInRequest.flush({ ...authenticatedSession, selectedContextId: 'context-a' });
    await submitPromise;

    expect(router.navigate).toHaveBeenCalledWith(['/app']);
  });

  it('toggles password visibility with the show/hide control', async () => {
    fixture.detectChanges();
    await flushPreflight();
    fixture.detectChanges();

    expect(component.passwordVisible()).toBe(false);
    const button = fixture.nativeElement.querySelector('.password-toggle') as HTMLButtonElement;
    button.click();
    expect(component.passwordVisible()).toBe(true);
  });

  it('shows the safe authentication error and a Development password hint after a failed sign-in', async () => {
    fixture.detectChanges();
    await flushPreflight();
    component.form.controls.password.setValue('wrong-password');

    const submitPromise = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(
      { code: 'authentication_failed' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await submitPromise;
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('We could not sign you in. Check your details and try again.');
    expect(component.showDevPasswordHint()).toBe(true);
  });

  it('clears the stale error once the login or password changes', async () => {
    fixture.detectChanges();
    await flushPreflight();
    component.form.controls.password.setValue('wrong-password');

    const submitPromise = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(
      { code: 'authentication_failed' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await submitPromise;
    expect(component.errorMessage()).not.toBe('');

    component.form.controls.password.setValue('trying-again');
    expect(component.errorMessage()).toBe('');
  });

  it('navigates to workspace selection when multiple eligible contexts are returned', async () => {
    fixture.detectChanges();
    await flushPreflight();
    component.form.controls.password.setValue('correct-password');

    const submitPromise = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    const contextsRequest = http.expectOne('/api/v1/auth/contexts');
    contextsRequest.flush({
      contexts: [
        { contextId: 'context-a', kind: 'OrdinaryMembership', tenantId: 'tenant-a', displayName: 'A', eligibilityVersion: 1 },
        { contextId: 'context-b', kind: 'OrdinaryMembership', tenantId: 'tenant-b', displayName: 'B', eligibilityVersion: 1 },
      ],
    });
    await submitPromise;

    expect(router.navigate).toHaveBeenCalledWith(['/tenant/select']);
  });

  it('navigates to the safe no-context route when zero contexts are returned', async () => {
    fixture.detectChanges();
    await flushPreflight();
    component.form.controls.password.setValue('correct-password');

    const submitPromise = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({ contexts: [] });
    await submitPromise;

    expect(router.navigate).toHaveBeenCalledWith(['/app']);
  });

  it('renders the Arabic translation and RTL direction after toggling language', async () => {
    fixture.detectChanges();
    await flushPreflight();
    fixture.detectChanges();

    component.language.toggle();
    fixture.detectChanges();

    expect(document.documentElement.dir).toBe('rtl');
    expect(component.language.text('signIn')).toBe('تسجيل الدخول');
  });

  it('renders the approved transparent 4:3 brand logo on the sign-in surface', async () => {
    fixture.detectChanges();
    await flushPreflight();
    fixture.detectChanges();

    const logo = fixture.nativeElement.querySelector('.brand-logo') as HTMLImageElement;
    expect(logo).toBeTruthy();
    expect(logo.getAttribute('src')).toBe('assets/Logo_4_3_BG_Removed.png');
    expect(logo.getAttribute('src')).not.toContain('logo-horizontal-trimmed.png');
    expect(logo.getAttribute('width')).toBe('1448');
    expect(logo.getAttribute('height')).toBe('1086');
  });
});
