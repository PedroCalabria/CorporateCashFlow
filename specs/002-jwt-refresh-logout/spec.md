# Feature Specification: Phase 1 Addendum — JWT Refresh Token Mechanism

**Feature Branch**: `002-jwt-refresh-logout`

**Created**: 2026-06-13

**Status**: Draft

## Clarifications

### Session 2026-06-13

- Q: When a consumed refresh token is reused, what is the system response? → A: HTTP 401 and immediate revocation of all active sessions for the user (family invalidation).
- Q: When the token whitelist store is temporarily unavailable during a refresh attempt, what is the system response? → A: Fail-closed — reject with HTTP 503 Service Unavailable (RFC 7807); never grant access without verified whitelist state.
- Q: Must refresh, logout, and reuse-detection events produce audit log entries per Constitution Principle V? → A: Yes — security event log entries (who, action, timestamp) for each event, kept distinct from the financial entity audit trail.
- Q: When two simultaneous requests arrive with the same valid refresh token (race condition), how is the loser handled? → A: First-wins — the duplicate receives HTTP 401 with no family invalidation; treated as a benign timing event, not an attack.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Silent Access Token Renewal (Priority: P1)

A user whose short-lived access token has expired can seamlessly obtain a new one by presenting
their longer-lived refresh token. The system validates the refresh token against a server-side
whitelist, issues a brand new access token and a new rotating refresh token, and invalidates
the previously used refresh token. The user is never prompted to re-enter their credentials.

**Why this priority**: Without token renewal, every user would be forced to log in again each
time their access token expires, breaking long-running financial workflows. Silent renewal is
the backbone of a continuous authenticated session and is a prerequisite for any session
management strategy.

**Independent Test**: Can be fully tested by obtaining a valid refresh token from the login
flow, then calling `POST /api/auth/refresh`. A new access token and refresh token are returned
and the old refresh token is rejected on subsequent use. Delivers uninterrupted session
continuity without credential re-entry.

**Acceptance Scenarios**:

1. **Given** a user holds a valid, unexpired refresh token, **When** they call
   `POST /api/auth/refresh` with that token, **Then** the system returns HTTP 200 with a
   new `accessToken`, a new `refreshToken`, and an updated `expiresAt` timestamp.
2. **Given** the same refresh token that was just consumed in a prior renewal, **When** it is
   submitted again to `POST /api/auth/refresh`, **Then** the system returns HTTP 401 and
   immediately revokes ALL active sessions for the affected user (family invalidation),
   requiring a full re-login on all devices.
3. **Given** a refresh token whose server-side record has been revoked or has exceeded its
   lifetime, **When** submitted to `POST /api/auth/refresh`, **Then** the system returns
   HTTP 401 with an RFC 7807 Problem Details body.
4. **Given** a request body missing the `refreshToken` field, **When** submitted to
   `POST /api/auth/refresh`, **Then** the system returns HTTP 400 with an `errors` object
   identifying the missing field.

---

### User Story 2 — Explicit Session Logout (Priority: P2)

An authenticated user can actively terminate their current session by calling the logout
endpoint. The server invalidates the session context tied to the current token, ensuring that
any subsequent request using that token or its associated refresh token is rejected.

**Why this priority**: Explicit logout is a security requirement for any application handling
financial data. Users must be able to end their sessions definitively, especially on shared
devices or when a potential compromise is suspected.

**Independent Test**: Can be fully tested by logging in, calling `POST /api/auth/logout` with
a valid Bearer token, and verifying that subsequent calls to protected endpoints using the
same token return 401. Delivers a reliable, trust-restoring session termination mechanism.

**Acceptance Scenarios**:

1. **Given** a valid Bearer token in the Authorization header, **When** the user calls
   `POST /api/auth/logout`, **Then** the system returns HTTP 204 No Content with no
   response body.
2. **Given** the same token used in a successful logout, **When** it is used to call any
   protected endpoint afterwards, **Then** the system returns HTTP 401.
3. **Given** no Authorization header or an invalid token, **When** calling
   `POST /api/auth/logout`, **Then** the system returns HTTP 401 with an RFC 7807 body.

---

### Edge Cases

- What happens if a refresh token is submitted concurrently from two devices simultaneously
  (race condition on single-use rotation)? **Resolved: first-wins — the duplicate gets HTTP
  401, no family invalidation (see FR-011). Family invalidation applies only to later
  replay attacks, not same-millisecond races.**
- What is the behavior when the server-side whitelist store is temporarily unavailable during
  a refresh attempt? **Resolved: fail-closed — HTTP 503, never fail-open (see FR-004b).**
- How does the system handle a structurally valid JWT refresh token whose signing key no longer
  matches (key rotation scenario)? **Resolved (out of scope)**: ASP.NET Core `JwtBearer` middleware
  returns HTTP 401 for signature mismatches — correct Phase 1 behaviour; dedicated key-rotation
  strategy is Phase 2 security hardening.
