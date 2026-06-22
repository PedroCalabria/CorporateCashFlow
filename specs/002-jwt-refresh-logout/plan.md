# Implementation Plan: Phase 1 — Complete Authentication Lifecycle

**Branch**: `002-jwt-refresh-logout` | **Date**: 2026-06-13 | **Spec**: [spec.md](spec.md)

**Incremental scope spans**: [001 spec](../001-foundation-auth-rbac/spec.md) (Login + Me)
and [002 spec](spec.md) (Refresh + Logout).

**Feature Scope**: Login (`POST /auth/login`), User Context (`GET /auth/me`),
Token Rotation (`POST /auth/refresh`), Secure Logout (`POST /auth/logout`).

---

## Summary

Implements the complete authentication lifecycle for the Corporate Cash Flow platform. The backend
exposes four endpoints governed by a server-side rotating refresh token whitelist stored in SQL
Server. Replay attacks trigger family invalidation (all tokens in the rotation chain revoked);
a first-wins optimistic lock handles benign race conditions. The frontend feature module lives
under `/src/features/auth`, uses MSW v2 for isolated development, Axios interceptors for silent
renewal, TanStack Query for server state, and React Router v6 protected routes with role-based
access control.

---

## Technical Context

**Language/Version**: Backend .NET 8 Web API; Frontend TypeScript ~6.x (React 19 + Vite 8)

**Primary Dependencies**:
- Backend: `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`,
  EF Core 8, Dapper, `BCrypt.Net-Next`
- Frontend: TanStack Query v5, Axios, React Router DOM v6, MSW v2, React Hook Form,
  Zod, `@hookform/resolvers`, Tailwind CSS, Shadcn/ui

**Storage**: SQL Server — tables `Users`, `UserRefreshTokens`, `SecurityAuditLogs`
(see [`data-model.md`](data-model.md))

**Testing**: xUnit (backend), Vitest + Testing Library (frontend), MSW v2 (frontend mock layer)

**Target Platform**: Web (responsive desktop / tablet / mobile)

**Performance Goals**:
- Token refresh p95 < 500 ms (SQL Server index on `TokenHash`)
- Login p95 < 2 s (bcrypt + single DB round-trip)
- `GET /auth/me` p95 < 100 ms (JWT claim decode + Dapper point-read)

**Constraints**: OpenAPI contract-first; DIP enforced; RBAC; automated security audit log (FR-010)

**Scale/Scope**: Corporate Cash Flow & Treasury Management — multi-subsidiary isolation via
`SubsidiaryId` JWT claim

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post-design — all gates confirmed.*

- [x] **OpenAPI contract**: All 4 endpoints defined in `/specs/openapi.yaml`. One pending
  amendment: add `refreshToken` field to `LoginResponse` schema (see `contracts/auth.md`).
- [x] **`SubsidiaryId` isolation**: Carried in JWT claims; propagated to all downstream modules.
  `UserRefreshTokens` stores `UserId`; subsidiary filtering is enforced by application layer.
- [x] **RBAC roles mapped**: `GET /auth/me` returns `role` claim; `POST /auth/logout` requires
  `BearerAuth`; `[Authorize(Roles = ...)]` enforced in controllers; `UserRole` enum in Entity layer.
- [x] **CQS split**: Login/Refresh/Logout → writes via EF Core; `GET /auth/me` → pure JWT claim
  decode + Dapper read (no write). Security audit writes via EF Core in separate `DbContext` call.
- [x] **Audit trail**: `SecurityAuditLog` entity captures refresh attempts, logout, and replay
  events (FR-010). Distinct from financial audit trail; written in `AuthService`, not via
  `SaveChangesAsync` override.
- [x] **MSW handlers**: All 4 endpoints have MSW handler stubs planned in
  `src/mocks/handlers/authHandlers.ts` covering success + all error variants.
- [x] **Frontend feature folder**: `/src/features/auth`; URL state: `?redirect=` query param
  for post-login navigation; no subsidiary filter sync required for auth endpoints.
- [x] **DIP**: `CorporateCashFlow.Business` defines `IAuthService`, `IRefreshTokenRepository`,
  `ISecurityAuditRepository`; `CorporateCashFlow.Repository.Imp` implements them.
  `CorporateCashFlow.Business.csproj` has ZERO references to EF Core, Dapper, or Controllers.
