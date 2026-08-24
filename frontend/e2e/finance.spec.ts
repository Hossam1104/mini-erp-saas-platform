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
  await page.route('**/api/v1/finance/accounts**', (route) => route.fulfill({ json: [{ id: 'account-a', companyId: 'company-a', code: '1000', englishName: 'Cash', arabicName: null, parentAccountId: null, accountType: 'Asset', isPostingAccount: true, lifecycle: 'Active', effectiveFrom: '2026-01-01', effectiveTo: null, version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars**', (route) => route.fulfill({ json: [{ id: 'calendar-a', companyId: 'company-a', name: 'FY 2026', functionalCurrencyCode: 'SAR', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars/calendar-a/years', (route) => route.fulfill({ json: [{ id: 'year-a', calendarId: 'calendar-a', yearNumber: 2026, startDate: '2026-01-01', endDate: '2026-12-31', state: 'Open' }] }));
  await page.route('**/api/v1/finance/years/year-a/periods', (route) => route.fulfill({ json: [{ id: 'period-a', fiscalYearId: 'year-a', sequence: 1, code: '2026-01', englishName: 'January', arabicName: null, startDate: '2026-01-01', endDate: '2026-01-31', state: 'Open', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/posting-rules**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/journals**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/gl**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/inventory-handoffs**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/ap/open-items**', (route) => route.fulfill({ json: [{ id: 'open-item-a', companyId: 'company-a', kind: 'Payable', partyId: 'supplier-a', reference: 'PI-1001', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 1250, allocatedAmount: 0, outstandingAmount: 1250, sourceContract: 'procurement-supplier-invoice.v1', sourceIdentity: 'match-a', recognitionState: 'Recognized', status: 'Open', recognitionJournalId: 'journal-a', version: 'AQ==' }] }));
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

test('settlements workspace presents configured methods and on-account reconciliation', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/settlements');

  const settlements = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(settlements).toBeVisible();
  await expect(settlements.getByRole('heading', { level: 1 })).toContainText('Payments, receipts, and allocation');
  await expect(settlements).toContainText('Payment Method');
  await expect(settlements).toContainText('Settlement');
});