- If a user logs out and then attempts to use a still-valid access token (not yet naturally
  expired), how is that handled? **Resolved (out of scope)**: Stateless JWT remains valid until
  `expiresAt`; full access-token blacklisting is Phase 2 scope. Refresh path is fully invalidated
  after logout.
- What happens if `accessToken` in `TokenRefreshRequest` belongs to a different user than the
  submitted `refreshToken`? **Resolved**: Session binding via `jti` mismatch returns HTTP 401.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST validate the provided `refreshToken` against a server-side whitelist
  before issuing any new tokens; tokens absent from or expired in the whitelist MUST be
  rejected.
- **FR-002**: Refresh tokens MUST be single-use and rotating — each successful call to the
  refresh endpoint MUST issue a new `refreshToken` and permanently invalidate the prior one.
- **FR-003**: A successful token refresh MUST return a new `accessToken`, a new `refreshToken`,
  and an updated `expiresAt` timestamp in the response.
- **FR-004**: System MUST reject expired or revoked refresh tokens with an HTTP 401 response
  conforming to RFC 7807 Problem Details.
- **FR-004a**: When a previously consumed refresh token is submitted again (reuse/replay
  attack), the system MUST return HTTP 401 AND immediately revoke ALL active sessions for the
  affected user (family invalidation), forcing full re-authentication across all devices.
- **FR-004b**: If the token whitelist store is temporarily unavailable, the refresh endpoint
  MUST fail-closed: reject the request with HTTP 503 Service Unavailable and an RFC 7807
  body. The system MUST NOT grant new tokens when whitelist state cannot be verified.
- **FR-005**: System MUST reject malformed or incomplete refresh request payloads (missing
  required fields) with an HTTP 400 response conforming to RFC 7807 Problem Details, including
  an `errors` object identifying the offending fields.
- **FR-006**: Authenticated users MUST be able to explicitly terminate their session via a
  dedicated logout endpoint protected by Bearer token authentication.
- **FR-007**: The logout endpoint MUST require a valid, non-expired Bearer token; requests with
  a missing or invalid token MUST be rejected with HTTP 401 and an RFC 7807 body.
- **FR-008**: Successful logout MUST return HTTP 204 No Content with an empty response body.
- **FR-009**: All JSON request and response field names MUST use camelCase formatting.
- **FR-010**: The system MUST produce a security event log entry for every refresh attempt
  (successful and failed), every logout, and every reuse-detection event. Each entry MUST
  capture: actor identity (`UserId`), action type, outcome, and timestamp. Security event
  logs are distinct from the financial entity audit trail governed by Constitution Principle V.
- **FR-011**: When two requests arrive simultaneously with the same valid refresh token
  (benign race condition), the system MUST apply a first-wins policy: the first rotation
  succeeds; the duplicate receives HTTP 401 with no family invalidation. Family invalidation
  (FR-004a) applies only to replay attacks arriving after a rotation has already been
  durably committed.

### Key Entities

- **TokenRefreshRequest**: The payload submitted to request a new token pair. Fields:
  `accessToken` (the current or recently expired access token, required), `refreshToken` (the
  single-use rotating refresh token, required).
- **TokenRefreshResponse**: The payload returned on a successful token renewal. Matches the
  structural naming pattern of existing authentication contracts. Fields: `accessToken` (new
  signed JWT, required), `refreshToken` (new single-use rotating refresh token, required),
  `expiresAt` (ISO 8601 UTC expiry of the new access token, required).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with a valid refresh token can obtain a new access token and resume an
  expired session in under 1 second under normal operating conditions, without re-entering
  credentials.
- **SC-002**: A refresh token that has been used exactly once is permanently rejected on any
  subsequent submission, making replay attacks on consumed tokens impossible.
- **SC-003**: After a successful logout, 100% of subsequent requests using the invalidated
  token or its associated refresh token are rejected with HTTP 401.
- **SC-004**: All error scenarios (expired, revoked, malformed request, missing token) return
  RFC 7807-conformant responses without exposing internal state, stack traces, or whitelist
  implementation details.

## Assumptions

- Refresh token lifetime is substantially longer than access token lifetime (assumed 7 days vs.
  8 hours from Phase 1); exact durations remain server-side configuration values.
- Refresh token whitelist management (storage, expiry, revocation) is a server-side
  implementation concern; the spec does not prescribe the storage technology.
- Single-use rotation is the minimum required behavior; advanced reuse-detection (e.g., family
  invalidation that revokes all tokens in a lineage on reuse attempt) is out of scope for this
  phase and may be added in a security hardening pass.
- The `accessToken` field in `TokenRefreshRequest` is included for server-side binding
  validation (ensuring the access and refresh tokens belong to the same session); the exact
  validation logic is an implementation concern.
- Logout invalidates the server-side session or whitelist record tied to the token. If access
  tokens are stateless JWTs without a blacklist, they may remain technically valid until
  natural expiry — full access token blacklisting is out of scope for this phase.
- The logout endpoint does not support "logout all devices" (global session revocation); that
  is out of scope for Phase 1.
- HTTPS is enforced at the infrastructure level; refresh tokens and access tokens are never
  transmitted over unencrypted connections.