- [x] **Lean controllers**: `AuthController` calls `IAuthService` only; zero DB / JWT logic.

---

## Project Structure

### Documentation (this feature)

```text
specs/002-jwt-refresh-logout/
├── plan.md          ← this file
├── research.md      ← Phase 0 decisions
├── data-model.md    ← entity schemas + EF Core config
├── quickstart.md    ← runnable validation scenarios
├── contracts/
│   └── auth.md      ← OpenAPI-derived contract reference
└── tasks.md         ← Phase 2 output (/speckit-tasks — not yet generated)
```

### Source Code

```text
backend/
├── CorporateCashFlow.Entity/
│   ├── DTOs/
│   │   ├── LoginRequestDto.cs
│   │   ├── LoginResponseDto.cs
│   │   ├── TokenRefreshRequestDto.cs
│   │   ├── TokenRefreshResponseDto.cs
│   │   ├── UserContextResponseDto.cs
│   │   └── ErrorResponseDto.cs
│   ├── Enums/
│   │   └── UserRole.cs
│   ├── User.cs
│   ├── UserRefreshToken.cs
│   └── SecurityAuditLog.cs
│
├── CorporateCashFlow.Business/
│   └── IBusiness/
│       ├── IAuthService.cs
│       ├── IRefreshTokenRepository.cs
│       └── ISecurityAuditRepository.cs
│
├── CorporateCashFlow.Business.Imp/
│   └── Business/
│       └── AuthService.cs
│
├── CorporateCashFlow.Repository.Imp/
│   ├── Context/
│   │   └── AppDbContext.cs
│   ├── Mapping/
│   │   ├── UserEntityTypeConfiguration.cs
│   │   ├── UserRefreshTokenEntityTypeConfiguration.cs
│   │   └── SecurityAuditLogEntityTypeConfiguration.cs
│   └── Repository/
│       ├── RefreshTokenRepository.cs
│       ├── SecurityAuditRepository.cs
│       └── UserRepository.cs               ← Dapper read for /me
│
└── CorporateCashFlow/
    ├── Controllers/
    │   └── AuthController.cs
    ├── Configurations/
    │   └── JwtConfiguration.cs
    ├── Middlewares/
    │   └── GlobalExceptionMiddleware.cs
    └── Startup.cs

frontend/
└── src/
    ├── features/
    │   └── auth/
    │       ├── api/
    │       │   └── authService.ts
    │       ├── hooks/
    │       │   ├── useLogin.ts
    │       │   ├── useCurrentUser.ts
    │       │   ├── useRefreshToken.ts
    │       │   └── useLogout.ts
    │       ├── components/
    │       │   ├── LoginForm.tsx
    │       │   └── ProtectedRoute.tsx
    │       └── types/
    │           └── auth.types.ts
    ├── mocks/
    │   ├── handlers/
    │   │   ├── authHandlers.ts
    │   │   └── index.ts
    │   └── browser.ts
    └── lib/
        └── axios.ts
```

**Structure Decision**: Enterprise monorepo layout per Constitution Principles II and IV.
Backend uses existing `CorporateCashFlow.*` project naming; the frontend feature folder
`/src/features/auth` is new in this phase.

---

## Complexity Tracking

*No Constitution violations requiring justification.*

---

## Implementation Checklist

> Ordered chronologically by dependency. Backend items must be completed in steps 1 → 4.
> Frontend items must be completed in steps 1 → 4. Backend and Frontend steps can proceed
> in parallel after Backend Step 1 (DB schema) and Frontend Step 1 (MSW setup) are complete.

---

### BACKEND

#### Step 1 — Database Schema & EF Core Mapping

*Target*: `CorporateCashFlow.Entity` + `CorporateCashFlow.Repository.Imp`

- [ ] **B1.1** Define `User` entity class in `CorporateCashFlow.Entity`:
  `Id`, `Name`, `Email`, `PasswordHash`, `Role` (UserRole enum), `SubsidiaryId?`,
  `IsActive`, `CreatedAt`, `UpdatedAt?`, `RefreshTokens` navigation
