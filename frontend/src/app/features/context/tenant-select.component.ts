import { Component, inject } from '@angular/core';
import { ContextSwitcherComponent } from '../../shared/ui/context-switcher.component';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-tenant-select',
  standalone: true,
  imports: [ContextSwitcherComponent],
  template: `
    <section class="tenant-select ui-page" aria-labelledby="tenant-select-title">
      <header class="ui-page-header ui-page-header--compact">
        <div>
          <p class="eyebrow">{{ language.text('switchContext') }}</p>
          <h1 id="tenant-select-title">{{ language.text('contextTitle') }}</h1>
          <p class="lede">{{ language.text('contextLead') }}</p>
        </div>
        <span class="ui-status-chip ui-status-chip--accent">{{ language.text('serverAuthority') }}</span>
      </header>

      <div class="ui-surface ui-surface--glass workspace-selector">
        <app-context-switcher />
      </div>

      <aside class="ui-technical-reference" [attr.aria-label]="language.text('technicalReference')">
        <span class="ui-technical-reference__label">{{ language.text('technicalReference') }}</span>
        <p>{{ language.text('contextServerReference') }}</p>
      </aside>
    </section>
  `,
  styles: `
    :host { display: block; }
    .tenant-select { display: grid; gap: 1rem; }
    .workspace-selector { max-width: 50rem; }
    .eyebrow { margin: 0; color: var(--accent-strong); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    h1 { margin: 0; color: var(--ink); font: 750 clamp(1.9rem, 4vw, 2.8rem)/1.05 var(--font-display); letter-spacing: -0.045em; }
    .lede { max-width: 42rem; margin: 0.75rem 0 0; color: var(--ink-muted); line-height: 1.65; }
  `,
})
export class TenantSelectComponent {
  readonly language = inject(LanguageService);
}
