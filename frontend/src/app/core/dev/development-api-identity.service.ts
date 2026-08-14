import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../api/api-client.service';
import { FoundationModuleRegistrationResponse } from '../api/foundation.models';

export type DevelopmentApiIdentityStatus = 'idle' | 'checking' | 'connected' | 'unavailable';

const EXPECTED_PLATFORM_MODULE = 'platform-administration';
const EXPECTED_MASTER_DATA_MODULE = 'master-data-catalog';
const EXPECTED_BUSINESS_PARTIES_MODULE = 'business-parties';

/**
 * Development-only guard against the Angular dev server proxying credentials
 * to a non-MiniERP localhost service. Not used, and never load-bearing, for
 * Production authentication.
 */
@Injectable({ providedIn: 'root' })
export class DevelopmentApiIdentityService {
  private readonly api = inject(ApiClientService);

  readonly status = signal<DevelopmentApiIdentityStatus>('idle');

  async check(): Promise<boolean> {
    this.status.set('checking');
    try {
      const response = await firstValueFrom(
        this.api.get<FoundationModuleRegistrationResponse>('/module-registration'),
      );
      const identified =
        response.module === EXPECTED_PLATFORM_MODULE &&
        response.masterData?.module === EXPECTED_MASTER_DATA_MODULE &&
        response.businessParties?.module === EXPECTED_BUSINESS_PARTIES_MODULE;
      this.status.set(identified ? 'connected' : 'unavailable');
      return identified;
    } catch {
      this.status.set('unavailable');
      return false;
    }
  }
}
