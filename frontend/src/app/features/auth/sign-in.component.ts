import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';

@Component({
  selector: 'app-sign-in',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, StatusCardComponent],
  template: `
    <main class="auth-page">
      <div class="auth-page__texture" aria-hidden="true"></div>
      <section class="auth-card" aria-labelledby="sign-in-title">
        <div class="auth-card__brand">
          <span class="brand-mark" aria-hidden="true">M</span>
          <div>
            <p class="eyebrow">{{ language.text('appKicker') }}</p>
            <p class="brand-name">{{ language.text('appName') }}</p>
          </div>
        </div>
        <div class="auth-card__heading">
          <p class="eyebrow">Release 1 · B2B ERP</p>
          <h1 id="sign-in-title">{{ language.text('signInTitle') }}</h1>
          <p>{{ language.text('signInLead') }}</p>
        </div>

        @if (errorMessage()) {
          <app-status-card [title]="language.text('error')" [message]="errorMessage()" tone="danger" />
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="login">{{ language.text('login') }}</label>
            <input id="login" type="text" autocomplete="username" formControlName="login" />
            @if (form.controls.login.touched && form.controls.login.invalid) {
              <span class="field-error" role="alert">{{ language.text('validationError') }}</span>
            }
          </div>
          <div class="field">
            <label for="password">{{ language.text('password') }}</label>
            <input id="password" type="password" autocomplete="current-password" formControlName="password" />
            @if (form.controls.password.touched && form.controls.password.invalid) {
              <span class="field-error" role="alert">{{ language.text('validationError') }}</span>
            }
          </div>
          <button class="primary-button" type="submit" [disabled]="busy()">
            {{ busy() ? language.text('signingIn') : language.text('signIn') }}
          </button>
        </form>

        <div class="auth-card__foot">
          <button class="language-button" type="button" (click)="language.toggle()">
            <span aria-hidden="true">◐</span>
            {{ language.language() === 'en' ? language.text('switchToArabic') : language.text('switchToEnglish') }}
          </button>
          <a routerLink="/tenant/select" class="quiet-link">{{ language.text('chooseWorkspace') }}</a>
        </div>
      </section>
    </main>
  `,
  styles: `
    :host { display: block; min-height: 100dvh; }
    .auth-page { min-height: 100dvh; display: grid; place-items: center; position: relative; padding: 2rem 1rem; overflow: hidden; background: var(--canvas); }
    .auth-page__texture { position: absolute; inset: 0; opacity: 0.42; background: radial-gradient(circle at 8% 18%, color-mix(in srgb, var(--accent) 18%, transparent), transparent 32%), linear-gradient(115deg, transparent 0 55%, color-mix(in srgb, var(--ink) 3%, transparent) 55% 56%, transparent 56%); pointer-events: none; }
    .auth-card { position: relative; width: min(100%, 29rem); display: grid; gap: 1.6rem; padding: clamp(1.5rem, 4vw, 2.5rem); border: 1px solid var(--line); border-radius: 1.4rem; background: color-mix(in srgb, var(--surface-raised) 94%, transparent); box-shadow: var(--shadow-card); }
    .auth-card__brand, .auth-card__foot { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .auth-card__brand { justify-content: flex-start; }
    .brand-mark { display: grid; place-items: center; width: 2.6rem; height: 2.6rem; border-radius: 0.85rem; color: #fff; background: var(--ink); font: 750 1.25rem/1 var(--font-display); }
    .brand-name { margin: 0; color: var(--ink); font-size: 0.9rem; font-weight: 750; }
    .eyebrow { margin: 0; color: var(--ink-muted); font-size: 0.7rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    .auth-card__heading h1 { margin: 0.45rem 0 0.55rem; color: var(--ink); font: 700 clamp(1.7rem, 4vw, 2.1rem)/1.12 var(--font-display); letter-spacing: -0.035em; }
    .auth-card__heading p:last-child { margin: 0; color: var(--ink-muted); line-height: 1.6; }
    form { display: grid; gap: 1.1rem; }
    .field { display: grid; gap: 0.45rem; }
    label { color: var(--ink); font-size: 0.82rem; font-weight: 700; }
    input { width: 100%; box-sizing: border-box; border: 1px solid var(--line-strong); border-radius: 0.75rem; padding: 0.8rem 0.9rem; color: var(--ink); background: var(--surface); font: inherit; }
    input:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; border-color: var(--accent); }
    .field-error { color: var(--danger); font-size: 0.76rem; }
    .primary-button { border: 0; border-radius: 0.75rem; padding: 0.85rem 1rem; color: #fff; background: var(--ink); font: 750 0.9rem/1 var(--font-sans); cursor: pointer; transition: transform 140ms ease, background 140ms ease; }
    .primary-button:hover:not(:disabled) { transform: translateY(-1px); background: var(--ink-strong); }
    .primary-button:disabled { cursor: wait; opacity: 0.56; }
    .language-button, .quiet-link { color: var(--ink-muted); font-size: 0.8rem; }
    .language-button { border: 0; padding: 0; background: transparent; cursor: pointer; font: 700 0.8rem/1 var(--font-sans); }
    .language-button:hover, .quiet-link:hover { color: var(--accent-strong); }
    .quiet-link { text-decoration: none; }
  `,
})
export class SignInComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly context = inject(ContextService);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly busy = signal(false);
  readonly form = this.formBuilder.nonNullable.group({
    login: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  errorMessage(): string {
    const error = this.auth.lastError();
    if (!error) return '';
    if (this.auth.status() === 'expired') return this.language.text('sessionExpired');
    if (error.code === 'authentication_failed') return this.language.text('signInError');
    if (error.code === 'network_error') return this.language.text('networkError');
    return this.language.text('requestError');
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const signedIn = await this.auth.signIn(this.form.controls.login.value, this.form.controls.password.value);
    if (signedIn) {
      const contexts = await this.context.load();
      await this.router.navigate([this.auth.session()?.selectedContextId || contexts.length === 0 ? '/app' : '/tenant/select']);
    }
    this.busy.set(false);
  }
}
