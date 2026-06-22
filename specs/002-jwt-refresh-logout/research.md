# Research: Phase 1 — Complete Authentication Lifecycle

**Feature**: `002-jwt-refresh-logout`
**Date**: 2026-06-13
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## 1. Refresh Token Whitelist Storage

**Decision**: SQL Server table `UserRefreshTokens` via EF Core.

**Rationale**: Keeps whitelist state in the same transactional boundary as user data. No additional
infrastructure (Redis, Memcached) required. With a unique index on `TokenHash`, lookup is O(log n)
and satisfies the p95 < 500 ms SLA for Phase 1 load. Fail-closed behaviour (FR-004b) is naturally
satisfied: if SQL Server is unreachable, the EF Core call throws and the middleware returns HTTP 503.

**Alternatives considered**:
- **Redis**: Faster for hot-path token lookups but adds operational complexity (Redis availability,
  eviction policy, data durability). Out of scope for Phase 1.
- **In-memory (ConcurrentDictionary)**: Zero latency but lost on restart and not viable across
  multiple API replicas. Rejected — fails fail-closed requirement.

---

## 2. Race Condition on Concurrent Refresh Requests (FR-011)

**Decision**: Optimistic single-row atomic update — `ExecuteUpdateAsync WHERE Id = @id AND IsRevoked = 0`.
Check `rows affected`; if 0, the race was lost → return HTTP 401 with no family invalidation.

**Rationale**: The `UPDATE … WHERE IsRevoked = 0` is a CAS (Compare-And-Swap) at the database level.
SQL Server row-level locking ensures exactly one writer succeeds. No explicit `UPDLOCK` or
`SERIALIZABLE` isolation needed for Phase 1 traffic levels. Satisfies FR-011 first-wins policy.

**Alternatives considered**:
- **Pessimistic locking with `UPDLOCK`**: More reliable at extreme concurrency but requires Dapper
  raw SQL and explicit transactions. Over-engineered for Phase 1 — rejected.
- **Application-level mutex**: Not viable across multiple API replicas. Rejected.

---

## 3. Replay Attack Detection & Family Invalidation (FR-004a)

**Decision**: Each `UserRefreshToken` record stores a `FamilyId` (Guid). All tokens in a rotation
chain share the same `FamilyId`. On replay detection (a `IsRevoked = true` token is submitted),
the system issues `UPDATE UserRefreshTokens SET IsRevoked = 1 WHERE FamilyId = @familyId AND IsRevoked = 0`.

**Rationale**: Token family invalidation without a separate "session" table. The `FamilyId` lineage
is established at login (new random Guid) and inherited on each rotation — the new token keeps the
same `FamilyId`. Single compound index `(FamilyId, IsRevoked)` keeps the revocation query efficient.

**Alternatives considered**:
- **Separate `Sessions` table**: Cleaner conceptual model but doubles the table count and adds FK
  complexity for Phase 1. Deferred to a security hardening pass.
- **Linked-list via `ReplacedByTokenId`**: Allows auditing the rotation chain but does not simplify
  family invalidation (still need `FamilyId` for bulk revocation). Kept as an audit column.

---

## 4. JWT Generation & Validation (.NET 8)

**Decision**: `Microsoft.AspNetCore.Authentication.JwtBearer` (validation middleware) +
`System.IdentityModel.Tokens.Jwt` / `Microsoft.IdentityModel.Tokens` (manual token generation).

**Rationale**: Native .NET 8 support, no third-party auth library. JWT generation lives in
`AuthService` (business layer); the signing key is injected via `IOptions<JwtConfiguration>`,
keeping the business layer free of `IConfiguration` coupling.

**Claims on the access token**:
| Claim name | Value | Notes |
|---|---|---|
| `sub` | `UserId` (Guid) | Subject claim |
| `role` | `UserRole` enum string | Used by `[Authorize(Roles = ...)]` |
| `subsidiary_id` | `SubsidiaryId` (Guid or empty) | Null → Global Admin |
| `jti` | `Guid.NewGuid()` | Used for access-refresh token binding |

**Access Token Lifetime**: configurable via `Jwt:AccessTokenExpiryHours` (default: 8 h).
**Refresh Token Lifetime**: configurable via `Jwt:RefreshTokenExpiryDays` (default: 7 d).

