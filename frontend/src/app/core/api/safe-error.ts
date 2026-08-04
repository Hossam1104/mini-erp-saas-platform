import { HttpErrorResponse } from '@angular/common/http';
import { FoundationProblemDetails } from './foundation.models';

export type SafeErrorCode =
  | 'authentication_failed'
  | 'access_denied'
  | 'context_version_conflict'
  | 'validation_failed'
  | 'network_error'
  | 'antiforgery_failed'
  | 'audit_unavailable'
  | 'request_failed';

export interface SafeUiError {
  code: SafeErrorCode;
  status: number;
  correlationId: string | null;
}

const knownCodes = new Set<SafeErrorCode>([
  'authentication_failed',
  'access_denied',
  'context_version_conflict',
  'validation_failed',
  'network_error',
  'antiforgery_failed',
  'audit_unavailable',
  'request_failed',
]);

export function toSafeUiError(error: unknown): SafeUiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { code: 'request_failed', status: 0, correlationId: null };
  }

  const body = (error.error ?? {}) as FoundationProblemDetails;
  const candidate = body.code;
  const code = knownCodes.has(candidate as SafeErrorCode)
    ? (candidate as SafeErrorCode)
    : error.status === 401
      ? 'authentication_failed'
      : error.status === 403
        ? 'access_denied'
        : error.status === 409
          ? 'context_version_conflict'
          : error.status === 0
            ? 'network_error'
            : 'request_failed';

  return {
    code,
    status: error.status,
    correlationId: typeof body.correlationId === 'string' ? body.correlationId : null,
  };
}
