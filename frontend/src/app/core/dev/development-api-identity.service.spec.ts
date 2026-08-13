import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DevelopmentApiIdentityService } from './development-api-identity.service';

const validRegistration = {
  module: 'platform-administration',
  name: 'Platform Administration',
  boundary: 'platform',
  registered: true,
  masterData: {
    module: 'master-data-catalog',
    name: 'Master Data Catalog',
    boundary: 'master-data',
    registered: true,
  },
  businessParties: {
    module: 'business-parties',
    name: 'Business Parties',
    boundary: 'business-parties',
    registered: true,
  },
};

describe('DevelopmentApiIdentityService', () => {
  let service: DevelopmentApiIdentityService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DevelopmentApiIdentityService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DevelopmentApiIdentityService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reports connected when the backend identifies as MiniERP modules', async () => {
    const result = service.check();
    expect(service.status()).toBe('checking');
    http.expectOne('/api/v1/module-registration').flush(validRegistration);

    await expect(result).resolves.toBe(true);
    expect(service.status()).toBe('connected');
  });

  it('reports unavailable when the response identifies a different service', async () => {
    const result = service.check();
    http.expectOne('/api/v1/module-registration').flush({
      ...validRegistration,
      module: 'unrelated-service',
    });

    await expect(result).resolves.toBe(false);
    expect(service.status()).toBe('unavailable');
  });

  it('reports unavailable when the request fails', async () => {
    const result = service.check();
    http.expectOne('/api/v1/module-registration').flush(
      { code: 'request_failed' },
      { status: 502, statusText: 'Bad Gateway' },
    );

    await expect(result).resolves.toBe(false);
    expect(service.status()).toBe('unavailable');
  });
});