- [ ] **B1.2** Define `UserRefreshToken` entity class in `CorporateCashFlow.Entity`:
  `Id`, `UserId`, `TokenHash`, `AccessTokenJti`, `FamilyId`, `ExpiresAt`, `IsRevoked`,
  `CreatedAt`, `RevokedAt?`, `ReplacedByTokenId?`, navigation props
  (see `data-model.md` for full field specs)
- [ ] **B1.3** Define `SecurityAuditLog` entity class in `CorporateCashFlow.Entity`:
  `Id` (long), `UserId?`, `Action`, `Outcome`, `IpAddress?`, `Detail?`, `OccurredAt`
- [ ] **B1.4** Add `UserRole` enum in `CorporateCashFlow.Entity/Enums/`: `Admin=0`, `Editor=1`, `Auditor=2`
- [ ] **B1.5** Create `UserEntityTypeConfiguration` in `CorporateCashFlow.Repository.Imp/Mapping/`:
  unique index on `Email`, `HasMaxLength` on string columns
- [ ] **B1.6** Create `UserRefreshTokenEntityTypeConfiguration`:
  unique index on `TokenHash`; FK to `Users` (cascade delete); self-ref FK for `ReplacedByTokenId`
  (no-action); filtered composite indexes on `(FamilyId, IsRevoked)` and `(UserId, IsRevoked)`
- [ ] **B1.7** Create `SecurityAuditLogEntityTypeConfiguration`:
  identity PK (`UseIdentityColumn`), no FK constraint on `UserId` (soft reference)
- [ ] **B1.8** Register all three entity type configurations in `AppDbContext.OnModelCreating`
- [ ] **B1.9** Add project reference: `CorporateCashFlow.Repository.Imp` → `CorporateCashFlow.Entity`
  (if not already present)
- [ ] **B1.10** Run `dotnet ef migrations add InitAuth --project CorporateCashFlow.Repository.Imp`
  and verify generated SQL matches `data-model.md` schema definitions
- [ ] **B1.11** Run `dotnet ef database update` and insert seed users for validation
  (see `quickstart.md` seed script)

---

#### Step 2 — Business Layer (Interfaces + DTOs + Service Implementation)

*Target*: `CorporateCashFlow.Entity/DTOs` + `CorporateCashFlow.Business/IBusiness` +
`CorporateCashFlow.Business.Imp/Business`

- [ ] **B2.1** Add DTOs in `CorporateCashFlow.Entity/DTOs/` matching OpenAPI schemas exactly:
  - `LoginRequestDto` (`Email`, `Password`)
  - `LoginResponseDto` (`Token`, `RefreshToken`, `ExpiresAt`) — includes `RefreshToken`
    per `contracts/auth.md` amendment
  - `TokenRefreshRequestDto` (`AccessToken`, `RefreshToken`)
  - `TokenRefreshResponseDto` (`AccessToken`, `RefreshToken`, `ExpiresAt`)
  - `UserContextResponseDto` (`Id`, `Name`, `Email`, `Role`, `SubsidiaryId?`)
  - `ErrorResponseDto` (`Type`, `Title`, `Status`, `Detail`, `Instance`, `Errors?`)
- [ ] **B2.2** Define `IAuthService` in `CorporateCashFlow.Business/IBusiness/`:

  ```csharp
  Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string ipAddress);
  Task<UserContextResponseDto> GetCurrentUserAsync(Guid userId);
  Task<TokenRefreshResponseDto> RefreshTokenAsync(TokenRefreshRequestDto request, string ipAddress);
  Task LogoutAsync(Guid userId, string ipAddress);
  ```

- [ ] **B2.3** Define `IRefreshTokenRepository` in `CorporateCashFlow.Business/IBusiness/`:

  ```csharp
  Task<UserRefreshToken?> GetByTokenHashAsync(string tokenHash);
  Task<int> ConsumeAndRotateAsync(Guid oldTokenId, UserRefreshToken newToken);
  Task RevokeAllByFamilyAsync(Guid familyId);
  Task RevokeAllByUserIdAsync(Guid userId);
  Task AddAsync(UserRefreshToken token);
  ```

