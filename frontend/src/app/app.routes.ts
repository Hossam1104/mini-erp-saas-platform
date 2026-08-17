import { Routes } from '@angular/router';
import { sessionGuard } from './core/auth/session.guard';
import { SignInComponent } from './features/auth/sign-in.component';
import { TenantSelectComponent } from './features/context/tenant-select.component';
import { ApplicationShellComponent } from './features/shell/application-shell.component';
import { WorkspaceHomeComponent } from './features/workspace/workspace-home.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  { path: 'login', component: SignInComponent },
  {
    path: 'app',
    component: ApplicationShellComponent,
    canActivate: [sessionGuard],
    children: [
      { path: '', component: WorkspaceHomeComponent },
      { path: 'workspaces', component: TenantSelectComponent },
      { path: 'master-data', pathMatch: 'full', redirectTo: 'master-data/categories' },
      { path: 'master-data/imports', loadComponent: () => import('./features/master-data/master-data-import-workspace.component').then((module) => module.MasterDataImportWorkspaceComponent) },
      { path: 'master-data/imports/:id', loadComponent: () => import('./features/master-data/master-data-import-workspace.component').then((module) => module.MasterDataImportWorkspaceComponent) },
      { path: 'master-data/:resource', loadComponent: () => import('./features/master-data/master-data-workspace.component').then((module) => module.MasterDataWorkspaceComponent) },
      { path: 'master-data/:resource/:id', loadComponent: () => import('./features/master-data/master-data-workspace.component').then((module) => module.MasterDataWorkspaceComponent) },
      { path: 'price-lists', loadComponent: () => import('./features/master-data/price-list-workspace.component').then((module) => module.PriceListWorkspaceComponent) },
      { path: 'price-lists/:id', loadComponent: () => import('./features/master-data/price-list-workspace.component').then((module) => module.PriceListWorkspaceComponent) },
      { path: 'procurement/purchase-requests', loadComponent: () => import('./features/procurement/purchase-request-workspace.component').then((module) => module.PurchaseRequestWorkspaceComponent) },
      { path: 'procurement/purchase-requests/new', loadComponent: () => import('./features/procurement/purchase-request-workspace.component').then((module) => module.PurchaseRequestWorkspaceComponent) },
      { path: 'procurement/purchase-requests/:id', loadComponent: () => import('./features/procurement/purchase-request-workspace.component').then((module) => module.PurchaseRequestWorkspaceComponent) },
      { path: 'procurement/purchase-requests/:id/edit', loadComponent: () => import('./features/procurement/purchase-request-workspace.component').then((module) => module.PurchaseRequestWorkspaceComponent) },
      { path: 'procurement/supplier-quotations', loadComponent: () => import('./features/procurement/supplier-quotation-workspace.component').then((module) => module.SupplierQuotationWorkspaceComponent) },
      { path: 'procurement/supplier-quotations/new', loadComponent: () => import('./features/procurement/supplier-quotation-workspace.component').then((module) => module.SupplierQuotationWorkspaceComponent) },
      { path: 'procurement/supplier-quotations/:id', loadComponent: () => import('./features/procurement/supplier-quotation-workspace.component').then((module) => module.SupplierQuotationWorkspaceComponent) },
      { path: 'procurement/supplier-quotations/:id/edit', loadComponent: () => import('./features/procurement/supplier-quotation-workspace.component').then((module) => module.SupplierQuotationWorkspaceComponent) },
    ],
  },
  { path: 'tenant/select', pathMatch: 'full', redirectTo: 'app/workspaces' },
  { path: '**', redirectTo: 'app' },
];
