import { test, expect, type Page } from '@playwright/test';

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

async function setupFinanceRoutes(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'finance-playwright-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
    entryMode: 'TenantHost',
    canonicalHost: '127.0.0.1',
    candidateTenantId: 'tenant-a',
    candidateTenantDisplayName: 'Alpha Tenant',
    authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
    operationalContexts: [{ contextId: 'context-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'context-a',
    operationalSelectionVersion: 1,
    branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
    code: null,
  } }));
  await page.route('**/api/v1/finance/companies', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', companyName: 'Alpha Company', functionalCurrencyCode: 'SAR', branchId: null, isActive: true }] }));
  await page.route('**/api/v1/master-data/currencies', (route) => route.fulfill({ json: [
    { id: 'currency-sar', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'SAR', englishName: 'Saudi Riyal', arabicName: null, revision: 1 },
    { id: 'currency-usd', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'USD', englishName: 'US Dollar', arabicName: null, revision: 1 },
  ] }));
  await page.route('**/api/v1/master-data/exchange-rates', (route) => route.fulfill({ json: [{
    id: 'rate-usd-sar', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==',
    sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR',
    currentVersionNumber: 3, versions: [{ id: 'rate-version-3', versionNumber: 3, effectiveFrom: '2026-01-01', effectiveTo: null, rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 configured reference', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR' }],
  }] }));
  await page.route('**/api/v1/master-data/exchange-rates/rate-usd-sar/reference?effectiveOn=*', (route) => {
    const effectiveOn = new URL(route.request().url()).searchParams.get('effectiveOn') ?? '2026-08-25';
    return route.fulfill({ json: {
      id: 'rate-usd-sar', tenantId: 'tenant-a', sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR',
      lifecycleState: 'Active', versionNumber: 3, versionId: 'rate-version-3', effectiveOn, effectiveFrom: '2026-01-01', effectiveTo: null,
      rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 configured reference', referenceValue: '3.750000', version: 'AQ==',
    } });
  });
  await page.route('**/api/v1/master-data/customers', (route) => route.fulfill({ json: [{ id: 'customer-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'CUS-1', englishLegalName: 'Customer One', arabicLegalName: null, englishTradingName: null, arabicTradingName: null, contacts: [] }] }));
  await page.route('**/api/v1/master-data/payment-terms', (route) => route.fulfill({ json: [{
    id: 'term-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'NET30', englishName: 'Net 30', arabicName: null, currentVersionNumber: 1,
    versions: [{ id: 'term-version-a', versionNumber: 1, effectiveFrom: '2026-01-01', effectiveTo: null, baseDateRule: 'DocumentDate', scheduleMode: 'SingleDueDate', dueOffsetDays: 30, dueOffsetMonths: 0, installments: [], earlySettlementDiscountEnabled: false, earlySettlementDiscountPercentage: null, earlySettlementDiscountDays: 0, earlySettlementDiscountMonths: 0, code: 'NET30', englishName: 'Net 30', arabicName: null }],
  }] }));
  await page.route('**/api/v1/finance/accounts**', (route) => route.fulfill({ json: [{ id: 'account-a', companyId: 'company-a', code: '1000', englishName: 'Cash', arabicName: null, parentAccountId: null, accountType: 'Asset', isPostingAccount: true, lifecycle: 'Active', effectiveFrom: '2026-01-01', effectiveTo: null, version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars**', (route) => route.fulfill({ json: [{ id: 'calendar-a', companyId: 'company-a', name: 'FY 2026', functionalCurrencyCode: 'SAR', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars/calendar-a/years', (route) => route.fulfill({ json: [{ id: 'year-a', calendarId: 'calendar-a', yearNumber: 2026, startDate: '2026-01-01', endDate: '2026-12-31', state: 'Open' }] }));
  await page.route('**/api/v1/finance/years/year-a/periods', (route) => route.fulfill({ json: [{ id: 'period-a', fiscalYearId: 'year-a', sequence: 1, code: '2026-01', englishName: 'January', arabicName: null, startDate: '2026-01-01', endDate: '2026-01-31', state: 'Open', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/posting-rules**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/journals**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/gl**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/inventory-handoffs**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/ap/open-items**', (route) => route.fulfill({ json: [{ id: 'open-item-a', companyId: 'company-a', kind: 'Payable', partyId: 'supplier-a', reference: 'PI-1001', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 1250, allocatedAmount: 0, outstandingAmount: 1250, sourceContract: 'procurement-supplier-invoice.v1', sourceIdentity: 'match-a', recognitionState: 'Recognized', status: 'Open', recognitionJournalId: 'journal-a', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/ap/source-ready**', (route) => route.fulfill({ json: [{ sourceEvidenceId: 'source-a', companyId: 'company-a', supplierId: 'supplier-a', supplierCode: 'SUP-1', supplierName: 'Supplier One', supplierInvoiceReference: 'PI-READY-1', invoiceDate: '2026-08-01', currencyCode: 'SAR', amount: 1250, dueDate: '2026-08-31', paymentTerm: { code: 'NET30', englishName: 'Net 30', arabicName: null, versionNumber: 1, dueDate: '2026-08-31' }, matchResult: 'ExactMatch', alreadyRecognized: false, sourceEvidenceVersion: 1 }] }));
  await page.route('**/api/v1/finance/ap/recognize', async (route) => { expect(route.request().method()).toBe('POST'); await route.fulfill({ json: { id: 'open-item-a', companyId: 'company-a', kind: 'Payable', reference: 'PI-READY-1', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 1250, allocatedAmount: 0, outstandingAmount: 1250, status: 'Open', recognitionState: 'Recognized', recognitionJournalId: 'journal-a', version: 'AQ==' } }); });
  await page.route('**/api/v1/finance/ap/aging**', (route) => route.fulfill({ json: [{ openItemId: 'open-item-a', reference: 'PI-1001', dueDate: '2026-08-31', daysOverdue: 0, outstandingAmount: 1250, currencyCode: 'SAR', status: 'Open' }] }));
  await page.route('**/api/v1/finance/ar/open-items**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/ar/aging**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/payment-methods**', (route) => route.fulfill({ json: [{ id: 'method-a', companyId: 'company-a', code: 'BANK', name: 'Bank transfer', direction: 'Both', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/cash-accounts**', (route) => route.fulfill({ json: [{ id: 'cash-a', companyId: 'company-a', code: '1000', name: 'Main bank', kind: 'Bank', currencyCode: 'SAR', linkedAccountCode: '1000', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/payments**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/receipts**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/allocations**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/settlement/reconciliation**', (route) => route.fulfill({ json: [{ scope: 'Settlement', subledgerAmount: 0, postedJournalAmount: 0, difference: 0, status: 'Reconciled' }] }));
}

test('Finance workspace renders Company books, periods, and bounded GL evidence', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance');

  const finance = page.locator('[data-testid="finance-workspace"]');
  await expect(finance).toBeVisible();
  await expect(finance.getByRole('heading', { level: 1 })).toContainText('Company books');
  await expect(finance.locator('select')).toContainText('Alpha Company');
  await expect(finance.getByRole('tab', { name: 'Chart of accounts' })).toBeVisible();
  await expect(finance.getByRole('tab', { name: 'GL inquiry' })).toBeVisible();
  await finance.getByRole('tab', { name: 'Chart of accounts' }).click();
  await expect(finance).toContainText('Cash');
});

test('Finance workspace changes the authenticated shell to RTL for Arabic', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance');
  await expect(page.locator('[data-testid="finance-workspace"]')).toBeVisible();
  await page.locator('.language-button').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
});

test('AP workspace renders source lineage and aging evidence', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/ap');

  const payable = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(payable).toBeVisible();
  await expect(payable.getByRole('heading', { level: 1 })).toContainText('Accounts Payable');
  await expect(payable).toContainText('PI-1001');
  await expect(payable).toContainText('procurement-supplier-invoice.v1');
  await expect(payable).toContainText('Aging');
});

test('AP workspace executes the bounded source-ready recognition journey', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/ap');

  const payable = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(payable.locator('[data-testid="ap-source-ready"]')).toContainText('PI-READY-1');
  await payable.getByRole('button', { name: 'Recognize payable' }).click();
  await expect(payable).toContainText('PI-READY-1');
});

test('settlements workspace presents configured methods and on-account reconciliation', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/settlements');

  const settlements = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(settlements).toBeVisible();
  await expect(settlements.getByRole('heading', { level: 1 })).toContainText('Payments, receipts, and allocation');
  await expect(settlements).toContainText('Payment Method');
  await expect(settlements).toContainText('Settlement');
});

test('Manual AR resolves exact MESP-120 non-functional currency evidence before creation', async ({ page }) => {
  await setupFinanceRoutes(page);
  let submittedPayload: Record<string, unknown> | undefined;
  await page.route('**/api/v1/finance/ar/manual', async (route) => {
    submittedPayload = route.request().postDataJSON() as Record<string, unknown>;
    await route.fulfill({ json: {
      id: 'ar-fx-1', companyId: 'company-a', customerId: 'customer-a', kind: 'Receivable', reference: 'AR-FX-1', documentDate: '2026-08-25', dueDate: '2026-09-24', currencyCode: 'USD', originalAmount: 100, allocatedAmount: 0, outstandingAmount: 100, status: 'Open', recognitionState: 'Recognized', recognitionJournalId: 'journal-ar-fx', version: 'AQ==',
    } });
  });

  await page.goto('/app/finance/ar');
  const ar = page.locator('[data-testid="finance-settlement-workspace"]');
  await ar.locator('select').first().selectOption('company-a');
  await ar.locator('[data-testid="ar-customer-select"]').selectOption('customer-a');
  await ar.locator('[data-testid="ar-payment-term-select"]').selectOption('term-a');
  await ar.locator('input').nth(1).fill('USD');
  await ar.locator('input[type="number"]').fill('100');
  await expect(ar.locator('[data-testid="ar-exchange-rate-select"]')).toBeVisible();
  await ar.locator('[data-testid="ar-exchange-rate-select"]').selectOption('rate-usd-sar');
  await expect(ar.locator('[data-testid="ar-fx-reference"]')).toContainText('3.75');
  const createReceivable = ar.getByRole('button', { name: 'Create manual receivable' });
  await expect(createReceivable).toBeEnabled();
  await createReceivable.click();
  await expect.poll(() => submittedPayload).toBeDefined();

  expect(submittedPayload).toMatchObject({
    companyId: 'company-a', customerId: 'customer-a', paymentTermId: 'term-a', currencyCode: 'USD', amount: 100,
    exchangeRate: 3.75, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3,
  });
});
