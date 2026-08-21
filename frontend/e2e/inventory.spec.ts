import { test, expect } from '@playwright/test';

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

test('Inventory workspace renders server-provided scope and availability', async ({ page }) => {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({
    json: {
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
    },
  }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/master-data/products', (route) => route.fulfill({ json: [{ id: 'product-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', sku: 'SKU-A', englishName: 'Product A', arabicName: null, description: null, categoryId: 'category-a', baseUnitOfMeasureId: 'unit-a', trackingDefaultEnabled: false, trackingEnabledOverride: false, trackingEnabled: false, isSellable: true, isPurchasable: true, isInventoryRelevant: true, barcodes: [] }] }));
  await page.route('**/api/v1/master-data/units-of-measure', (route) => route.fulfill({ json: [{ id: 'unit-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'EA', englishName: 'Each', arabicName: null }] }));
  await page.route('**/api/v1/inventory/ledger**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/opening-balances**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/reservations**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/availability**', (route) => route.fulfill({ json: { onHandQuantity: 12, reservedQuantity: 3, availableQuantity: 9, unitOfMeasureCode: 'EA' } }));

  await page.goto('/app/inventory');

  await expect(page.locator('[data-testid="inventory-workspace"]')).toBeVisible();
  await expect(page.locator('h1')).toHaveText('Stock');
  await expect(page.locator('select').first()).toContainText('WH-A · Main warehouse');
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('9');
});

test('Inventory opening posts to the ledger and reservation release restores availability', async ({ page }) => {
  let openingStatus = 'Draft';
  let openingVersion = 'AQ==';
  let openingRequestBody: { sourceReference?: string; rows?: Array<{ sourceLineReference?: string }> } | undefined;
  let posted = false;
  let reservationStatus = 'Active';
  let reservationVersion = 'BA==';
  let reserved = 0;

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: { entryMode: 'TenantHost', canonicalHost: '127.0.0.1', candidateTenantId: 'tenant-a', candidateTenantDisplayName: 'Alpha Tenant', authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }], operationalContexts: [{ contextId: 'context-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }], selectedOperationalContextId: 'context-a', operationalSelectionVersion: 1, branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true }, currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null } }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ status: 204, headers: { 'X-CSRF-TOKEN': 'inventory-e2e-token' } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/master-data/products', (route) => route.fulfill({ json: [{ id: 'product-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', sku: 'SKU-A', englishName: 'Product A', arabicName: null, description: null, categoryId: 'category-a', baseUnitOfMeasureId: 'unit-a', trackingDefaultEnabled: false, trackingEnabledOverride: false, trackingEnabled: false, isSellable: true, isPurchasable: true, isInventoryRelevant: true, barcodes: [] }] }));
  await page.route('**/api/v1/master-data/units-of-measure', (route) => route.fulfill({ json: [{ id: 'unit-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'EA', englishName: 'Each', arabicName: null }] }));
  await page.route('**/api/v1/inventory/opening-balances**', async (route) => {
    if (route.request().method() === 'POST') {
      openingRequestBody = route.request().postDataJSON() as { sourceReference?: string; rows?: Array<{ sourceLineReference?: string }> };
      openingStatus = 'Draft';
      openingVersion = 'AQ==';
      return route.fulfill({ json: { id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: 5, rows: [] } });
    }
    return route.fulfill({ json: [{ id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: posted ? 5 : 0, rows: [] }] });
  });
  await page.route('**/api/v1/inventory/opening-balances/opening-a/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() === 'POST' && path.endsWith('/validate')) {
      openingStatus = 'Validated';
      openingVersion = 'Ag==';
    } else if (route.request().method() === 'POST' && path.endsWith('/post')) {
      openingStatus = 'Posted';
      openingVersion = 'Aw==';
      posted = true;
    }
    return route.fulfill({ json: { id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: posted ? 5 : 5, rows: [] } });
  });
  await page.route('**/api/v1/inventory/reservations**', async (route) => {
    if (route.request().method() === 'POST') {
      reserved = 2;
      reservationStatus = 'Active';
      return route.fulfill({ json: { id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: reserved, unallocatedQuantity: 0 } });
    }
    return route.fulfill({ json: reserved > 0 || reservationStatus === 'Released' ? [{ id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: reserved, unallocatedQuantity: reservationStatus === 'Released' ? 2 : 0 }] : [] });
  });
  await page.route('**/api/v1/inventory/reservations/reservation-a/release', async (route) => {
    reservationStatus = 'Released';
    reservationVersion = 'BQ==';
    reserved = 0;
    return route.fulfill({ json: { id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: 0, unallocatedQuantity: 2 } });
  });
  await page.route('**/api/v1/inventory/ledger**', (route) => route.fulfill({ json: posted ? [{ id: 'movement-a', productSku: 'SKU-A', productName: 'Product A', sourceType: 'OpeningBalance', direction: 'Inbound', quantity: 5, effectiveDate: '2026-08-21' }] : [] }));
  await page.route('**/api/v1/inventory/availability**', (route) => route.fulfill({ json: { onHandQuantity: posted ? 5 : 0, reservedQuantity: reserved, availableQuantity: (posted ? 5 : 0) - reserved, expectedQuantity: 0, damagedQuantity: 0, inTransitQuantity: 0, unitOfMeasureCode: 'EA' } }));

  await page.goto('/app/inventory');
  await expect(page.locator('[data-testid="inventory-workspace"]')).toBeVisible();

  const openingForm = page.locator('form').first();
  await openingForm.locator('input[name="quantity"]').fill('5');
  await openingForm.locator('input[name="sourceReference"]').fill('OPENING-1');
  await openingForm.getByRole('button').click();
  await expect.poll(() => openingRequestBody).toMatchObject({ sourceReference: 'OPENING-1', rows: [{ sourceLineReference: 'OPENING-1' }] });
  expect(JSON.stringify(openingRequestBody)).not.toContain('line-1');
  await expect(page.getByRole('button', { name: 'Validate' })).toBeVisible();
  await page.getByRole('button', { name: 'Validate' }).click();
  await expect(page.getByRole('button', { name: 'Post movement' })).toBeVisible();
  await page.getByRole('button', { name: 'Post movement' }).click();
  await expect(page.locator('.ui-grid tbody tr').first()).toContainText('SKU-A');
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('5');

  const reservationForm = page.locator('form').nth(1);
  await reservationForm.locator('input[name="requestedQuantity"]').fill('2');
  await reservationForm.locator('input[name="sourceReference"]').fill('DEMAND-1');
  await reservationForm.getByRole('button').click();
  await expect(page.getByText('DEMAND-1')).toBeVisible();
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('3');
  await page.getByRole('button', { name: 'Release' }).click();
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('5');
});
