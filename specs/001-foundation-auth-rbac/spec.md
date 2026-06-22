# Feature Specification: Phase 1 — Foundation, Contract & Authentication (RBAC)

**Feature Branch**: `001-foundation-auth-rbac`

**Created**: 2026-06-13

**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 — User Login (Priority: P1)

A system user opens the login screen, submits their registered email address and password, and
receives a signed session token. The token encodes their identity, assigned role, and subsidiary
scope, enabling all subsequent authenticated requests across the platform.

**Why this priority**: Authentication is the absolute prerequisite for every other feature. No
dashboard, transaction, or report can be accessed without a valid session token. This story
must be complete before any other user story can be implemented or tested.

**Independent Test**: Can be fully tested by submitting a POST request to `/api/auth/login` with
valid credentials and verifying that a token with the correct claims is returned. Delivers a
functional login gate that protects the entire application.

**Acceptance Scenarios**:

1. **Given** a registered user with valid credentials, **When** they submit their email and
   password, **Then** the system returns HTTP 200 with a `token` string and an `expiresAt`
   timestamp.
2. **Given** a registered user provides an incorrect password, **When** they attempt to log in,
   **Then** the system returns HTTP 401 with an RFC 7807 Problem Details body and no token.
3. **Given** a request body contains a malformed email (e.g., missing `@`), **When** submitted
   to the login endpoint, **Then** the system returns HTTP 400 with an `errors` object
   describing the validation failure.
4. **Given** valid credentials, **When** the token is decoded, **Then** it contains distinct
   claims for `UserId`, `Role` (one of Admin, Editor, Auditor), and `SubsidiaryId`.

---

### User Story 2 — Session Context Resolution (Priority: P2)

An already-authenticated user (or client application) calls the session context endpoint to
retrieve the caller's full identity profile — including their name, email, RBAC role, and
subsidiary assignment. The frontend uses this data on application startup to render the correct
navigation, subsidiary selector, and permission-gated UI elements.

**Why this priority**: Once login works, the frontend needs a reliable way to hydrate the
session on page load or token refresh without re-authenticating. This story enables the client
to resolve "who am I?" in a single call and is a prerequisite for any role-aware or
subsidiary-scoped UI.

**Independent Test**: Can be fully tested by calling GET `/api/auth/me` with a valid Bearer
token and verifying the returned payload matches the authenticated user's stored profile and
role. Delivers a working session bootstrap that the entire frontend depends on.

**Acceptance Scenarios**:

1. **Given** a valid Bearer token in the Authorization header, **When** the user calls
   `/api/auth/me`, **Then** the system returns HTTP 200 with `id`, `name`, `email`, `role`,
   and `subsidiaryId`.
2. **Given** a Global Admin account (not scoped to a single subsidiary), **When** the user
   calls `/api/auth/me`, **Then** the response includes `subsidiaryId: null`.
3. **Given** a valid token for a role-scoped user, **When** the user calls `/api/auth/me`,
   **Then** `role` is exactly one of `Admin`, `Editor`, or `Auditor`.

---

### User Story 3 — Token Rejection & Standardized Error Feedback (Priority: P3)

The system consistently rejects and clearly describes invalid or missing authentication inputs
at both the login endpoint and any protected endpoint, returning structured error responses that
client applications can parse and display without additional interpretation.

**Why this priority**: Consistent, machine-readable error responses are required by the frontend
MSW mock layer and by end users who need actionable feedback. They also prevent information
leakage from raw or inconsistent server error messages.

**Independent Test**: Can be fully tested by submitting malformed requests to both endpoints and
verifying that all error responses conform to RFC 7807 Problem Details without exposing
internal details.

**Acceptance Scenarios**:

1. **Given** no Authorization header is sent, **When** calling GET `/api/auth/me`, **Then** the
   system returns HTTP 401 with an RFC 7807 body.
2. **Given** an expired JWT is sent, **When** calling GET `/api/auth/me`, **Then** the system
   returns HTTP 401 with a descriptive `detail` field and no profile data.
3. **Given** a structurally invalid JWT (tampered signature), **When** calling any protected
   endpoint, **Then** the system returns HTTP 401 — never HTTP 500.
