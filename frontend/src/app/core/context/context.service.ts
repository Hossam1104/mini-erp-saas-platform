import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../api/api-client.service';
import {
  FoundationContextCandidate,
  FoundationContextSwitchRequest,
  FoundationContextsResponse,
  FoundationSessionResponse,
} from '../api/foundation.models';
import { SafeUiError, toSafeUiError } from '../api/safe-error';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class ContextService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private requestSequence = 0;

  readonly contexts = signal<FoundationContextCandidate[]>([]);
  readonly loading = signal(false);
  readonly switching = signal(false);
  readonly lastError = signal<SafeUiError | null>(null);
  readonly currentContext = computed(() => {
    const selectedId = this.auth.session()?.selectedContextId;
    return selectedId ? this.contexts().find((candidate) => candidate.contextId === selectedId) ?? null : null;
  });

  constructor() {
    effect(() => {
      const state = this.auth.status();
      if (state === 'anonymous' || state === 'expired' || state === 'error') {
        this.contexts.set([]);
        this.lastError.set(null);
      }
    });
  }

  async load(): Promise<FoundationContextCandidate[]> {
    this.loading.set(true);
    this.lastError.set(null);
    try {
      const response = await firstValueFrom(this.api.get<FoundationContextsResponse>('/auth/contexts'));
      this.contexts.set(response.contexts ?? []);
      return this.contexts();
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      this.lastError.set(safeError);
      if (safeError.code === 'authentication_failed') {
        this.auth.markSessionExpired();
      }
      return [];
    } finally {
      this.loading.set(false);
    }
  }

  async switchContext(contextId: string): Promise<boolean> {
    const candidate = this.contexts().find((item) => item.contextId === contextId);
    const session = this.auth.session();
    if (!candidate || !session) {
      this.lastError.set({ code: 'access_denied', status: 403, correlationId: null });
      return false;
    }

    const sequence = ++this.requestSequence;
    this.switching.set(true);
    this.lastError.set(null);
    try {
      if (!(await this.auth.bootstrapAntiforgery())) {
        return false;
      }
      const request: FoundationContextSwitchRequest = {
        contextId: candidate.contextId,
        expectedSelectionVersion: session.selectionVersion,
        expectedEligibilityVersion: candidate.eligibilityVersion,
      };
      const headers = this.auth.requestHeaders()
        .set('Idempotency-Key', this.idempotencyKey());
      const response = await firstValueFrom(
        this.api.post<FoundationSessionResponse>('/auth/context-switch', request, { headers }),
      );
      if (sequence !== this.requestSequence) {
        return false;
      }
      this.auth.acceptServerSession(response);
      return true;
    } catch (error: unknown) {
      if (sequence === this.requestSequence) {
        const safeError = toSafeUiError(error);
        this.lastError.set(safeError);
        if (safeError.code === 'authentication_failed') {
          this.auth.markSessionExpired();
        }
      }
      return false;
    } finally {
      if (sequence === this.requestSequence) {
        this.switching.set(false);
      }
    }
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.() ?? `context-${Date.now().toString(36)}`;
  }
}
