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
    children: [{ path: '', component: WorkspaceHomeComponent }],
  },
  { path: 'tenant/select', component: TenantSelectComponent, canActivate: [sessionGuard] },
  { path: '**', redirectTo: 'app' },
];
