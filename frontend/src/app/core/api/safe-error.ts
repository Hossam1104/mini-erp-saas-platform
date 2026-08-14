import { HttpErrorResponse } from '@angular/common/http';
import { FoundationProblemDetails } from './foundation.models';

export type SafeErrorCode =
  | 'authentication_failed'
  | 'access_denied'
  | 'permission_denied'
  | 'context_version_conflict'
  | 'concurrency_conflict'
  | 'validation_failed'
  | 'network_error'
  | 'antiforgery_failed'
  | 'audit_unavailable'
  | 'price_list_not_found'
  | 'price_list_inactive'
  | 'price_list_effective_overlap'
  | 'price_list_precedence_conflict'
  | 'price_list_duplicate'
  | 'idempotency_conflict'
  | 'idempotency_key_invalid'
  | 'customer_reference_unavailable'
  | 'persistence_unavailable'
  | 'import_resource_unsupported'
  | 'import_request_invalid'
  | 'import_row_limit_exceeded'
  | 'import_row_invalid'
  | 'import_row_number_invalid'
  | 'import_row_payload_too_large'
  | 'import_field_too_large'
  | 'import_source_invalid'
  | 'import_batch_invalid_state'
  | 'import_batch_not_found'
  | 'import_dry_run_no_commit'
  | 'import_concurrency_conflict'
  | 'import_replay_idempotency_required'
  | 'import_replay_idempotency_conflict'
  | 'import_row_not_found'
  | 'import_replay_not_allowed'
  | 'import_unavailable'
  | 'import_duplicate'
  | 'import_reference_invalid'
  | 'import_reference_unavailable'
  | 'import_update_not_permitted'
  | 'import_commit_failed'
  | 'import_commit_reparse_failed'
  | 'import_field_invalid'
  | 'import_field_required'
  | 'import_field_duplicate'
  | 'import_untrusted_field'
  | 'import_persistence_failed'
  | 'import_audit_persistence_failed'
  | 'organization_scope_denied'
  | 'request_failed';

export interface SafeUiError {
  code: SafeErrorCode;
  status: number;
  correlationId: string | null;
}

const knownCodes = new Set<SafeErrorCode>([
  'authentication_failed',
  'access_denied',
  'permission_denied',
  'context_version_conflict',
  'concurrency_conflict',
  'validation_failed',
  'network_error',
  'antiforgery_failed',
  'audit_unavailable',
  'price_list_not_found',
  'price_list_inactive',
  'price_list_effective_overlap',
  'price_list_precedence_conflict',
  'price_list_duplicate',
  'idempotency_conflict',
  'idempotency_key_invalid',
  'customer_reference_unavailable',
  'persistence_unavailable',
  'import_resource_unsupported',
  'import_request_invalid',
  'import_row_limit_exceeded',
  'import_row_invalid',
  'import_row_number_invalid',
  'import_row_payload_too_large',
  'import_field_too_large',
  'import_source_invalid',
  'import_batch_invalid_state',
  'import_batch_not_found',
  'import_dry_run_no_commit',
  'import_concurrency_conflict',
  'import_replay_idempotency_required',
  'import_replay_idempotency_conflict',
  'import_row_not_found',
  'import_replay_not_allowed',
  'import_unavailable',
  'import_duplicate',
  'import_reference_invalid',
  'import_reference_unavailable',
  'import_update_not_permitted',
  'import_commit_failed',
  'import_commit_reparse_failed',
  'import_field_invalid',
  'import_field_required',
  'import_field_duplicate',
  'import_untrusted_field',
  'import_persistence_failed',
  'import_audit_persistence_failed',
  'organization_scope_denied',
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
          ? 'concurrency_conflict'
          : error.status === 400
            ? 'validation_failed'
            : error.status === 0
              ? 'network_error'
              : 'request_failed';

  return {
    code,
    status: error.status,
    correlationId: typeof body.correlationId === 'string' ? body.correlationId : null,
  };
}
