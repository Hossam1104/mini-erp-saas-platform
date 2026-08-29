# AI Executor Tooling

## Ponytail

Ponytail is an executor-level productivity and organization tool for MESP implementation work.

### Supported Executors

- Claude Code (Claude IDE)
- OpenAI Codex

### Default Mode

When Ponytail is installed and available, use **FULL** as the default mode for normal MESP implementation work.

### Activation Examples

**Claude Code:**
```
/ponytail:ponytail full
```

**OpenAI Codex:**
```
@ponytail full
```

### FULL Semantics

FULL is a productivity mode only and must never weaken:

- Validation, test coverage, or acceptance gates
- Tenant isolation or authorization semantics
- Financial/accounting integrity
- Data-loss safeguards or concurrency control
- Audit/evidence/traceability
- Accessibility or API contracts
- Security or deployment rules

### Unavailability

Absence or unavailability of Ponytail must not block otherwise valid work:

1. Report unavailability honestly at the start of the session.
2. Continue under the repository's normal rules and governance.
3. Do not attempt to install or configure Ponytail during execution.
4. Only stop if the bounded task specifically requires Ponytail.

### Machine-Local Installation

Ponytail's installation files, plugin cache, marketplace clones, hook trust state, and hook configuration live under the executor's Windows user profile. They are **not repository source files** and must not be committed to the repository.

Repository governance documents expected behavior only; executors must configure Ponytail locally per the repository rules.