- [ ] **B2.4** Define `ISecurityAuditRepository` in `CorporateCashFlow.Business/IBusiness/`:

  ```csharp
  Task LogAsync(Guid? userId, string action, string outcome, string? ipAddress, string? detail);
  ```

- [ ] **B2.5** Implement `AuthService` in `CorporateCashFlow.Business.Imp/Business/AuthService.cs`:
  - `LoginAsync`: look up user by email (Dapper) → bcrypt verify → generate access JWT
    (claims: `sub`, `role`, `subsidiary_id`, `jti` = `Guid.NewGuid()`) → generate raw refresh
    token (`Guid.NewGuid().ToString("N")`) → compute SHA-256 hash → persist `UserRefreshToken`
    (new `FamilyId`) → log `Login.Success` → return `LoginResponseDto`
  - On bcrypt failure: log `Login.Failure` → throw domain exception → 401
  - `GetCurrentUserAsync`: Dapper `SELECT Name, Email FROM Users WHERE Id = @id` → map to DTO
  - `RefreshTokenAsync`:
    1. SHA-256 hash the submitted `refreshToken`
    2. `GetByTokenHashAsync` — not found → log `Refresh.Failure.Expired` → throw 401
    3. If `IsRevoked = true` → log `Refresh.Failure.Replay` → `RevokeAllByFamilyAsync` → throw 401
    4. If `ExpiresAt < UtcNow` → log `Refresh.Failure.Expired` → throw 401
    5. Parse submitted `accessToken` (no lifetime validation) → extract `jti` →
       verify matches `AccessTokenJti` stored in record
    6. `ConsumeAndRotateAsync` (atomic CAS) → if `rowsAffected = 0` →
       log `Refresh.Failure.Race` → throw 401 (no family invalidation)
    7. Generate new access JWT + new refresh token (same `FamilyId`, new `Id`) → log
       `Refresh.Success` → return DTO
  - `LogoutAsync`: `RevokeAllByUserIdAsync` → log `Logout.Success`
- [ ] **B2.6** **DIP gate**: verify `CorporateCashFlow.Business.csproj` and
  `CorporateCashFlow.Business.Imp.csproj` have ZERO `<PackageReference>` entries for
  `Microsoft.EntityFrameworkCore`, `Dapper`, or any web framework package

---

#### Step 3 — Infrastructure Layer (Repository Implementations)

*Target*: `CorporateCashFlow.Repository.Imp/Repository/`

- [ ] **B3.1** Implement `RefreshTokenRepository : IRefreshTokenRepository`:
  - `GetByTokenHashAsync`: EF Core `FirstOrDefaultAsync(t => t.TokenHash == hash)`
  - `ConsumeAndRotateAsync`: EF Core `ExecuteUpdateAsync WHERE Id = @id AND IsRevoked = false`
    (set `IsRevoked = true`, `RevokedAt = now`, `ReplacedByTokenId = newToken.Id`) +
    `AddAsync(newToken)` in same `SaveChangesAsync` call → return `ExecuteUpdate` rows affected
  - `RevokeAllByFamilyAsync`: EF Core `ExecuteUpdateAsync WHERE FamilyId = @id AND IsRevoked = false`
  - `RevokeAllByUserIdAsync`: EF Core `ExecuteUpdateAsync WHERE UserId = @id AND IsRevoked = false`
  - `AddAsync`: `AddAsync + SaveChangesAsync`
- [ ] **B3.2** Implement `SecurityAuditRepository : ISecurityAuditRepository`:
  - `LogAsync`: create `SecurityAuditLog` entity → `AddAsync + SaveChangesAsync`
  - Use `try/catch` internally — audit write failure must NEVER propagate to caller
- [ ] **B3.3** Implement `UserRepository` (Dapper, for `GET /auth/me` reads):
  - `GetByIdAsync(Guid id)`: Dapper `QuerySingleOrDefaultAsync<User>(sql, new { id })` via
    `IDbConnection` (injected from `IDbConnectionFactory` or `SqlConnection` directly)
- [ ] **B3.4** Add `IDbConnectionFactory` or register `SqlConnection` as scoped in `Startup.cs`
  for Dapper injection
- [ ] **B3.5** Add project reference: `CorporateCashFlow.Repository.Imp` →
  `CorporateCashFlow.Business` (for interface resolution)
