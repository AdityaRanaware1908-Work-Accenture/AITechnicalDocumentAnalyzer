# Sample documents

Input documents for trying the analyzer. They are shared by every project in the solution.

## Layout and naming

```
samples/
  requirements/<scenario>.requirements.txt
  designs/<scenario>.design.txt
```

- A **scenario** is one test set. The requirements file and the design file of a scenario share the part of the name before the first dot, so the UI can pick the matching design when you pick a requirements file.
- Use lowercase words separated by hyphens for the scenario. Plain `.txt` (or `.md`) files, one statement per line, each starting with an identifier (`REQ-001: ...`, `DESIGN-001: ...`).
- To add a set, drop a new pair into the two folders. No code change is needed.

## Scenarios and what to expect

| Scenario | Purpose | Expected highlights |
|---|---|---|
| `web-app-basics` | Small smoke test, 4 requirements | Analysis: REQ-001 `vague_language`, nothing else. Compare: REQ-001 `not_assessable`; REQ-002 `satisfied`; REQ-003 conflict (300 ms vs 500 ms, 95% equal); REQ-004 conflict (365 vs 90 days); the cache statement is not a finding. |
| `ecommerce-orders` | Covers every issue type, 10 requirements | Analysis: `vague_language` (REQ-003), `ambiguous_statement` (REQ-004), `incomplete_requirement` (REQ-005), `internal_contradiction` (REQ-006 with REQ-007), `untestable_requirement` (REQ-008); REQ-001, 002, 009, 010 clean. Compare: REQ-001, 002, 006 satisfied; REQ-003 not assessable; REQ-004 partial; REQ-005 not addressed; REQ-007, 008, 009 conflict; REQ-010 design unclear. |
| `banking-transfers` | Numbers, behaviors and security, 7 requirements | Indicative only. Compare: REQ-002 conflict (2 s / 99% vs 2.5 s / 95%); REQ-003 conflict (no idempotency); REQ-005 conflict (RTO 30 vs 60 minutes, RPO equal); REQ-007 conflict (internal HTTP). Judgment calls: REQ-001 (MFA setup vs MFA on every session), REQ-004 (two independent transactions), REQ-006 (administrator update rights). |

Model output varies, so treat the expectations as a guide, not an exact test. Judgment calls are marked.
