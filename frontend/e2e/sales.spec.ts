import { expect, test, type Page } from '@playwright/test';

const session = {
  authenticated: true,
  actorId: 'actor-sales-1',
  sessionId: 'session-sales-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-sales-a',
  selectedContextId: 'context-sales-a',
  selectionVersion: 1,
};

async function setupSalesRoutes(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', route => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/antiforgery', route => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'sales-playwright-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', route => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/entry', route => route.fulfill({ json: {
    entryMode: 'TenantHost', canonicalHost: '127.0.0.1', candidateTenantId: 'tenant-sales-a', candidateTenantDisplayName: 'Sales Tenant',
    authorizedTenants: [{ tenantId: 'tenant-sales-a', displayName: 'Sales Tenant', canonicalHost: 'sales.localhost' }],
    operationalContexts: [{ contextId: 'context-sales-a', kind: 'Company', displayName: 'Sales Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'context-sales-a', operationalSelectionVersion: 1,
    branding: { displayName: 'Sales Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Sales Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null,
  } }));
  await page.route('**/api/v1/sales/quotations', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/sales/orders', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/customers', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/currencies', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/products', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/units', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/price-lists', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/procurement/organization-scopes', route => route.fulfill({ json: [] }));
}

test.describe('MESP-136 Sales workspace', () => {
  test.beforeEach(async ({ page }) => setupSalesRoutes(page));

  test('renders searchable quotation and order registers with empty states', async ({ page }) => {
    await page.goto('/app/sales/quotations');
    await expect(page.locator('h1#sales-title')).toBeVisible();
    await expect(page.getByText('No quotations yet')).toBeVisible();
    await expect(page.locator('[role="search"]')).toBeVisible();

    await page.goto('/app/sales/orders');
    await expect(page.locator('h1#sales-title')).toBeVisible();
    await expect(page.getByText('No Sales Orders yet')).toBeVisible();
  });

  test('keeps quotation scope selection server-configured and omits raw GUID inputs', async ({ page }) => {
    await page.goto('/app/sales/quotations/new');
    await expect(page.locator('select[name="organizationScope"]')).toBeVisible();
    await expect(page.locator('input[name="companyId"]')).toHaveCount(0);
    await expect(page.locator('input[name="branchId"]')).toHaveCount(0);
    await expect(page.getByText('Prices and totals are server-authoritative', { exact: false })).toBeVisible();
  });
});
