# Quickstart Validation Guide: Phase 1 — Complete Authentication Lifecycle

**Feature**: `002-jwt-refresh-logout`
**Date**: 2026-06-13

This guide documents runnable validation scenarios that prove the feature works end-to-end.
For schema details see [`contracts/auth.md`](contracts/auth.md).
For entity definitions see [`data-model.md`](data-model.md).

---

## Prerequisites

### Backend
- .NET 8 SDK installed
- SQL Server instance accessible (local or Docker)
- Connection string configured in `backend/CorporateCashFlow/appsettings.Development.json`
- EF Core migration applied: `dotnet ef database update`
- At least one seed user in the `Users` table (see seed script below)

### Frontend
- Node.js 20+ installed
- `npm install` run in `frontend/`
- MSW service worker initialized: `npx msw init public --save`
- `VITE_API_BASE_URL` defaults to `/api` (Vite proxy to backend during dev)

### Seed Data (SQL Server)

```sql
-- Password: "S3cur3P@ss" — bcrypt hash for testing only
INSERT INTO Users (Id, Name, Email, PasswordHash, Role, SubsidiaryId, IsActive, CreatedAt)
VALUES (
    '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    'Jane Doe',
    'jane.doe@example.com',
    '$2a$12$exampleBcryptHashHere',   -- replace with actual bcrypt hash
    1,                                 -- Editor
    '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
    1,
    SYSDATETIMEOFFSET()
);

-- Global Admin with null SubsidiaryId
INSERT INTO Users (Id, Name, Email, PasswordHash, Role, SubsidiaryId, IsActive, CreatedAt)
VALUES (
    '1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d',
    'Admin User',
    'admin@example.com',
    '$2a$12$exampleAdminBcryptHashHere',
    0,     -- Admin
    NULL,  -- Global Admin
    1,
    SYSDATETIMEOFFSET()
);
```

---

## Running the Backend

```bash
cd backend/CorporateCashFlow
dotnet run
# API available at https://localhost:5001/api
```

## Running the Frontend (with MSW mocks — no backend required)

```bash
cd frontend
npm run dev
# App available at http://localhost:5173
# MSW intercepts all /api/* requests in development mode
```

---

## Scenario 1 — Successful Login

**Endpoint**: `POST /api/auth/login`
**Covers**: FR-001, FR-002 (login half), spec-001 US1

```bash
curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"jane.doe@example.com","password":"S3cur3P@ss"}' | jq .
```

**Expected response** (HTTP 200):
```json
{
  "token": "<signed-jwt>",
  "refreshToken": "<opaque-refresh-token>",
  "expiresAt": "2026-06-14T07:00:00Z"
}
```

**Verify**:
- `token` decodes to claims: `sub`, `role: "Editor"`, `subsidiary_id`, `jti`
- `refreshToken` is a non-empty opaque string
- `expiresAt` is ~8 hours from now
- `UserRefreshTokens` table has one new record with `IsRevoked = 0`
- `SecurityAuditLogs` has a `Login.Success` entry

---

## Scenario 2 — Login Failure (wrong password)

```bash
curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"jane.doe@example.com","password":"WrongPass1"}' | jq .
```

**Expected response** (HTTP 401):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password.",
  "instance": "/api/auth/login",
  "errors": null
}
```

**Verify**: `SecurityAuditLogs` has a `Login.Failure` entry.

---

## Scenario 3 — Retrieve User Context

**Endpoint**: `GET /api/auth/me`
**Covers**: FR-003, FR-004, spec-001 US2

```bash
ACCESS_TOKEN="<token from Scenario 1>"
curl -s https://localhost:5001/api/auth/me \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq .
```

**Expected response** (HTTP 200):
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Jane Doe",
  "email": "jane.doe@example.com",
  "role": "Editor",
  "subsidiaryId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"
}
```

**Global Admin variant** — repeat with Admin token; verify `subsidiaryId: null`.

---

## Scenario 4 — Silent Token Renewal

**Endpoint**: `POST /api/auth/refresh`
**Covers**: FR-001–FR-005 (refresh), spec-002 US1 acceptance scenarios 1

```bash
REFRESH_TOKEN="<refreshToken from Scenario 1>"
OLD_ACCESS_TOKEN="<token from Scenario 1>"

curl -s -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d "{\"accessToken\":\"$OLD_ACCESS_TOKEN\",\"refreshToken\":\"$REFRESH_TOKEN\"}" | jq .
```

**Expected response** (HTTP 200):
```json
{
  "accessToken": "<new-jwt>",
  "refreshToken": "<new-refresh-token>",
  "expiresAt": "<new-expiry>"
}
```