- [ ] **B3.6** Add NuGet packages to `CorporateCashFlow.Repository.Imp`:
  `Microsoft.EntityFrameworkCore.SqlServer`, `Dapper`
- [ ] **B3.7** Add NuGet packages to `CorporateCashFlow.Business.Imp`:
  `BCrypt.Net-Next`, `Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt`
- [ ] **B3.8** Register DI bindings in `Startup.cs`:
  - `IAuthService → AuthService` (Scoped)
  - `IRefreshTokenRepository → RefreshTokenRepository` (Scoped)
  - `ISecurityAuditRepository → SecurityAuditRepository` (Scoped)
  - `IUserRepository → UserRepository` (Scoped)

---

#### Step 4 — Presentation Layer (Controllers + JWT + Middleware)

*Target*: `CorporateCashFlow/` (API host)

- [ ] **B4.1** Create `JwtConfiguration` POCO in `Configurations/`:
  properties `Secret`, `Issuer`, `Audience`, `AccessTokenExpiryHours` (int), `RefreshTokenExpiryDays` (int)
- [ ] **B4.2** Bind `JwtConfiguration` from `appsettings.json` section `"Jwt"` in `Startup.cs`
  via `services.Configure<JwtConfiguration>(configuration.GetSection("Jwt"))`
- [ ] **B4.3** Add `"Jwt"` section to `appsettings.json` (placeholder values) and override
  in `appsettings.Development.json` with local dev secret
- [ ] **B4.4** Register `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`
  in `Startup.cs` with `TokenValidationParameters`:
  `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all `true`
- [ ] **B4.5** Implement `GlobalExceptionMiddleware` in `Middlewares/`:
  - Wrap `await _next(context)` in `try/catch`
  - Map exception types to RFC 7807 `ErrorResponseDto` responses:
    - Unhandled → 500, title `"Internal Server Error"`, no stack trace in body
    - Domain `UnauthorizedException` → 401
    - Domain `ValidationException` → 400 with `errors` map
    - Domain `ServiceUnavailableException` → 503
  - Set `Content-Type: application/problem+json` on all error responses
- [ ] **B4.6** Register `GlobalExceptionMiddleware` as the first middleware in the pipeline
  in `Startup.cs` (before `UseAuthentication`, `UseAuthorization`, `UseRouting`)
- [ ] **B4.7** Create lean `AuthController` in `Controllers/` with route `[Route("api/auth")]`:
  - `POST login` → `[AllowAnonymous]` → validate `LoginRequestDto` (DataAnnotations or FluentValidation)
    → `await _authService.LoginAsync(dto, GetIpAddress())` → return `Ok(result)`
  - `GET me` → `[Authorize]` → extract `UserId` from `User.FindFirst("sub")` →
    `await _authService.GetCurrentUserAsync(userId)` → return `Ok(result)`
  - `POST refresh` → `[AllowAnonymous]` → validate `TokenRefreshRequestDto` →
    `await _authService.RefreshTokenAsync(dto, GetIpAddress())` → return `Ok(result)`
  - `POST logout` → `[Authorize]` → extract `UserId` + IP →
    `await _authService.LogoutAsync(userId, GetIpAddress())` → return `NoContent()`
  - Private `GetIpAddress()` helper: `HttpContext.Connection.RemoteIpAddress?.ToString()`
- [ ] **B4.8** Add project references in `CorporateCashFlow.API.csproj`:
  `CorporateCashFlow.Business`, `CorporateCashFlow.Business.Imp`, `CorporateCashFlow.Repository.Imp`
- [ ] **B4.9** Add NuGet packages to `CorporateCashFlow.API`:
  `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] **B4.10** Update `openapi.yaml` to add `refreshToken` field to `LoginResponse` schema
  (required amendment per `contracts/auth.md`)
- [ ] **B4.11** Build and run; execute all 9 quickstart scenarios from `quickstart.md`

---

### FRONTEND

#### Step 1 — MSW Mock Layer

*Target*: `frontend/src/mocks/`

- [ ] **F1.1** Install MSW v2: `npm install msw --save-dev`
- [ ] **F1.2** Initialize service worker: `npx msw init public --save`
  (creates `public/mockServiceWorker.js`)