**Alternatives considered**:
- **OpenIddict / IdentityServer**: Full-featured but massively over-scoped for manual JWT per
  constitution Principle III. Rejected.
- **ASP.NET Core Identity**: Adds User/Role management tables that conflict with the
  custom `Users` entity design. Rejected.

---

## 5. Access Token Binding in Refresh Requests

**Decision**: The `accessToken` in `TokenRefreshRequest` is parsed (without signature validation
if expired — `ValidateLifetime = false`) to extract its `jti` claim. This `jti` must match the
`AccessTokenJti` column stored in `UserRefreshTokens` for the submitted `refreshToken`.

**Rationale**: Prevents refresh tokens from being usable with access tokens from a different session
(cross-session binding attack). Satisfies the spec assumption on `accessToken` binding. Parsing an
expired JWT without lifetime validation is standard practice; structural/signature validation is
still performed.

**Alternatives considered**:
- **Ignore `accessToken` entirely**: Simpler but leaves the spec assumption unimplemented, which
  could allow cross-session token swapping. Rejected for security.
- **Require non-expired `accessToken`**: Contradicts the primary use-case (user refreshes *because*
  the access token is expired). Rejected.

---

## 6. Security Event Logging (FR-010)

**Decision**: `SecurityAuditLog` table with columns `UserId`, `Action`, `Outcome`, `IpAddress`,
`Detail`, `OccurredAt`. Written via `ISecurityAuditRepository` from `AuthService`. Kept separate
from the financial audit trail governed by `SaveChangesAsync` override (Constitution Principle V).

**Actions logged**:
| Action | Trigger |
|--------|---------|
| `Login.Success` | Successful credential verification |
| `Login.Failure` | Invalid email or password |
| `Refresh.Success` | Successful token rotation |
| `Refresh.Failure.Expired` | Expired/revoked refresh token |
| `Refresh.Failure.Replay` | Replay attack detected (family revoked) |
| `Refresh.Failure.Race` | Race condition — optimistic lock lost |
| `Refresh.Failure.Unavailable` | Whitelist store unreachable (503) |
| `Logout.Success` | Session invalidated |

**Rationale**: Distinct from financial audit trail; satisfies FR-010 without polluting the
EF Core `SaveChangesAsync` interceptor with security concerns.

---

## 7. MSW v2 Setup in Vite + React 19

**Decision**: MSW v2 browser integration. Service worker registered via `npx msw init public`.
Activation guarded by `import.meta.env.DEV` in `main.tsx`. Handlers co-located in
`src/mocks/handlers/authHandlers.ts`, composed in `src/mocks/browser.ts`.

**Rationale**: MSW v2 uses native Fetch API interception, compatible with Vite 8 + React 19 + Axios.
`onUnhandledRequest: 'bypass'` prevents false-positive warnings for Vite HMR and asset requests.

**Alternatives considered**:
- **Mirage.js**: Simpler API but does not support Service Worker-level interception; network tab
  shows fake responses. MSW preferred per constitution Principle I (OpenAPI-aligned mocking).
- **msw v1**: Deprecated; Node.js handler API differs from v2. Rejected.

---

## 8. Frontend Token Persistence & Silent Refresh

**Decision**: `localStorage` for token storage + Axios response interceptor for silent refresh.

**Rationale**: No SSR requirement; `localStorage` is sufficient for SPA token persistence. Axios
interceptor intercepts 401 responses, calls `POST /api/auth/refresh`, stores new tokens, and retries
the original request transparently. A `_retry` flag on the request config prevents infinite retry loops.

**HttpOnly cookie alternative**: More secure against XSS but requires CORS credential configuration
and same-site cookie policy — adds backend complexity outside Phase 1 scope. Recorded as a
security hardening backlog item.

---

## 9. Protected Route & Role-Based Navigation (React Router v6)

**Decision**: Wrapper component `<ProtectedRoute allowedRoles?>` that reads `AuthContext`.
Unauthenticated → redirect to `/login?redirect=<current path>`. Unauthorized role →
redirect to `/unauthorized`. Loading state prevents flash-of-redirect.

**Rationale**: Declarative, composable, idiomatic React Router v6 pattern. Role check in the
client is a UX guard only — server-side authorization (`[Authorize(Roles = ...)]`) remains the
authoritative enforcement point.
