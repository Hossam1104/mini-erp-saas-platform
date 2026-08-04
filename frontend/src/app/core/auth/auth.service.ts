import { HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  ApiClientService,
} from '../api/api-client.service';
import { FoundationSessionResponse } from '../api/foundation.models';
import { SafeUiError, toSafeUiError } from '../api/safe-error';

export type AuthenticationStatus = 'unknown' | 'loading' | 'authenticated' | 'anonymous' | 'expired' | 'error';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClientService);
  private readonly router = inject(Router);
  private sessionRequest: Promise<boolean> | null = null;
  private antiforgeryRequest: Promise<boolean> | null = null;
  private antiforgeryToken: string | null = null;

  readonly session = signal<FoundationSessionResponse | null>(null);
  readonly status = signal<AuthenticationStatus>('unknown');
  readonly lastError = signal<SafeUiError | null>(null);

  async ensureSession(): Promise<boolean> {
    if (this.status() === 'authenticated' && this.session()?.authenticated) {
      return true;
    }
    if (this.sessionRequest) {
      return this.sessionRequest;
    }

    this.status.set('loading');
    this.sessionRequest = firstValueFrom(this.api.get<FoundationSessionResponse>('/auth/session'))
      .then((response) => {
        this.acceptServerSession(response);
        return response.authenticated;
      })
      .catch((error: unknown) => {
        this.markSessionUnavailable(error);
        return false;
      })
      .finally(() => {
        this.sessionRequest = null;
      });

    return this.sessionRequest;
  }

  async signIn(login: string, password: string): Promise<boolean> {
    this.status.set('loading');
    this.lastError.set(null);
    this.session.set(null);
    this.antiforgeryToken = null;

    try {
      const response = await firstValueFrom(
        this.api.post<FoundationSessionResponse>('/auth/sign-in', { login, password }),
      );
      this.acceptServerSession(response);
      return true;
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      this.lastError.set(safeError);
      this.status.set('anonymous');
      return false;
    }
  }

  async bootstrapAntiforgery(): Promise<boolean> {
    if (this.antiforgeryToken) {
      return true;
    }
    if (this.antiforgeryRequest) {
      return this.antiforgeryRequest;
    }

    this.antiforgeryRequest = firstValueFrom(this.api.getResponse<{ status: string }>('/auth/antiforgery'))
      .then((response) => {
        const token = response.headers.get('X-CSRF-TOKEN');
        if (!token) {
          this.lastError.set({ code: 'request_failed', status: 500, correlationId: null });
          return false;
        }
        this.antiforgeryToken = token;
        return true;
      })
      .catch((error: unknown) => {
        this.lastError.set(toSafeUiError(error));
        return false;
      })
      .finally(() => {
        this.antiforgeryRequest = null;
      });

    return this.antiforgeryRequest;
  }

  requestHeaders(): HttpHeaders {
    return this.antiforgeryToken
      ? new HttpHeaders({ 'X-CSRF-TOKEN': this.antiforgeryToken })
      : new HttpHeaders();
  }

  acceptServerSession(response: FoundationSessionResponse): void {
    if (!response.authenticated) {
      this.session.set(null);
      this.status.set('anonymous');
      return;
    }
    this.session.set(response);
    this.status.set('authenticated');
    this.lastError.set(null);
  }

  markSessionExpired(): void {
    this.session.set(null);
    this.antiforgeryToken = null;
    this.lastError.set({ code: 'authentication_failed', status: 401, correlationId: null });
    this.status.set('expired');
    void this.router.navigate(['/login']);
  }

  async signOut(): Promise<void> {
    try {
      if (this.session()) {
        await this.bootstrapAntiforgery();
        await firstValueFrom(this.api.post<void>('/auth/sign-out', {}, { headers: this.requestHeaders() }));
      }
    } catch (error: unknown) {
      this.lastError.set(toSafeUiError(error));
    } finally {
      this.session.set(null);
      this.antiforgeryToken = null;
      this.status.set('anonymous');
      await this.router.navigate(['/login']);
    }
  }

  private markSessionUnavailable(error: unknown): void {
    const safeError = toSafeUiError(error);
    this.lastError.set(safeError);
    this.session.set(null);
    this.antiforgeryToken = null;
    this.status.set(safeError.code === 'authentication_failed' ? 'anonymous' : 'error');
  }
}
