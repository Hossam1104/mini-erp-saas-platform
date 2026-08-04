import { test, expect } from '@playwright/test';

const sessionWithoutContext = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 0,
};

test.describe('Wave 1 shell', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithoutContext }));
    await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({
      json: {
        contexts: [{
          contextId: 'context-a',
          kind: 'OrdinaryMembership',
          tenantId: 'tenant-a',
          displayName: 'Alpha workspace',
          eligibilityVersion: 3,
        }],
      },
    }));
    await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({
      headers: { 'X-CSRF-TOKEN': 'playwright-token' },
      json: { status: 'issued' },
    }));
    await page.route('**/api/v1/auth/context-switch', (route) => route.fulfill({
      json: {
        ...sessionWithoutContext,
        selectedPath: 'OrdinaryMembership',
        selectedTenantId: 'tenant-a',
        selectedContextId: 'context-a',
        selectionVersion: 1,
      },
    }));
  });

  test('bootstraps navigation, switches language direction, and adopts server-confirmed context', async ({ page }) => {
    await page.goto('/app');
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.getByRole('heading', { name: 'Choose a workspace' })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('#workspace-title')).toHaveText('اختر مساحة عمل');

    await page.locator('#workspace-select').selectOption('context-a');
    await expect(page.locator('#workspace-title')).toHaveText('Alpha workspace');
    await expect(page.locator('.context-switcher__pill')).toHaveText('عضوية العميل');
  });
});
