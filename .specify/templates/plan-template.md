# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: Backend .NET 8; Frontend TypeScript (React + Vite)

**Primary Dependencies**: EF Core, Dapper, TanStack Query, Shadcn/ui, MSW (see constitution)

**Storage**: SQL Server (single database, `SubsidiaryId` column isolation)

**Testing**: [e.g., xUnit, Vitest, Playwright or NEEDS CLARIFICATION]

**Target Platform**: Web (responsive desktop/tablet/mobile browsers)

**Project Type**: Enterprise Monorepo (Frontend & Backend)

**Performance Goals**: [domain-specific, e.g., dashboard p95 < 500ms via Dapper or NEEDS CLARIFICATION]

**Constraints**: OpenAPI contract-first; DIP; RBAC; automated audit trail (see constitution)

**Scale/Scope**: Corporate Cash Flow & Treasury Management with multi-subsidiary isolation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verify compliance with `.specify/memory/constitution.md`:

- [ ] OpenAPI contract defined or updated in `/specs/openapi.yaml` before implementation
- [ ] `SubsidiaryId` isolation in data model and API design
- [ ] RBAC roles (Admin, Editor, Auditor) mapped to endpoint authorization
- [ ] CQS split documented (EF Core writes vs Dapper reads where applicable)
- [ ] Audit trail coverage for new mutating entities
- [ ] MSW handlers planned for new/changed endpoints
- [ ] Frontend feature folder under `/src/features/` and URL filter sync identified
- [ ] DIP respected: interfaces in `API.Business`, implementations in `API.Repository.Implementation`
- [ ] Controllers remain lean (no business or database logic)

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Enterprise monorepo (Corporate Cash Flow — default for this project)
backend/
├── API.Business/                    # Domain logic + repository interfaces
├── API.Repository.Implementation/   # EF Core + Dapper implementations
└── [API host project]/              # Lean controllers only

frontend/
├── src/
│   ├── features/                    # Feature-based modules (auth, transactions, …)
│   ├── components/                  # Shared UI (Shadcn/ui)
│   └── mocks/                       # MSW handlers (OpenAPI-aligned)
└── tests/

specs/
└── openapi.yaml                     # Single source of truth for API contracts

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
