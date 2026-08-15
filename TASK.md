# MESP-123 — Purchase Request / Approval Foundation Handoff

## Current bounded session

MESP-123 Phase A is complete on branch
`feat/MESP-123-purchase-request-approval`, based on starting main
`7eac2155982e7bedbe7a243a33b74998031dbfbe`.

The backend/API vertical slice is the only completed scope in this session:

- Tenant-owned internal Purchase Request Drafts scoped to Company or Branch;
- Product/UOM/quantity/need-by/purpose lines, with server-resolved Product and
  UOM snapshots;
- list, detail, create, edit, submit, approve, reject, return-for-change, and
  eligible cancel operations;
- immutable lifecycle, approval, history, and audit evidence;
- reusable configuration-led approval-policy and bounded delegation seams;
- no self-approval / separation-of-duties enforcement;
- optimistic concurrency and required mutation idempotency;
- Foundation authorization, antiforgery, audit coordination, REST catalogue,
  generated OpenAPI, and Development/QA Scalar reference;
- focused Tenant, authorization, lifecycle, delegation, and stale-version
  tests.

Purchase Request remains internal demand only. This capability does not create
stock, supplier commitment, Supplier Quotation, Purchase Order, receipt,
invoice, AP, payment, accounting, or any other downstream commercial effect.
MESP-48 remains open; no production volume, capacity, or SLO claim is allowed.
No Jira or other external-tracker work is part of this handoff. Do not touch,
delete, rename, replace, regenerate, optimize, recolor, move, or restore any
Owner-managed source asset under `frontend/assets`.

Validation baseline for this handoff:

- backend Release solution build: 0 warnings / 0 errors;
- backend non-SQL suite: 718 / 718, including four focused Purchase Request
  tests;
- SQL safety: 21 cases remain gated when
  `MESP_SQLSERVER_CONNECTION_STRING` is unavailable;
- Angular suite: 158 / 158;
- Angular initial bundle: 439.15 kB;
- final Development runtime: MiniERP API 5300 and Angular 4300;
- RMS ports 5000/5001 are unrelated and must not be touched.

One Draft PR must be created for this branch and must not be merged in the
MESP-123 Phase A session. Stop after the backend/API slice is ready for the
next executor.

## Next exact session — Claude Sonnet 5

Build the first visible Angular Purchase Request UI against the existing
MESP-123 API contract. Keep the work bounded to the Purchase Request journey:
list/detail/create/edit Draft, lines, submit, approval decisions, return for
change, eligible cancel, history/audit evidence, optimistic concurrency,
idempotency/error handling, EN/AR and RTL/LTR behavior, accessibility, and
responsive states. Reuse the existing Angular shell, shared patterns, and
server-derived action affordances. Do not add Supplier Quotation, Purchase
Order, receipt, invoice, payment, stock, AP, accounting, external providers,
credentials, Jira work, or production infrastructure.

The Sonnet session must not merge the existing Draft PR, must not start a
different capability, and must stop after its bounded UI handoff for review.
