import { Component, Input } from '@angular/core';

export type StatusCardTone = 'neutral' | 'danger' | 'success' | 'accent';

@Component({
  selector: 'app-status-card',
  standalone: true,
  template: `
    <section class="status-card" [class]="'status-card status-card--' + tone" [attr.aria-label]="title">
      <div class="status-card__marker" aria-hidden="true"></div>
      <div>
        <h2>{{ title }}</h2>
        <p>{{ message }}</p>
      </div>
    </section>
  `,
  styles: `
    :host { display: block; }
    .status-card {
      display: flex;
      gap: 0.9rem;
      align-items: flex-start;
      border: 1px solid var(--line);
      border-radius: 1rem;
      padding: 1rem 1.1rem;
      background: var(--surface-raised);
    }
    .status-card__marker { width: 0.55rem; height: 0.55rem; margin-top: 0.35rem; border-radius: 50%; background: var(--ink-muted); flex: 0 0 auto; }
    .status-card--danger { border-color: color-mix(in srgb, var(--danger) 32%, var(--line)); background: color-mix(in srgb, var(--danger) 6%, var(--surface-raised)); }
    .status-card--danger .status-card__marker { background: var(--danger); }
    .status-card--success .status-card__marker { background: var(--success); }
    .status-card--accent .status-card__marker { background: var(--accent); }
    h2 { margin: 0; font: 650 0.86rem/1.35 var(--font-sans); color: var(--ink); }
    p { margin: 0.25rem 0 0; color: var(--ink-muted); font-size: 0.9rem; line-height: 1.55; }
  `,
})
export class StatusCardComponent {
  @Input() title = '';
  @Input() message = '';
  @Input() tone: StatusCardTone = 'neutral';
}
