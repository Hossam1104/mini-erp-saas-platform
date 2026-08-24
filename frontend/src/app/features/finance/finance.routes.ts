import { Routes } from '@angular/router';

export const financeRoutes: Routes = [
  { path: '', loadComponent: () => import('./finance-workspace.component').then((module) => module.FinanceWorkspaceComponent) },
];