- [ ] **F1.3** Create `src/mocks/handlers/authHandlers.ts` with handlers for all 4 endpoints:
  - `http.post('/api/auth/login', ...)`:
    - Match `jane.doe@example.com` / `S3cur3P@ss` → 200 with `LoginResponse` shape
      (include `refreshToken` per contract amendment)
    - Match missing/invalid email format → 400 with `errors` map
    - Any other credentials → 401
  - `http.get('/api/auth/me', ...)`:
    - Valid Authorization header → 200 with `UserContextResponse` (Editor + subsidiaryId)
    - Missing/expired header → 401
  - `http.post('/api/auth/refresh', ...)`:
    - Known valid refresh token → 200 with `TokenRefreshResponse`
    - Missing `refreshToken` field → 400 with `errors.refreshToken`
    - Unknown or revoked refresh token → 401
  - `http.post('/api/auth/logout', ...)`:
    - Valid Authorization header → 204
    - Missing/invalid token → 401
- [ ] **F1.4** Create `src/mocks/handlers/index.ts` barrel: `export const handlers = [...authHandlers]`
- [ ] **F1.5** Create `src/mocks/browser.ts`:
  `export const worker = setupWorker(...handlers)`
- [ ] **F1.6** Integrate MSW in `src/main.tsx` with dev guard:

  ```typescript
  if (import.meta.env.DEV) {
    const { worker } = await import('./mocks/browser')
    await worker.start({ onUnhandledRequest: 'bypass' })
  }
  ```

- [ ] **F1.7** Smoke-test: run `npm run dev`, open browser, confirm `[MSW] Mocking enabled`
  in DevTools console

---

#### Step 2 — Axios Client & Bearer Token Interceptor

*Target*: `frontend/src/lib/axios.ts`

- [ ] **F2.1** Install Axios: `npm install axios`
- [ ] **F2.2** Create `src/lib/axios.ts`:
  - `baseURL` from `import.meta.env.VITE_API_BASE_URL ?? '/api'`
  - Request interceptor: read `token` from `localStorage` → attach `Authorization: Bearer <token>`
    if present
  - Response interceptor: on `401` response AND `!config._retry`:
    1. Set `config._retry = true`
    2. Call `POST /api/auth/refresh` with stored `{ accessToken, refreshToken }`
    3. On success: update `localStorage` with new tokens → retry original request
    4. On refresh failure: clear `localStorage` → `window.location.replace('/login')`
  - Guard refresh calls (path = `/auth/refresh`) from the retry interceptor to avoid loops

---

#### Step 3 — TypeScript Types + Service Client + TanStack Query Hooks

*Target*: `frontend/src/features/auth/`

- [ ] **F3.1** Install TanStack Query v5: `npm install @tanstack/react-query @tanstack/react-query-devtools`
- [ ] **F3.2** Wrap `App.tsx` root with `<QueryClientProvider client={queryClient}>`
  and add `<ReactQueryDevtools initialIsOpen={false} />` (dev only)
- [ ] **F3.3** Create `src/features/auth/types/auth.types.ts` with TypeScript interfaces
  matching OpenAPI schemas exactly (see `contracts/auth.md`):
  - `LoginRequest`, `LoginResponse` (with `refreshToken`), `TokenRefreshRequest`,
    `TokenRefreshResponse`, `UserContextResponse`, `ErrorResponse`
  - `UserRole` union type: `'Admin' | 'Editor' | 'Auditor'`
- [ ] **F3.4** Create `src/features/auth/api/authService.ts` — thin Axios wrappers
  using the configured `axiosInstance` from `src/lib/axios.ts`:
  - `login(req: LoginRequest): Promise<LoginResponse>`
  - `getCurrentUser(): Promise<UserContextResponse>`
  - `refreshToken(req: TokenRefreshRequest): Promise<TokenRefreshResponse>`
  - `logout(): Promise<void>` (handles 204 No Content)
- [ ] **F3.5** Create `src/features/auth/hooks/useLogin.ts`:
  `useMutation` wrapping `authService.login` → on success: store `token`, `refreshToken`,
  `expiresAt` in `localStorage` → `queryClient.invalidateQueries(['currentUser'])`
