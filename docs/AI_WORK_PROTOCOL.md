# AI work protocol

This project is expected to use multiple AIs. The main project lead controls scope and integrates results.

## Roles

### Project lead

Owns:

- phase order;
- task assignment;
- master status;
- backlog;
- parity matrix;
- evidence standards;
- conflict resolution;
- final integration decisions.

### Worker AI

Owns one assigned bounded work item.

A worker must not independently broaden scope, change the target version, redesign the product or rewrite master project policy.

## Work-item format

Every assignment should contain:

- **ID** — e.g. `LWB317-UI-001`
- **Goal**
- **Allowed scope**
- **Explicit non-goals**
- **Inputs/reference artifacts**
- **Required outputs**
- **Acceptance checks**
- **Time boundary**

## Worker start checklist

1. Read `AGENTS.md`.
2. Read `task.md`.
3. Read this file.
4. Read the exact current master doc for the assigned workstream.
5. Check `git status --short`.
6. Confirm target SHA-256/version if touching reference-derived evidence.

## Evidence rule

A worker should create a focused review/finding file for meaningful recovered facts.

Do not dump raw reasoning. Record:

- source identity;
- locator;
- tool/command;
- observation;
- interpretation;
- limits;
- implementation impact.

## Master-doc ownership

Workers may propose updates, but broad status changes should be conservative.

For ambiguous findings, mark `UNKNOWN` rather than upgrading the master matrix.

## Git

Default branch is `research/offline-controller`.

Use one coherent commit per completed work item where practical.

Do not force-push.

## Handoff template

At the end of a worker task report:

```text
Work item:
Status:
Reference:
Files changed:
Evidence:
Facts established:
Still unknown:
Checks:
Commit:
Recommended next task:
```

## Parallelism rule

Avoid two workers editing the same master file at the same time.

Prefer splitting by evidence/workstream and letting the project lead integrate master status.
