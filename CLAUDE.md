@AGENTS.md

# Mini ERP execution guidance — Claude-specific

This repository is a reusable, bilingual, multi-tenant B2B ERP foundation.

## Entry point

- Read `TASK.md` for the current bounded session and active checkpoint.
- Read `.ai/CURRENT_STATE.md` for concise project truth and live state.
- Read `.ai/AI_TOOLING_SETUP.md` for executor tooling setup and Ponytail details.

## Claude execution rules

- MESP-136 is accepted and merged; MESP-137 and later capabilities are inactive
  and must not be activated by this session.
- Use Ponytail FULL when installed and available, but never trade safety or
  validation for a smaller diff.
- Run the full available validation suite before push.
- Stop for independent GPT-5.6 Sol acceptance. Do not merge the PR, mark Ready,
  close Jira checkpoint items, or activate later capabilities without explicit
  Sol/Owner authorization.
- The executor must update the tracked statistics file (`docs/staticts.md`) when
  project progress or implementation state changes materially.