- [ ] **F3.6** Create `src/features/auth/hooks/useCurrentUser.ts`:
  `useQuery({ queryKey: ['currentUser'], queryFn: authService.getCurrentUser,
  enabled: !!localStorage.getItem('token'), staleTime: 5 * 60 * 1000 })`
- [ ] **F3.7** Create `src/features/auth/hooks/useRefreshToken.ts`:
  `useMutation` wrapping `authService.refreshToken` → on success: update tokens in `localStorage`
  (Note: primary silent refresh path is the Axios interceptor in Step 2; this hook covers
  explicit/manual refresh scenarios)
- [ ] **F3.8** Create `src/features/auth/hooks/useLogout.ts`:
  `useMutation` wrapping `authService.logout` → on success: clear `localStorage` →
  `queryClient.clear()` → `navigate('/login')`

---

#### Step 4 — UI State, Protected Routes & Role-Based Navigation

*Target*: `frontend/src/features/auth/components/` + `src/App.tsx`

- [ ] **F4.1** Install React Router v6: `npm install react-router-dom`
- [ ] **F4.2** Install form + validation deps: `npm install react-hook-form zod @hookform/resolvers`
- [ ] **F4.3** Install Tailwind CSS + Shadcn/ui per project setup guide (if not yet initialized)
- [ ] **F4.4** Create `AuthContext` (React Context) providing:
  `{ user: UserContextResponse | null, isAuthenticated: boolean, isLoading: boolean }`
  — sourced from `useCurrentUser` query result; exposed via `useAuthContext` hook
- [ ] **F4.5** Create `ProtectedRoute` component (`src/features/auth/components/ProtectedRoute.tsx`):
  - Read `{ isAuthenticated, isLoading, user }` from `AuthContext`
  - If `isLoading` → render `<LoadingSpinner />` (prevents flash-of-redirect)
  - If not authenticated → `<Navigate to="/login" state={{ redirect: location.pathname }} />`
  - If `allowedRoles` prop provided AND `user.role` not in list →
    `<Navigate to="/unauthorized" replace />`
  - Otherwise → `<Outlet />`
- [ ] **F4.6** Create `LoginForm` component (`src/features/auth/components/LoginForm.tsx`):
  - React Hook Form + Zod schema: `email` (required, email format), `password` (required, min 8)
  - On submit: call `useLogin` mutation
  - On 401: display RFC 7807 `detail` as inline form error
  - On 400: map `errors` object keys to per-field React Hook Form `setError` calls
  - On success: read `location.state?.redirect ?? '/dashboard'` → `navigate(redirect)`
  - Tailwind CSS + Shadcn/ui `<Input>`, `<Button>`, `<Label>`, `<Alert>` components
- [ ] **F4.7** Wire up routing in `App.tsx` with `<BrowserRouter>`:
  - Public routes: `/login` → `<LoginForm />`
  - Protected (all authenticated roles): `/dashboard` → placeholder
  - Protected (Admin only): `/admin` → `<ProtectedRoute allowedRoles={['Admin']} />`
  - Fallback: `*` → redirect to `/login`
  - Wrap authenticated subtree with `<AuthContextProvider>` and `<ProtectedRoute>`
- [ ] **F4.8** Run MSW frontend smoke test (Scenario 9 in `quickstart.md`):
  login → dashboard redirect → session restore on refresh → logout → `/login`

---

## Design Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Research | `specs/002-jwt-refresh-logout/research.md` | ✅ Generated |
| Data Model | `specs/002-jwt-refresh-logout/data-model.md` | ✅ Generated |
| Auth Contracts | `specs/002-jwt-refresh-logout/contracts/auth.md` | ✅ Generated |
| Quickstart | `specs/002-jwt-refresh-logout/quickstart.md` | ✅ Generated |
| Tasks | `specs/002-jwt-refresh-logout/tasks.md` | ⏳ Pending (`/speckit-tasks`) |

---

## OpenAPI Amendment Required (Pre-Implementation Gate)

> **Before any implementation begins**, update `specs/openapi.yaml` to add `refreshToken`
> to the `LoginResponse` schema. This is a Constitution Principle I gate — contract must be
> updated first, then backend and MSW mock both emit the field. See `contracts/auth.md` for
> the exact YAML fragment to add.
