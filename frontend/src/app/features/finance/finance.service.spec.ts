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

  it('submits manual journals without source-owned authority fields', async () => {
    const payload = {
      companyId: 'company-a',
      journalDate: '2026-08-24',
      postingDate: '2026-08-24',
      transactionCurrencyCode: null,
      exchangeRate: null,
      description: 'Manual journal',
      lines: [
        { accountId: 'debit-account', debit: 10, credit: 0, costCenterId: null, description: null },
        { accountId: 'credit-account', debit: 0, credit: 10, costCenterId: null, description: null },
      ],
    };
    const promise = service.createJournal(payload);
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    const request = httpMock.expectOne('/api/v1/finance/journals');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    expect(Object.keys(request.request.body).sort()).toEqual([
      'companyId',
      'description',
      'exchangeRate',
      'journalDate',
      'lines',
      'postingDate',
      'transactionCurrencyCode',
    ]);
    expect(request.request.body.sourceContract).toBeUndefined();
    expect(request.request.body.sourceEvent).toBeUndefined();
    expect(request.request.body.sourceEvidenceId).toBeUndefined();
    expect(request.request.body.sourceEvidenceVersion).toBeUndefined();
    expect(request.request.body.postingRuleId).toBeUndefined();
    request.flush({});
    await promise;
  });
});
