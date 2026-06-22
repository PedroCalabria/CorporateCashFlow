<!--
Sync Impact Report
- Version change: template (unversioned placeholders) → 1.0.0
- Modified principles: none (initial definition from template)
- Added principles:
  - I. Contract-First Development (OpenAPI / SDD)
  - II. Backend Clean Architecture & Dependency Inversion
  - III. Multi-Subsidiary Isolation & RBAC Security
  - IV. Frontend Feature Architecture & UX Standards
  - V. Compliance & Automated Audit Trail
- Added sections:
  - Technology Stack & Monorepo Scope
  - Development Workflow & Quality Gates
- Removed sections: none (template placeholders replaced)
- Templates:
  - ✅ .specify/templates/plan-template.md
  - ✅ .specify/templates/spec-template.md
  - ✅ .specify/templates/tasks-template.md
  - ⚠ README.md (minimal; no runtime guidance doc yet)
- Follow-up TODOs: none
-->

# Corporate Cash Flow Constitution

## Core Principles

### I. Contract-First Development (OpenAPI / SDD)

The OpenAPI specification at `/specs/openapi.yaml` is the single source of truth for all API
contracts.

- All backend endpoints and frontend API clients MUST conform to the spec before integration.
- Code generation or manual implementation MUST strictly satisfy defined contracts; drift is
  prohibited.
- Frontend development MUST use Mock Service Worker (MSW) handlers mapped 100% to the OpenAPI
  spec to enable isolated parallel development.
- Contract changes MUST update the OpenAPI spec first, then propagate to backend, MSW mocks,
  and clients.

**Rationale**: Spec-driven development decouples frontend and backend teams, prevents
integration surprises, and preserves a verifiable contract boundary.

### II. Backend Clean Architecture & Dependency Inversion

The backend follows a Clean/Layered architecture with strict Dependency Inversion Principle
(DIP).

- Stack: .NET 8 Web API, SQL Server, EF Core, Dapper.
- `API.Business` MUST contain all repository interfaces and core domain logic.
- `API.Repository.Implementation` MUST reference `API.Business` and implement those
  interfaces—never the reverse.
- Controllers MUST be lean: zero business logic and zero direct database access.
- Command Query Separation (CQS): EF Core for standard writes/mutations; Dapper for heavy read
  queries and dashboards.

**Rationale**: DIP keeps domain logic testable and independent of infrastructure; CQS optimizes
write consistency and read performance.

### III. Multi-Subsidiary Isolation & RBAC Security

The system is a Corporate Cash Flow & Treasury Management platform with multi-subsidiary
isolation.

- Database strategy: single database with column-based tenant isolation via `SubsidiaryId` on
  all subsidiary-scoped entities.
- All queries and mutations MUST enforce `SubsidiaryId` filtering derived from authenticated
  context—no cross-subsidiary data leakage.
- Security model: Role-Based Access Control (RBAC) with roles Admin, Editor, and Auditor.
- Authentication: manual JWT implementation with Claims for `UserId`, `Role`, and
  `SubsidiaryId`.
- Authorization checks MUST occur before business logic executes.

**Rationale**: Treasury data demands strict tenant boundaries and auditable role separation.

### IV. Frontend Feature Architecture & UX Standards

The frontend follows a feature-based architecture with server-driven state and consistent UX.

- Stack: React, Vite, TypeScript, Tailwind CSS, Shadcn/ui, TanStack Query, React Hook Form.
- Structure: feature folders under `/src/features/` (e.g., `auth`, `transactions`).
- State: server-driven via TanStack Query; UI filters (subsidiary, date ranges) MUST sync to
  URL query strings.
- Design: responsive across devices; follow UX best practices.
- Theme: dark mode default with user-selectable light mode toggle.
- Language: English for all UI text and user-facing copy.

**Rationale**: Feature isolation scales team delivery; URL-synced filters enable shareable
views; consistent UX reduces operational error in financial workflows.

