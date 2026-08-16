# ADR-019 — Tenant Host Resolution, Operational Workspace Context, and Configured Branding

**Status:** Accepted for planned implementation  
**Date:** 17 August 2026  
**Decision owner:** Product Owner / Mini ERP SaaS Platform  
**Primary Jira:** MESP-143  
**Related Jira:** MESP-2, MESP-4, MESP-12, MESP-67, MESP-77, MESP-123

## 1. Context

The current foundation UI exposes a pre-ERP “Choose a workspace” flow where the selected item is effectively the Tenant itself (for example, `Wafra · Tenant membership`). This is temporary foundation plumbing, not the target SaaS ERP experience.

For a normal user of one Tenant, the Tenant is not a business choice to make on every login. It is a security/data-isolation boundary that must be resolved and authorized before Tenant business data is loaded. A normal Wafra user must not need to know that other Tenants exist and must never be offered unrelated Tenant names or identifiers.

The platform backlog also uses **Tenant Workspace** for the Platform Administration control-plane surface (MESP-67). That is distinct from an ordinary ERP user's operational working context inside a Tenant.

The Owner has added a Wafra branding/logo asset and a Saudi Riyal symbol asset under `frontend/assets`. They are configuration/country-pack inputs, not authorization or customer-specific product rules.

## 2. Decision

### 2.1 Tenant and Workspace are distinct

Canonical model:

```text
MESP Platform
└── Tenant (security/data-isolation boundary)
    └── Company / Legal Entity
        └── Branch / bounded operational context
            └── ERP working context where applicable
```

- **Tenant** is server-authoritative and is the SaaS isolation boundary.
- **Operational Workspace / Context** is inside an already-authorized Tenant and should align with approved Company/Branch scope rather than invent a parallel authorization hierarchy.
- **Platform Tenant Workspace** (MESP-67) remains a separate control-plane concept and must not imply Tenant ERP business authority.

### 2.2 Tenant-specific host is the normal ERP entry

A Tenant may have one or more verified host bindings. A canonical host may look like:

```text
wafra.mesp.com
```

The host resolves a **candidate Tenant** only. It does not grant authority.

```text
wafra.mesp.com
→ authenticate
→ resolve host binding to candidate Tenant
→ verify exact-Tenant membership/access
→ establish server-owned Tenant context
→ load Wafra Overview
```

An authenticated user without Wafra access receives a safe denial and no Wafra business data.

### 2.3 Common host is a routing entry

Illustrative `mesp.com` behavior:

- exactly one authorized Tenant → redirect directly to its canonical host;
- multiple authorized Tenants → show only those legitimate memberships, then redirect;
- zero memberships → safe no-access/onboarding state;
- never expose an unrelated Tenant catalogue to an ordinary user.

Raw Tenant GUIDs are not user-facing selectors.

### 2.4 Platform Administration is a separate control plane

Use a separate platform-admin surface/host (illustratively `admin.mesp.com`).

Platform Administrator authority is not Tenant business authority.

The control plane may expose purpose-bound administrative metadata such as Tenant identity, lifecycle, plans, entitlements, limits, branding governance, support access, exports/offboarding, and audit.

Any support/platform-admin entry into Tenant ERP data must use an approved exact-Tenant membership or bounded support grant and must be attributable/audited.

### 2.5 Operational Workspace selection occurs after Overview and only when needed

Do not force ordinary Tenant users through a Tenant/Workspace chooser before Overview.

- one permitted operational context → auto-select;
- multiple permitted operational contexts → header/application-context selector;
- remove mandatory `Switch workspace` from ordinary-user primary navigation;
- `/app/workspaces` may remain for management/discovery, but not as a login gate;
- header should distinguish Organization/Tenant from Company/Branch/operational context;
- no normal flow requires typing Tenant/Company/Branch/Workspace GUIDs.

### 2.6 Host binding is configuration-led and custom-domain ready

Do not hard-code `{tenant}.mesp.com` as the only model.

Introduce a generic host-binding abstraction (working name `TenantHostBinding`) capable of approved aliases/custom domains later.

```text
wafra.mesp.com       → Tenant Wafra
erp.wafra.example    → Tenant Wafra
customer-b.mesp.com  → Tenant Customer B
```

Host configuration must be validated, auditable, and collision-safe. Production DNS/TLS automation is separate infrastructure scope.

### 2.7 Wafra branding is Tenant configuration

The owner-added Wafra logo asset under `frontend/assets` may be associated with Wafra through a generic Tenant branding profile.

Required behavior:

