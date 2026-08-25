import { Routes } from '@angular/router';

export const financeRoutes: Routes = [
  { path: '', loadComponent: () => import('./finance-workspace.component').then((module) => module.FinanceWorkspaceComponent) },
  { path: 'ap', loadComponent: () => import('./finance-settlement-workspace.component').then((module) => module.FinanceSettlementWorkspaceComponent) },
  { path: 'ar', loadComponent: () => import('./finance-settlement-workspace.component').then((module) => module.FinanceSettlementWorkspaceComponent) },
  { path: 'settlements', loadComponent: () => import('./finance-settlement-workspace.component').then((module) => module.FinanceSettlementWorkspaceComponent) },
  { path: 'tax-fx', loadComponent: () => import('./finance-tax-fx-workspace.component').then((module) => module.FinanceTaxFxWorkspaceComponent) },
  { path: 'revaluation', redirectTo: 'tax-fx' },
];