### V. Compliance & Automated Audit Trail

Financial and treasury operations require immutable change history.

- EF Core `SaveChangesAsync` MUST be overridden to capture audit records automatically.
- Each audit entry MUST serialize changes as JSON with `OldValues` and `NewValues`.
- Audit trail MUST cover all mutating operations on governed entities without manual
  per-endpoint logging.
- Auditor role MUST have read access to audit history without mutate privileges on governed
  data.

**Rationale**: Automated audit trails satisfy compliance requirements and eliminate
inconsistent manual logging.

## Technology Stack & Monorepo Scope

**Project Type**: Enterprise Monorepo (Frontend & Backend)

**Scope**: Corporate Cash Flow & Treasury Management System with multi-subsidiary isolation.

### Repository Layout

```text
corporate_cash_flow/
├── backend/          # .NET 8 Web API solution (API.Business, API.Repository.Implementation)
├── frontend/         # React + Vite application
└── specs/
    └── openapi.yaml  # API contract (single source of truth)
```

### Backend Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 Web API |
| Database | SQL Server |
| ORM (writes) | EF Core |
| Queries (reads) | Dapper |
| Auth | Manual JWT (`UserId`, `Role`, `SubsidiaryId` claims) |

### Frontend Stack

| Concern | Technology |
|---------|------------|
| Framework | React + Vite + TypeScript |
| Styling | Tailwind CSS + Shadcn/ui |
| Data fetching | TanStack Query |
| Forms | React Hook Form |
| Mocking | MSW (mandatory, OpenAPI-aligned) |

## Development Workflow & Quality Gates

### Feature Delivery Order

1. Update or confirm OpenAPI contract in `/specs/openapi.yaml`.
2. Implement backend domain logic in `API.Business`; repository implementations in
   `API.Repository.Implementation`.
3. Expose lean controllers that delegate to business services.
4. Implement or update MSW handlers mirroring the spec for frontend parallel work.
5. Build frontend features under `/src/features/` consuming TanStack Query hooks.
6. Integrate only after contract compliance is verified on both sides.

### Constitution Check Gates (for `/speckit-plan`)

Every implementation plan MUST verify:

- [ ] OpenAPI contract defined or updated before implementation
- [ ] `SubsidiaryId` isolation accounted for in data model and API design
- [ ] RBAC roles (Admin, Editor, Auditor) mapped to endpoint authorization
- [ ] CQS split documented (EF Core writes vs Dapper reads where applicable)
- [ ] Audit trail coverage for new mutating entities
- [ ] MSW handlers planned for new/changed endpoints
- [ ] Frontend feature folder and URL filter sync identified

### Pull Request Requirements

- PRs MUST demonstrate constitution compliance in description or checklist.
- API changes MUST include OpenAPI spec diff.
- Frontend PRs touching API consumption MUST include MSW handler updates.
- Violations of DIP, lean controller, or subsidiary isolation rules MUST be rejected or
  documented in Complexity Tracking with justification.

## Governance

This constitution supersedes ad-hoc conventions for the Corporate Cash Flow monorepo.

**Amendment Procedure**:

- Proposed changes MUST be documented with rationale and version bump type
  (MAJOR/MINOR/PATCH).
- Run `/speckit-constitution` to apply amendments and propagate to dependent templates.
- MAJOR bumps indicate backward-incompatible governance or principle removals.

**Versioning Policy**:

- MAJOR: Principle removal or incompatible redefinition
- MINOR: New principle, section, or materially expanded guidance
- PATCH: Clarifications, wording, non-semantic refinements

**Compliance Review**:

- All feature specs, plans, and task lists MUST pass Constitution Check gates.
- `/speckit-analyze` SHOULD be run after task generation to validate cross-artifact
  consistency.
- Complexity deviations MUST be recorded in plan.md Complexity Tracking table.

**Version**: 1.0.0 | **Ratified**: 2026-06-13 | **Last Amended**: 2026-06-13
