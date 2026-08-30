@AGENTS.md

## Current handoff - 30 August 2026

MESP-137 is accepted, Done, and merged through PR #84. MESP-144 remains the
active In Progress repository-health checkpoint on Draft PR #82; keep it
Open/Unmerged for independent Sol review. No next capability is active:
MESP-138 and MESP-139 remain To Do/inactive. The accepted fast-track boundary
is 21/26 (80.8%), while production readiness remains approximately 47% overall
and 41% Procurement/P2P.

# Mini ERP execution guidance — Claude-specific

This repository is a reusable, bilingual, multi-tenant B2B ERP foundation.

## Entry point

- Read `TASK.md` for the current bounded session and active checkpoint.
- Read `.ai/CURRENT_STATE.md` for concise project truth and live state.
- Read `.ai/AI_TOOLING_SETUP.md` for executor tooling setup and Ponytail details.

## Claude execution rules

- MESP-137 is accepted and merged; MESP-138 and later capabilities are inactive
  and must not be activated by this session.
- Use Ponytail FULL when installed and available, but never trade safety or
  validation for a smaller diff.
- Run the full available validation suite before push.
- Stop for independent GPT-5.6 Sol acceptance. Do not merge the PR, mark Ready,
  close Jira checkpoint items, or activate later capabilities without explicit
  Sol/Owner authorization.
- The executor must update the tracked statistics file (`docs/staticts.md`) when
  project progress or implementation state changes materially.
