import { Component, inject } from '@angular/core';
import { ContextSwitcherComponent } from '../../shared/ui/context-switcher.component';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-tenant-select',
  standalone: true,
  imports: [ContextSwitcherComponent],
  template: `
    <section class="tenant-select" aria-labelledby="tenant-select-title">
      <p class="eyebrow">{{ language.text('switchContext') }}</p>
      <h1 id="tenant-select-title">{{ language.text('contextTitle') }}</h1>
      <p class="lede">{{ language.text('contextLead') }}</p>
      <app-context-switcher />
    </section>
  `,
  styles: `
    :host { display: block; }
    .tenant-select { display: grid; gap: 1rem; max-width: 42rem; }
    .eyebrow { margin: 0; color: var(--accent-strong); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    h1 { margin: 0; color: var(--ink); font: 750 clamp(1.9rem, 4vw, 2.8rem)/1.05 var(--font-display); letter-spacing: -0.045em; }
    .lede { margin: 0 0 0.75rem; color: var(--ink-muted); line-height: 1.65; }
  `,
})
export class TenantSelectComponent {
  readonly language = inject(LanguageService);
}
