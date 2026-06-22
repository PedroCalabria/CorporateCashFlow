# Auth Contracts: Phase 1 — Complete Authentication Lifecycle

**Feature**: `002-jwt-refresh-logout`
**Source of truth**: [`/specs/openapi.yaml`](../../openapi.yaml) — all contracts are derived from
and must stay in sync with that file. In case of any discrepancy, `openapi.yaml` governs.

---

## Endpoints Summary

| Method | Path | Auth | Success | Purpose |
|--------|------|------|---------|---------|
| `POST` | `/api/auth/login` | None | `200 LoginResponse` | Credential authentication + JWT issuance |
| `GET` | `/api/auth/me` | Bearer | `200 UserContextResponse` | Session context resolution |
| `POST` | `/api/auth/refresh` | None | `200 TokenRefreshResponse` | Rotating token renewal |
| `POST` | `/api/auth/logout` | Bearer | `204 No Content` | Session invalidation |

---

## Request / Response Schemas

### `LoginRequest`
→ `POST /api/auth/login` body

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `email` | `string (email)` | Yes | Valid email format |
| `password` | `string` | Yes | Minimum 8 characters |

### `LoginResponse`
← `POST /api/auth/login` 200

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `token` | `string` | Yes | Signed JWT; claims: `sub` (UserId), `role`, `subsidiary_id`, `jti` |
| `expiresAt` | `string (date-time)` | Yes | ISO 8601 UTC expiry of the access token |

> **Note**: `LoginResponse` does NOT include a `refreshToken` field. The refresh token is returned
> separately only from `POST /api/auth/refresh`. On login, the refresh token is issued server-side
> and must be stored securely by the client (returned in `TokenRefreshResponse` schema on first
> refresh). **Correction to spec assumption**: To allow the client to perform its first refresh,
> the login response must include the initial refresh token. The backend MUST return an initial
> refresh token alongside the access token at login time, even though `LoginResponse` in the
> current `openapi.yaml` schema only defines `token` and `expiresAt`.
>
> **Resolution**: A `refreshToken` field will be added to `LoginResponse` in `openapi.yaml`
> as part of this implementation. Pending contract update before implementation (Constitution
> Principle I gate). Until updated, the frontend MSW mock MUST include `refreshToken` in
> login responses to enable the full auth lifecycle.

---

### `TokenRefreshRequest`
→ `POST /api/auth/refresh` body

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `accessToken` | `string` | Yes | Current or recently expired access token (for session binding) |
| `refreshToken` | `string` | Yes | Single-use rotating refresh token |

### `TokenRefreshResponse`
← `POST /api/auth/refresh` 200

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `accessToken` | `string` | Yes | New signed JWT access token |
| `refreshToken` | `string` | Yes | New single-use rotating refresh token (previous one invalidated) |
| `expiresAt` | `string (date-time)` | Yes | ISO 8601 UTC expiry of the new access token |

---

### `UserContextResponse`
← `GET /api/auth/me` 200

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | `string (uuid)` | Yes | Authenticated user's UUID |
| `name` | `string` | Yes | Display name |
| `email` | `string (email)` | Yes | Email address |
| `role` | `string (enum)` | Yes | One of: `Admin`, `Editor`, `Auditor` |
| `subsidiaryId` | `string (uuid) \| null` | Yes | `null` for Global Admin accounts |

---

### `ErrorResponse` (RFC 7807 Problem Details)
← All `400`, `401`, `503` error responses

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `type` | `string (uri)` | No | Problem type URI |
| `title` | `string` | No | Short problem summary |
| `status` | `integer` | No | HTTP status code mirror |
| `detail` | `string` | No | Human-readable occurrence explanation |
| `instance` | `string (uri)` | No | Request path (e.g., `/api/auth/refresh`) |
| `errors` | `object \| null` | No | Field-level validation map (400 responses only) |

---

## Error Response Matrix

### `POST /api/auth/login`

| Scenario | HTTP | `title` | `detail` |
|----------|------|---------|---------|
| Invalid email format or password < 8 chars | `400` | `Validation Failed` | `One or more validation errors occurred.` + `errors` map |
| Wrong email or password | `401` | `Unauthorized` | `Invalid email or password.` |
| Server fault | `500` | `Internal Server Error` | Generic; no stack trace |

### `GET /api/auth/me`

| Scenario | HTTP | `detail` |
|----------|------|---------|
| Missing `Authorization` header | `401` | `Authorization token is required.` |
| Expired token | `401` | `The provided token has expired.` |
| Invalid/tampered token | `401` | `The provided token is invalid.` |

### `POST /api/auth/refresh`

| Scenario | HTTP | `detail` |
|----------|------|---------|
| Missing `refreshToken` or `accessToken` field | `400` | `One or more validation errors occurred.` + `errors` map |
| Token expired | `401` | `The refresh token has expired.` |
| Token revoked or already consumed | `401` | `The refresh token has already been used or revoked.` |
| Race condition — optimistic lock lost | `401` | `The refresh token has already been used or revoked.` |
| Whitelist store unavailable | `503` | `The token validation service is temporarily unavailable.` |

### `POST /api/auth/logout`

| Scenario | HTTP | `detail` |
|----------|------|---------|
| Missing or invalid token | `401` | `Authorization token is required.` |
| Already logged out / invalid session | `401` | `The provided token is invalid or the session has already been terminated.` |

---

## Security Scheme

```yaml
BearerAuth:
  type: http
  scheme: bearer
  bearerFormat: JWT
```

JWT claims carried in every access token:
- `sub`: `UserId` (UUID string)
- `role`: `UserRole` string (`Admin` | `Editor` | `Auditor`)
- `subsidiary_id`: Subsidiary UUID string, or `""` / absent for Global Admin
- `jti`: Unique token identifier (UUID string) — used for access-refresh session binding
- `exp`: Unix timestamp expiry

---

## OpenAPI Contract Change Required

> **Action item before implementation begins** (Constitution Principle I gate):
>
> The `LoginResponse` schema in `specs/openapi.yaml` must be updated to include:
> ```yaml
> refreshToken:
>   type: string
>   description: Initial single-use rotating refresh token issued at login.
>   example: "aW5pdGlhbFJlZnJlc2hUb2tlbg..."
> ```
> This is required to complete the full authentication lifecycle. The MSW mock and backend
> `AuthService.LoginAsync` both emit this field. Absence of this field would break the client's
> ability to perform its first token refresh.