**Verify**:
- New `accessToken` has a new `jti` claim
- New `refreshToken` is different from the old one
- `UserRefreshTokens`: old record has `IsRevoked = 1`, new record has `IsRevoked = 0`
- `SecurityAuditLogs` has a `Refresh.Success` entry

---

## Scenario 5 — Replay Attack Detection (Family Invalidation)

**Covers**: FR-004a, spec-002 US1 acceptance scenario 2

```bash
# Use the OLD refresh token from Scenario 4 (already consumed)
curl -s -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d "{\"accessToken\":\"$OLD_ACCESS_TOKEN\",\"refreshToken\":\"$REFRESH_TOKEN\"}" | jq .
```

**Expected response** (HTTP 401):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Unauthorized",
  "status": 401,
  "detail": "The refresh token has already been used or revoked.",
  "instance": "/api/auth/refresh",
  "errors": null
}
```

**Verify**:
- ALL `UserRefreshTokens` for this user (same `FamilyId`) now have `IsRevoked = 1`
- `SecurityAuditLogs` has a `Refresh.Failure.Replay` entry
- Subsequent call with the new refresh token from Scenario 4 also returns 401

---

## Scenario 6 — Malformed Refresh Request (400)

**Covers**: FR-005, spec-002 US1 acceptance scenario 4

```bash
curl -s -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"accessToken":"some-token"}' | jq .
# Missing required field: refreshToken
```

**Expected response** (HTTP 400):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/auth/refresh",
  "errors": {
    "refreshToken": ["The refreshToken field is required."]
  }
}
```

---

## Scenario 7 — Explicit Logout

**Endpoint**: `POST /api/auth/logout`
**Covers**: FR-006–FR-008, spec-002 US2

```bash
# Obtain a fresh login first
NEW_TOKENS=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"jane.doe@example.com","password":"S3cur3P@ss"}')

NEW_ACCESS=$(echo $NEW_TOKENS | jq -r '.token')

# Logout
curl -s -o /dev/null -w "%{http_code}" \
  -X POST https://localhost:5001/api/auth/logout \
  -H "Authorization: Bearer $NEW_ACCESS"
# Expected: 204
```

**Verify**:
- Response is HTTP 204 with empty body
- `UserRefreshTokens` for this user: active token now has `IsRevoked = 1`
- `SecurityAuditLogs` has a `Logout.Success` entry

---

## Scenario 8 — Token Rejected After Logout

**Covers**: SC-003, spec-002 US2 acceptance scenario 2

```bash
# Immediately after Scenario 7 — use the same token
curl -s https://localhost:5001/api/auth/me \
  -H "Authorization: Bearer $NEW_ACCESS" | jq .
```

**Expected**: HTTP 401 with RFC 7807 body.

> **Note**: If access tokens are stateless JWTs and no blacklist is implemented, this scenario
> may return HTTP 200 until the token naturally expires. Full access token blacklisting is
> explicitly out of scope for Phase 1 (see spec assumptions). The refresh token IS revoked
> and any refresh attempt will return 401.

---

## Scenario 9 — Frontend MSW Smoke Test (No Backend)

1. Start frontend: `npm run dev`
2. Open `http://localhost:5173`
3. In DevTools → Network: confirm MSW service worker is active (`[MSW] Mocking enabled`)
4. Navigate to Login page
5. Submit `jane.doe@example.com` / `S3cur3P@ss`
6. Verify redirect to dashboard; user name and role displayed in navigation
7. Open DevTools → Application → Local Storage: confirm `token` and `refreshToken` are stored
8. Hard-refresh the page: verify session is restored via `GET /api/auth/me` (MSW handles it)
9. Trigger logout: verify redirect to `/login` and localStorage cleared

---

## Checklist Summary

| Scenario | Endpoint | Expected | Constitution Gate |
|----------|----------|----------|-------------------|
| 1. Successful login | `POST /login` | 200 + tokens | FR-001, FR-002 |
| 2. Login failure | `POST /login` | 401 RFC 7807 | FR-005 |
| 3. User context | `GET /me` | 200 + profile | FR-003, FR-004 |
| 4. Token renewal | `POST /refresh` | 200 + new tokens | FR-001–FR-003 |
| 5. Replay attack | `POST /refresh` | 401 + family revoked | FR-004a |
| 6. Malformed refresh | `POST /refresh` | 400 + errors map | FR-005 |
| 7. Logout | `POST /logout` | 204 No Content | FR-006–FR-008 |
| 8. Post-logout rejection | `GET /me` | 401 | SC-003 |
| 9. MSW frontend smoke | UI | Full lifecycle in browser | Constitution Principle I |