- inventory the exact owner-added filename(s) before implementation;
- Tenant branding is configuration/data, never a `Wafra` code branch;
- missing/rejected/unavailable Tenant branding falls back to MESP platform branding;
- branding never changes authorization, Tenant context, workflow, tax, numbering, or navigation permission;
- provide alt/accessibility and EN/AR, RTL/LTR, light/dark/fallback behavior;
- do not rename, recolor, re-encode, replace, or delete owner source assets without explicit approval.

Recommended visual ownership:
- `wafra.mesp.com`: Wafra Tenant logo may be primary in the Tenant ERP shell, with MESP as secondary/powered-by platform identity if desired.
- `mesp.com` and `admin.mesp.com`: MESP platform branding remains primary.

### 2.8 Saudi Riyal symbol is Saudi/SAR presentation

The owner-added Saudi Riyal symbol asset under `frontend/assets` is a Saudi country-pack/currency-presentation asset.

It is not Wafra branding and not a global currency rule.

- SAR remains the currency identity; symbol rendering is presentation only.
- No FX conversion, tax rule, accounting meaning, or persisted amount changes.
- Non-SAR currencies retain their own configured presentation.
- Safe text fallback such as `SAR` remains available.
- In multi-currency/comparison/audit/export contexts, preserve an unambiguous currency code even when a symbol is rendered.
- Validate EN/AR, RTL/LTR, screen, print/document, sizing/alignment, and accessibility.
- Inventory the exact owner-added filename(s) and preserve source assets unchanged.

This complements MESP-12/MESP-37 and does not bypass their regulatory/accounting gates.

## 3. Security invariants

1. Host resolution produces candidate Tenant context only.
2. Authentication plus exact-Tenant authorization is required before Tenant business data.
3. Client-provided Tenant identifiers cannot expand scope.
4. Cross-Tenant access is denied by default.
5. Tenant/organization ownership remains enforced on all reads/writes.
6. Platform Admin role alone grants no Tenant ERP access.
7. Support/admin entry is explicit and audited.
8. Forwarded-host/host handling must trust only configured proxies and resist spoofing/misrouting.
9. Branding and country presentation never influence authorization.

## 4. UX consequence

Temporary foundation flow:

```text
Login → Choose workspace/Tenant → ERP
```

Target Tenant-user flow:

```text
Tenant host
→ Sign in
→ exact-Tenant membership verified
→ Tenant Overview
→ auto-select one operational context OR header-switch among permitted contexts
→ ERP modules
```

Target Platform Admin flow:

```text
admin.mesp.com
→ Platform Overview / Tenant Catalogue
→ administrative Tenant Workspace (MESP-67)
→ optional separately authorized/audited Tenant ERP entry
```

The Tenant Overview should evolve toward implemented business status/tasks/alerts instead of foundation/session diagnostics as primary content.

## 5. Delivery sequence

1. Close MESP-123 Opus findings F-1/F-2/F-5 and targeted re-verification.
2. Merge/close MESP-123 only after Owner/GPT-5.6 Sol decision.
3. Activate MESP-143 before broad additional Tenant-facing UI expansion.
4. Coordinate MESP-143 with:
   - MESP-65 / MESP-66 / MESP-67 — Platform control plane;
   - MESP-77 — Tenant branding;
   - MESP-12 / MESP-37 — Saudi country pack and SAR presentation.
5. Continue downstream procurement UI against the corrected Tenant/Workspace model.

## 6. Non-goals

This ADR does not itself implement:

- production DNS/TLS provisioning;
- subscription billing;
- new support impersonation mechanisms;
- Purchase Order / receipt / invoice / AP / accounting / payment / stock effects;
- statutory ZATCA/FATOORA behavior;
- customer-specific forks;
- a second Workspace authorization hierarchy separate from Company/Branch.

## 7. Required implementation validation

MESP-143 must cover:

- valid/unknown host resolution;
- unauthorized user on a valid Tenant host;
- single-membership automatic routing;
- bounded multi-membership chooser;
- no unrelated Tenant enumeration;
- Platform Admin versus Tenant ERP authority;
- single-workspace auto-selection;
- multi-workspace header switching;
- Company/Branch scope preservation;
- EN/AR and RTL/LTR;
- accessibility/keyboard behavior;
- Tenant branding fallback and no-Wafra-branch tests;
- SAR/Riyal-symbol presentation plus text fallback;
- host/proxy spoofing tests;
- regression for existing procurement routes/Tenant isolation.

## 8. Asset inventory note

The Owner reports new Wafra-logo branding and Saudi Riyal symbol files locally under `frontend/assets`.

At the time this ADR was prepared, their exact new filenames were not visible in the current remote branch. Do not guess them. The implementation executor must inventory the local working tree first and preserve the owner-managed files exactly.
