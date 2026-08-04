import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';

@Component({
  selector: 'app-workspace-home',
  standalone: true,
  imports: [StatusCardComponent],
  template: `
    <section class="workspace-home" aria-labelledby="workspace-title">
      <div class="workspace-home__intro">
        <p class="eyebrow">{{ language.text('overview') }}</p>
        <h1 id="workspace-title">{{ contextHeading() }}</h1>
        <p class="lede">{{ language.text('shellWelcome') }}</p>
      </div>

      <div class="signal-grid">
        <article class="signal-card signal-card--accent">
          <span class="signal-card__label">{{ language.text('sessionState') }}</span>
          <strong>{{ language.text('active') }}</strong>
          <small>{{ language.text('shellSafeBoundary') }}</small>
        </article>
        <article class="signal-card">
          <span class="signal-card__label">{{ language.text('currentWorkspace') }}</span>
          <strong>{{ workspaceName() }}</strong>
          <small>{{ pathLabel() }}</small>
        </article>
      </div>

      @if (!auth.session()?.selectedContextId && auth.session()?.selectedPath !== 'PlatformGovernanceContext') {
        <app-status-card [title]="language.text('chooseWorkspace')" [message]="language.text('contextLead')" tone="accent" />
      } @else {
        <app-status-card [title]="language.text('empty')" [message]="language.text('shellNoBusinessData')" tone="neutral" />
      }
    </section>
  `,
  styles: `
    :host { display: block; }
    .workspace-home { display: grid; gap: 2rem; }
    .eyebrow { margin: 0 0 0.55rem; color: var(--accent-strong); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    h1 { max-width: 34rem; margin: 0; color: var(--ink); font: 750 clamp(2rem, 5vw, 3.4rem)/1.02 var(--font-display); letter-spacing: -0.055em; }
    .lede { max-width: 33rem; margin: 1rem 0 0; color: var(--ink-muted); font-size: 1rem; line-height: 1.65; }
    .signal-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
    .signal-card { display: grid; gap: 0.55rem; min-height: 8.5rem; border: 1px solid var(--line); border-radius: 1rem; padding: 1.2rem; background: var(--surface-raised); box-shadow: var(--shadow-soft); }
    .signal-card--accent { border-color: color-mix(in srgb, var(--accent) 45%, var(--line)); background: linear-gradient(135deg, var(--accent-soft), var(--surface-raised) 75%); }
    .signal-card__label { color: var(--ink-muted); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
    .signal-card strong { color: var(--ink); font: 700 1.25rem/1.15 var(--font-display); }
    .signal-card small { color: var(--ink-muted); font-size: 0.8rem; line-height: 1.45; }
    @media (max-width: 560px) { .signal-grid { grid-template-columns: 1fr; } }
  `,
})
export class WorkspaceHomeComponent {
  readonly auth = inject(AuthService);
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  contextHeading(): string {
    return this.context.currentContext()?.displayName
      ?? (this.auth.session()?.selectedPath === 'PlatformGovernanceContext'
        ? this.language.text('platformGovernance')
        : this.language.text('contextTitle'));
  }

  workspaceName(): string {
    return this.context.currentContext()?.displayName
      ?? (this.auth.session()?.selectedPath === 'PlatformGovernanceContext'
        ? this.language.text('platformGovernance')
        : this.language.text('chooseWorkspace'));
  }

  pathLabel(): string {
    const path = this.auth.session()?.selectedPath;
    if (!path) return this.language.text('chooseWorkspace');
    if (path === 'SupportGrant') return this.language.text('supportGrant');
    if (path === 'PlatformGovernanceContext') return this.language.text('platformGovernance');
    return this.language.text('ordinaryMembership');
  }
}
