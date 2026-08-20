import { expect, test, type Page } from '@playwright/test';

const tenantId = 'tenant-a';
const matchId = '11111111-1111-1111-1111-111111111111';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: tenantId,
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

const match = (result: string) => ({
  id: matchId,
  tenantId,
  companyId: 'company-a',
  branchId: null,
  purchaseInvoiceHandoffId: '22222222-2222-2222-2222-222222222222',
  purchaseOrderId: '33333333-3333-3333-3333-333333333333',
  lifecycle: 'Current',
  result,
  evaluatedAt: '2026-08-20T08:00:00Z',
  evaluatedByActorId: 'actor-1',
  resolvedByActorId: result === 'ResolvedException' ? 'actor-2' : null,
  resolvedAt: result === 'ResolvedException' ? '2026-08-20T09:00:00Z' : null,
  resolutionReason: result === 'ResolvedException' ? 'Supporting evidence reviewed' : null,
  sourceFingerprint: 'fingerprint-12345678',
  purchaseOrderVersion: 'UE8=',
  handoffVersion: 'SFY=',
  declaredEvidenceId: '44444444-4444-4444-4444-444444444444',
  declaredEvidenceVersion: 1,
  policy: {
    policyId: 'exact-safe-default', version: 1,
    quantityAbsoluteTolerance: 0, quantityPercentageTolerance: 0,
    priceAbsoluteTolerance: 0, pricePercentageTolerance: 0,
    amountAbsoluteTolerance: 0, amountPercentageTolerance: 0,
    taxAbsoluteTolerance: 0, taxPercentageTolerance: 0,
    effectiveFrom: '0001-01-01T00:00:00Z', effectiveTo: null,
  },
  resolutionPolicy: null,
  appliedExchangeRate: null,
  variances: result === 'ExceptionHold' ? [{ classification: 'PriceVariance', purchaseOrderLineId: null, goodsReceiptLineId: null, expectedValue: 100, actualValue: 105, variance: 5, allowedTolerance: 0, currencyCode: 'SAR', details: 'Supplier price differs.' }] : [],
  sourceSnapshot: null,
  version: 'V2',
  varianceCount: result === 'ExceptionHold' ? 1 : 0,
});

async function installAuth(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-a', kind: 'OrdinaryMembership', tenantId, displayName: 'Alpha workspace', eligibilityVersion: 3 }] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
    entryMode: 'TenantHost', canonicalHost: '127.0.0.1', candidateTenantId: tenantId,
    candidateTenantDisplayName: 'Alpha Tenant', authorizedTenants: [{ tenantId, displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
    operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'operation-a', operationalSelectionVersion: 1,
    branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null,
  } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'playwright-token' }, json: { status: 'issued' } }));
}

test.describe('Three-way matching workspace', () => {
  test.beforeEach(async ({ page }) => installAuth(page));

  test('renders an exact decision from the canonical matching endpoint and switches to Arabic RTL', async ({ page }) => {
    await page.route('**/api/v1/procurement/purchase-invoice-matches', (route) => route.fulfill({ json: [match('ExactMatch')] }));
    await page.goto('/app/procurement/invoice-matching');
    await expect(page.getByTestId('invoice-matching-list')).toBeVisible();
    await expect(page.getByRole('table').getByText('Exact match')).toBeVisible();
    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  test('shows a price hold and sends an authorized resolution reason', async ({ page }) => {
    let resolved = false;
    await page.route('**/api/v1/procurement/purchase-invoice-matches/**', (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/history')) return route.fulfill({ json: [] });
      if (path.endsWith('/audit')) return route.fulfill({ json: [] });
      if (route.request().method() === 'POST' && path.endsWith('/resolve-exception')) {
        resolved = true;
        return route.fulfill({ json: match('ResolvedException') });
      }
      return route.fulfill({ json: match(resolved ? 'ResolvedException' : 'ExceptionHold') });
    });
    await page.goto(`/app/procurement/invoice-matching/${matchId}`);
    await expect(page.getByRole('heading', { name: 'Exception hold', exact: true })).toBeVisible();
    await expect(page.getByText('PriceVariance')).toBeVisible();
    await page.getByLabel('Resolution reason').fill('Reviewed supporting evidence');
    await page.getByRole('button', { name: 'Resolve exception' }).click();
    await expect(page.getByText('Resolved by authorized review')).toBeVisible();
    expect(resolved).toBe(true);
  });
});
