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
