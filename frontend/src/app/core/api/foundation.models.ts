export type FoundationPath =
  | 'OrdinaryMembership'
  | 'SupportGrant'
  | 'PlatformGovernanceContext'
  | string;

export interface FoundationSessionResponse {
  authenticated: boolean;
  actorId: string | null;
  sessionId: string | null;
  lifecycleState: string;
  absoluteExpiresAt: string | null;
  selectedPath: FoundationPath | null;
  selectedTenantId: string | null;
  selectedContextId: string | null;
  selectionVersion: number;
  replayed?: boolean;
}

export interface FoundationContextCandidate {
  contextId: string;
  kind: FoundationPath;
  tenantId: string | null;
  displayName: string;
  eligibilityVersion: number;
}

export interface FoundationContextsResponse {
  contexts: FoundationContextCandidate[];
}

export interface FoundationContextSwitchRequest {
  contextId: string;
  expectedSelectionVersion: number;
  expectedEligibilityVersion: number;
}

export interface FoundationProblemDetails {
  code?: string;
  correlationId?: string;
  operationId?: string;
}

export interface FoundationModuleDescriptor {
  module: string;
  name: string;
  boundary: string;
  registered: boolean;
}

export interface FoundationModuleRegistrationResponse extends FoundationModuleDescriptor {
  masterData: FoundationModuleDescriptor;
  businessParties: FoundationModuleDescriptor;
}