4. **Given** a login request with a missing required field (`email` or `password`), **When**
   submitted, **Then** the system returns HTTP 400 with an `errors` object keyed by field name.

---

### Edge Cases

- What happens when a user account is disabled or soft-deleted after a token is issued?
- How does the system behave when the JWT signing key is rotated mid-session?
- What is the behavior when `password` meets the minimum length but contains only whitespace?
- How are concurrent login attempts from the same account handled?
- What response is returned if the database is unreachable during authentication (should be
  HTTP 500 with RFC 7807, never a raw exception)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST authenticate users via email and password and return a signed JWT on
  successful credential verification.
- **FR-002**: The issued JWT MUST encode three claims: `UserId` (UUID), `Role` (one of Admin,
  Editor, Auditor), and `SubsidiaryId` (UUID or null for Global Admin).
- **FR-003**: Authenticated users MUST be able to retrieve their current profile, role, and
  subsidiary context via a token-protected endpoint without re-submitting credentials.
- **FR-004**: The `subsidiaryId` field in the user context response MUST explicitly support
  `null` to represent Global Admin accounts that are not scoped to a single subsidiary.
- **FR-005**: All error responses for HTTP 400, 401, and 500 status codes MUST conform to the
  RFC 7807 Problem Details specification (fields: `type`, `title`, `status`, `detail`,
  `instance`, and optionally `errors`).
- **FR-006**: All JSON request and response field names MUST use camelCase formatting.
- **FR-007**: The login endpoint MUST validate that `email` is a well-formed email address and
  that `password` meets the minimum length requirement of 8 characters before attempting
  credential verification.
- **FR-008**: The session context endpoint MUST require a valid, non-expired Bearer token and
  MUST reject all requests without one.

### Key Entities

- **LoginRequest**: Represents the credentials submitted by a user to initiate a session.
  Fields: `email` (valid email format, required), `password` (minimum 8 characters, required).
- **LoginResponse**: Represents the outcome of a successful authentication. Fields: `token`
  (signed JWT string, required), `expiresAt` (ISO 8601 date-time of token expiry, required).
- **UserContextResponse**: Represents the identity snapshot of the currently authenticated
  caller. Fields: `id` (UUID, required), `name` (display name, required), `email` (required),
  `role` (enum: Admin | Editor | Auditor, required), `subsidiaryId` (UUID, nullable — null for
  Global Admin accounts, required in schema).
- **ErrorResponse**: RFC 7807 Problem Details envelope used for all error conditions. Fields:
  `type` (URI reference), `title` (short human-readable summary), `status` (HTTP status code),
  `detail` (human-readable explanation), `instance` (URI of the specific occurrence), `errors`
  (optional object with field-level validation details, nullable).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with valid credentials can complete the login flow and receive a usable
  session token in under 2 seconds under normal operating conditions.
- **SC-002**: All three defined error scenarios (400 validation, 401 bad credentials, 401
  missing/expired token) return structurally identical RFC 7807 responses without exposing
  stack traces, database errors, or internal system details.
- **SC-003**: The session context endpoint returns a complete identity payload that the frontend
  can use immediately — without additional API calls — to render role-appropriate navigation
  and enforce subsidiary-scoped UI restrictions.
- **SC-004**: The MSW mock layer can fully simulate all defined endpoint behaviors (success and
  all error states) using only the contracts defined in `specs/openapi.yaml`, enabling frontend
  development to proceed in full isolation from the backend.

## Assumptions

- Users and their role/subsidiary assignments are pre-provisioned in the database; self-
  registration is out of scope for Phase 1.
- Token expiry duration is a configurable server-side value; a default of 8 hours is assumed
  for Phase 1.
- Token refresh and revocation flows are out of scope for Phase 1; they may be addressed in a
  later phase.
- A Global Admin account is identified by a null `SubsidiaryId` in the user record; this
  interpretation is baked into the JWT claim and the context endpoint response.
- Password hashing algorithm and storage standards follow industry best practices (e.g.,
  bcrypt); the spec does not mandate a specific algorithm.
- HTTPS is enforced at the infrastructure/reverse-proxy level; the API layer does not need to
  address transport security within this feature scope.
- The `name` field in `UserContextResponse` is a single display name string; first/last name
  split is out of scope for Phase 1.
