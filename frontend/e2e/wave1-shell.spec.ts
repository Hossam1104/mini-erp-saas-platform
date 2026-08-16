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

const sessionWithContext = {
  ...sessionWithoutContext,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

test.describe('Wave 1 shell', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({
      json: { authenticated: false },
    }));
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
    await page.goto('/app/workspaces');
    await expect(page).toHaveURL(/\/app\/workspaces$/);
    await expect(page.getByRole('heading', { name: 'Choose a workspace' })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('#context-switcher-title')).toHaveText('اختر مساحة عمل');

    await page.locator('#workspace-select').selectOption('context-a');
    await expect(page.locator('#context-switcher-title')).toHaveText('Alpha workspace');
    await expect(page.locator('.context-switcher__pill')).toHaveText('عضوية العميل');
  });

  test('keeps the authenticated shell and selected context after an unconfirmed sign-out', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    let signOutAttempts = 0;
    await page.route('**/api/v1/auth/sign-out', (route) => {
      signOutAttempts += 1;
      return route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ code: 'audit_unavailable' }),
      });
    });

    await page.goto('/app');
    await expect(page.locator('#workspace-title')).toHaveText('Alpha workspace');
    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page.getByRole('alert')).toHaveText(
      'Sign-out could not be confirmed. Your session may still be active. Please try again.',
    );
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#workspace-title')).toHaveText('Alpha workspace');
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeEnabled();
    expect(signOutAttempts).toBe(1);
    expect(await page.evaluate(() => ({
      localStorage: localStorage.length,
      sessionStorage: sessionStorage.length,
      cookie: document.cookie,
    }))).toEqual({ localStorage: 0, sessionStorage: 0, cookie: '' });
  });

  test('retries an unconfirmed sign-out and clears protected state after 204', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    let signOutAttempts = 0;
    await page.route('**/api/v1/auth/sign-out', (route) => {
      signOutAttempts += 1;
      if (signOutAttempts === 1) {
        return route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({ code: 'request_failed' }),
        });
      }
      return route.fulfill({ status: 204, body: '' });
    });

    await page.goto('/app');
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
    expect(signOutAttempts).toBe(2);
    await expect(page.locator('#workspace-title')).toHaveCount(0);
  });

  test('treats a server-confirmed 401 as an expired session and returns to login', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    await page.route('**/api/v1/auth/sign-out', (route) => route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'authentication_failed' }),
    }));

    await page.goto('/app');
    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(await page.evaluate(() => ({ localStorage: localStorage.length, sessionStorage: sessionStorage.length })))
      .toEqual({ localStorage: 0, sessionStorage: 0 });
  });
});
