import { ApplicationShellComponent } from './features/shell/application-shell.component';
import { routes } from './app.routes';

describe('Application route contract', () => {
  it('keeps workspace selection inside the authenticated shell', () => {
    const appRoute = routes.find((route) => route.path === 'app');
    const workspaceRoute = appRoute?.children?.find((route) => route.path === 'workspaces');
    const compatibilityRoute = routes.find((route) => route.path === 'tenant/select');

    expect(appRoute?.component).toBe(ApplicationShellComponent);
    expect(workspaceRoute?.loadComponent).toBeDefined();
    expect(compatibilityRoute?.redirectTo).toBe('app/workspaces');
    expect(compatibilityRoute?.canActivate).toBeUndefined();
  });

  it('exposes the bounded procurement and Finance foundation navigation surfaces', () => {
    const appRoute = routes.find((route) => route.path === 'app');
    const childPaths = (appRoute?.children ?? []).map((route) => route.path);

    expect(childPaths).toContain('');
    expect(childPaths).toContain('workspaces');
    expect(childPaths).toContain('master-data/imports');
    expect(childPaths).toContain('price-lists');
    expect(childPaths).toContain('procurement/purchase-requests');
    expect(childPaths).toContain('procurement/supplier-quotations');
    expect(childPaths).toContain('procurement/invoice-matching');
    expect(childPaths).toContain('finance');
    expect(childPaths).toContain('procurement/purchase-orders');
    expect(childPaths).not.toContain('inventory/goods-receipts');
    expect(childPaths).not.toContain('finance/accounts-payable');
  });

  it('exposes the bounded Sales quotation and order workspace without fulfillment routes', () => {
    const appRoute = routes.find((route) => route.path === 'app');
    const childPaths = (appRoute?.children ?? []).map((route) => route.path);

    expect(childPaths).toContain('sales');
    expect(childPaths).toContain('sales/quotations');
    expect(childPaths).toContain('sales/quotations/new');
    expect(childPaths).toContain('sales/quotations/:id');
    expect(childPaths).toContain('sales/quotations/:id/edit');
    expect(childPaths).toContain('sales/orders');
    expect(childPaths).toContain('sales/orders/:id');
    expect(childPaths).not.toContain('sales/deliveries');
    expect(childPaths).not.toContain('sales/invoices');
  });
});
