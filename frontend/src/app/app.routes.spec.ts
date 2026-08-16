import { TenantSelectComponent } from './features/context/tenant-select.component';
import { ApplicationShellComponent } from './features/shell/application-shell.component';
import { routes } from './app.routes';

describe('Application route contract', () => {
  it('keeps workspace selection inside the authenticated shell', () => {
    const appRoute = routes.find((route) => route.path === 'app');
    const workspaceRoute = appRoute?.children?.find((route) => route.path === 'workspaces');
    const compatibilityRoute = routes.find((route) => route.path === 'tenant/select');

    expect(appRoute?.component).toBe(ApplicationShellComponent);
    expect(workspaceRoute?.component).toBe(TenantSelectComponent);
    expect(compatibilityRoute?.redirectTo).toBe('app/workspaces');
    expect(compatibilityRoute?.canActivate).toBeUndefined();
  });

  it('exposes the bounded B2 navigation surfaces without opening future procurement pages', () => {
    const appRoute = routes.find((route) => route.path === 'app');
    const childPaths = (appRoute?.children ?? []).map((route) => route.path);

    expect(childPaths).toContain('');
    expect(childPaths).toContain('workspaces');
    expect(childPaths).toContain('master-data/imports');
    expect(childPaths).toContain('price-lists');
    expect(childPaths).toContain('procurement/purchase-requests');
    expect(childPaths).not.toContain('procurement/purchase-orders');
  });
});
