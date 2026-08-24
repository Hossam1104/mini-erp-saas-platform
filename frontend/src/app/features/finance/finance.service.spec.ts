import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { FinanceService } from './finance.service';

describe('FinanceService', () => {
  let service: FinanceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(FinanceService);
    httpMock = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(AuthService), 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads the Company-scoped foundation and bounded GL evidence routes', () => {
    service.companies().subscribe((companies) => expect(companies).toEqual([]));
    const companies = httpMock.expectOne('/api/v1/finance/companies');
    expect(companies.request.method).toBe('GET');
    companies.flush([]);

    service.accounts('company-a').subscribe();
    const accounts = httpMock.expectOne('/api/v1/finance/accounts?companyId=company-a');
    expect(accounts.request.method).toBe('GET');
    accounts.flush([]);

    service.gl('company-a').subscribe((lines) => expect(lines).toEqual([]));
    const gl = httpMock.expectOne('/api/v1/finance/gl?companyId=company-a');
    expect(gl.request.method).toBe('GET');
    gl.flush([]);

    service.handoffs('company-a').subscribe((handoffs) => expect(handoffs).toEqual([]));
    const handoffs = httpMock.expectOne('/api/v1/finance/inventory-handoffs?companyId=company-a');
    expect(handoffs.request.method).toBe('GET');
    handoffs.flush([]);
  });

  it('resolves fiscal periods through the server-selected calendar and year', () => {
    service.years('calendar-a').subscribe((years) => expect(years).toEqual([]));
    const years = httpMock.expectOne('/api/v1/finance/calendars/calendar-a/years');
    expect(years.request.method).toBe('GET');
    years.flush([]);

    service.periods('year-a').subscribe((periods) => expect(periods).toEqual([]));
    const periods = httpMock.expectOne('/api/v1/finance/years/year-a/periods');
    expect(periods.request.method).toBe('GET');
    periods.flush([]);
  });
});
