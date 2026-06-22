# Tasks: Phase 1 — Complete Authentication Lifecycle

**Branch**: `002-jwt-refresh-logout`
**Spec**: [spec.md](spec.md) + [../001-foundation-auth-rbac/spec.md](../001-foundation-auth-rbac/spec.md)
**Plan**: [plan.md](plan.md)
**Design artifacts**: [research.md](research.md) · [data-model.md](data-model.md) · [contracts/auth.md](contracts/auth.md) · [quickstart.md](quickstart.md)

---

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies in progress)
- **[USx]**: User story this task belongs to
- File paths are relative to repository root

---

## User Stories

| ID | Story | Priority | Endpoint |
|----|-------|----------|---------|
| US1 | User Login | P1 | `POST /api/auth/login` |
| US2 | Session Context | P2 | `GET /api/auth/me` |
| US3 | Silent Token Renewal | P1 (spec-002) | `POST /api/auth/refresh` |
| US4 | Explicit Logout | P2 (spec-002) | `POST /api/auth/logout` |

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Install all packages and create directory scaffolding before any source code is written.
No user story can begin until this phase is complete.

- [X] T001 Add NuGet packages to `backend/CorporateCashFlow.Business.Imp/CorporateCashFlow.Business.Imp.csproj`: `BCrypt.Net-Next`, `Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt`
- [X] T002 [P] Add NuGet packages to `backend/CorporateCashFlow.Repository.Imp/CorporateCashFlow.Repository.Imp.csproj`: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Dapper`
- [X] T003 [P] Add NuGet package to `backend/CorporateCashFlow/CorporateCashFlow.API.csproj`: `Microsoft.AspNetCore.Authentication.JwtBearer`
- [X] T004 [P] Add project references in `backend/CorporateCashFlow/CorporateCashFlow.API.csproj` → `CorporateCashFlow.Business`, `CorporateCashFlow.Business.Imp`, `CorporateCashFlow.Repository.Imp`; add `CorporateCashFlow.Business.Imp` → `CorporateCashFlow.Business`; add `CorporateCashFlow.Repository.Imp` → `CorporateCashFlow.Business`
- [X] T005 [P] Install frontend runtime dependencies in `frontend/`: `npm install axios @tanstack/react-query @tanstack/react-query-devtools react-router-dom react-hook-form zod @hookform/resolvers`
- [X] T006 [P] Install frontend dev dependency in `frontend/`: `npm install msw --save-dev`; then run `npx msw init public --save` to generate `frontend/public/mockServiceWorker.js`
- [X] T006b Initialize Tailwind CSS and Shadcn/ui in `frontend/` — **MUST complete before T041 (LoginForm)**:
  1. Install: `npm install tailwindcss @tailwindcss/vite`
  2. Add `@tailwindcss/vite` plugin to `frontend/vite.config.ts`
  3. Add `@import "tailwindcss";` to `frontend/src/index.css` (replace existing content)
  4. Run `npx shadcn-ui@latest init` (accept defaults: TypeScript, default style, CSS variables); this creates `frontend/components.json` and `frontend/src/lib/utils.ts`
  5. Add first Shadcn components needed by LoginForm: `npx shadcn-ui@latest add button input label alert`
  6. Verify `frontend/src/components/ui/` contains `button.tsx`, `input.tsx`, `label.tsx`, `alert.tsx`

**Checkpoint**: All packages installed, project references wired, MSW worker file present, Tailwind CSS processed by Vite, Shadcn/ui components available.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database schema, EF Core mappings, shared interfaces, JWT config, DI wiring, Axios
client, TanStack Query provider, and MSW scaffolding. **All user stories are blocked until this phase is complete.**

### Backend — Entities & EF Core

- [X] T007 [P] Create `UserRole` enum in `backend/CorporateCashFlow.Entity/Enums/UserRole.cs`: `Admin = 0`, `Editor = 1`, `Auditor = 2`
- [X] T008 [P] Create `User` entity class in `backend/CorporateCashFlow.Entity/User.cs`: `Id` (Guid), `Name`, `Email`, `PasswordHash`, `Role` (UserRole), `SubsidiaryId?` (Guid?), `IsActive` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt?`, `RefreshTokens` nav collection
- [X] T009 [P] Create `UserRefreshToken` entity class in `backend/CorporateCashFlow.Entity/UserRefreshToken.cs`: `Id`, `UserId`, `TokenHash`, `AccessTokenJti`, `FamilyId`, `ExpiresAt`, `IsRevoked`, `CreatedAt`, `RevokedAt?`, `ReplacedByTokenId?`, `User` and `ReplacedBy` nav props (see `data-model.md` for full field specs)
- [X] T010 [P] Create `SecurityAuditLog` entity class in `backend/CorporateCashFlow.Entity/SecurityAuditLog.cs`: `Id` (long), `UserId?` (Guid?), `Action`, `Outcome`, `IpAddress?`, `Detail?`, `OccurredAt` (DateTimeOffset)
- [X] T011 Create `UserEntityTypeConfiguration` in `backend/CorporateCashFlow.Repository.Imp/Mapping/UserEntityTypeConfiguration.cs`: `ToTable("Users")`, unique index on `Email`, `HasMaxLength` per `data-model.md`
- [X] T012 [P] Create `UserRefreshTokenEntityTypeConfiguration` in `backend/CorporateCashFlow.Repository.Imp/Mapping/UserRefreshTokenEntityTypeConfiguration.cs`: unique index on `TokenHash`; FK to Users (cascade delete); self-ref FK for `ReplacedByTokenId` (no-action); filtered composite indexes on `(FamilyId, IsRevoked)` and `(UserId, IsRevoked)` per `data-model.md`
- [X] T013 [P] Create `SecurityAuditLogEntityTypeConfiguration` in `backend/CorporateCashFlow.Repository.Imp/Mapping/SecurityAuditLogEntityTypeConfiguration.cs`: `UseIdentityColumn()` on `Id`; no FK on `UserId` (soft reference); `HasMaxLength` per `data-model.md`
- [X] T014 Create `AppDbContext` in `backend/CorporateCashFlow.Repository.Imp/Context/AppDbContext.cs`: `DbSet<User>`, `DbSet<UserRefreshToken>`, `DbSet<SecurityAuditLog>`; apply all three configurations in `OnModelCreating`
- [X] T015 Run EF Core migration from repo root: `dotnet ef migrations add InitAuth --project backend/CorporateCashFlow.Repository.Imp --startup-project backend/CorporateCashFlow`; verify generated SQL matches `data-model.md`; run `dotnet ef database update`; then insert seed users for quickstart validation — **generate real bcrypt hashes before running the INSERT**: use a temporary `dotnet-script` or a one-liner in the `Program.cs` entry point during dev: `Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("S3cur3P@ss", 12));` — copy the output hash into the seed INSERT replacing the placeholder in `quickstart.md`; without a real hash, quickstart Scenario 1 will fail at runtime even though the migration succeeds

### Backend — Interfaces & Domain Exceptions

- [X] T016 [P] Create `IAuthService` interface in `backend/CorporateCashFlow.Business/IBusiness/IAuthService.cs`: `LoginAsync(LoginRequestDto, string ipAddress)`, `GetCurrentUserAsync(Guid userId)`, `RefreshTokenAsync(TokenRefreshRequestDto, string ipAddress)`, `LogoutAsync(Guid userId, string ipAddress)`
- [X] T017 [P] Create `IRefreshTokenRepository` interface in `backend/CorporateCashFlow.Business/IBusiness/IRefreshTokenRepository.cs`: `GetByTokenHashAsync`, `ConsumeAndRotateAsync`, `RevokeAllByFamilyAsync`, `RevokeAllByUserIdAsync`, `AddAsync` (see `plan.md` B2.3 for full signatures)
- [X] T018 [P] Create `ISecurityAuditRepository` interface in `backend/CorporateCashFlow.Business/IBusiness/ISecurityAuditRepository.cs`: `LogAsync(Guid? userId, string action, string outcome, string? ipAddress, string? detail)`
- [X] T019 [P] Create `IUserRepository` interface in `backend/CorporateCashFlow.Business/IBusiness/IUserRepository.cs`: `GetByEmailAsync(string email)`, `GetByIdAsync(Guid id)`
- [X] T020 [P] Create domain exception classes in `backend/CorporateCashFlow.Business/Exceptions/`: `UnauthorizedException.cs`, `ValidationException.cs` (with `Dictionary<string, string[]> Errors` property), `ServiceUnavailableException.cs`

### Backend — Configuration & Middleware

- [X] T021 [P] Create `JwtConfiguration` POCO in `backend/CorporateCashFlow/Configurations/JwtConfiguration.cs`: `Secret`, `Issuer`, `Audience`, `AccessTokenExpiryHours` (int, default 8), `RefreshTokenExpiryDays` (int, default 7)
- [X] T022 Add `"Jwt"` section with placeholder values to `backend/CorporateCashFlow/appsettings.json`; add real dev secret to `backend/CorporateCashFlow/appsettings.Development.json`
- [X] T023 Implement `GlobalExceptionMiddleware` in `backend/CorporateCashFlow/Middlewares/GlobalExceptionMiddleware.cs`: catch `UnauthorizedException` → 401, `ValidationException` → 400 with `errors` map, `ServiceUnavailableException` → 503, unhandled → 500; all responses use RFC 7807 shape and `Content-Type: application/problem+json`; no stack trace in body
- [X] T024 Wire DI and middleware pipeline in `backend/CorporateCashFlow/Startup.cs`: `services.Configure<JwtConfiguration>(...)`; `AddAuthentication().AddJwtBearer(...)` with `TokenValidationParameters`; **`AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)`** (fixes FR-006/FR-009 — .NET 8 default is PascalCase); `UseMiddleware<GlobalExceptionMiddleware>()` first in pipeline; `UseAuthentication()`; `UseAuthorization()`; register `IAuthService → AuthService`, `IRefreshTokenRepository → RefreshTokenRepository`, `ISecurityAuditRepository → SecurityAuditRepository`, `IUserRepository → UserRepository` (all Scoped); register `IDbConnection` as **Transient** via `services.AddTransient<IDbConnection>(_ => new SqlConnection(configuration.GetConnectionString("Default")))` (Transient scope is required — Scoped or Singleton causes thread-safety issues under concurrent load)

### Frontend — Shared Infrastructure

- [X] T025 [P] Create `frontend/src/features/auth/types/auth.types.ts`: `LoginRequest`, `LoginResponse` (with `refreshToken`), `TokenRefreshRequest`, `TokenRefreshResponse`, `UserContextResponse`, `ErrorResponse` interfaces matching `contracts/auth.md`; `UserRole = 'Admin' | 'Editor' | 'Auditor'` union type; **also export `localStorage` key constants** used by all auth hooks and the Axios interceptor — `export const AUTH_TOKEN_KEY = 'token'`, `export const AUTH_REFRESH_TOKEN_KEY = 'refreshToken'`, `export const AUTH_EXPIRES_AT_KEY = 'expiresAt'`; note: `LoginResponse.token` and `TokenRefreshResponse.accessToken` are different field names in the API but must both be stored under `AUTH_TOKEN_KEY` in localStorage
- [X] T026 Create `frontend/src/lib/axios.ts`: `axiosInstance` with `baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api'`; import `AUTH_TOKEN_KEY`, `AUTH_REFRESH_TOKEN_KEY`, `AUTH_EXPIRES_AT_KEY` from `auth.types.ts` and use exclusively — **never use string literals for key names**; request interceptor reads `localStorage.getItem(AUTH_TOKEN_KEY)` → attaches `Authorization: Bearer <token>` if present; response interceptor intercepts 401 (non-refresh path), reads `{ accessToken: localStorage.getItem(AUTH_TOKEN_KEY), refreshToken: localStorage.getItem(AUTH_REFRESH_TOKEN_KEY) }`, calls `POST /auth/refresh`, on success stores `response.data.accessToken` under `AUTH_TOKEN_KEY` + `response.data.refreshToken` under `AUTH_REFRESH_TOKEN_KEY` + `response.data.expiresAt` under `AUTH_EXPIRES_AT_KEY`, retries original request with `_retry = true` guard; on refresh failure clears all three keys and redirects to `/login`
- [X] T027 [P] Add `QueryClientProvider` wrapper and `<ReactQueryDevtools initialIsOpen={false} />` (dev only) to `frontend/src/App.tsx`
- [X] T028 [P] Create `frontend/src/mocks/browser.ts`: `export const worker = setupWorker(...handlers)` (handlers imported from index)
- [X] T029 [P] Create `frontend/src/mocks/handlers/index.ts`: barrel export `export const handlers = [...authHandlers]`
- [X] T030 Integrate MSW in `frontend/src/main.tsx` with `if (import.meta.env.DEV)` guard, `await worker.start({ onUnhandledRequest: 'bypass' })`; run `npm run dev` and confirm `[MSW] Mocking enabled` in console

**Checkpoint**: Database migrated, all interfaces defined, JWT + DI configured, Axios + QueryClient + MSW scaffolded. User story implementation can now begin.

---

## Phase 3: User Story 1 — User Login (Priority: P1) 🎯 MVP

**Goal**: A registered user submits email + password and receives a signed JWT access token,
a rotating refresh token, and an expiry timestamp. Errors return RFC 7807 responses.

**Independent Test**: `POST https://localhost:5001/api/auth/login` with valid credentials returns
HTTP 200 with `token`, `refreshToken`, `expiresAt`. Invalid credentials return 401. Missing
fields return 400 with `errors` map. See quickstart.md Scenarios 1–2.

### Backend — US1

> **Contract-first gate (Constitution Principle I)**: T037 MUST be completed before T032, T035, T036, T038, T040, and T041. The OpenAPI contract is the single source of truth; backend DTOs and MSW mocks both derive from it.

- [X] T037 [US1] Update `specs/openapi.yaml` `LoginResponse` schema: add `refreshToken` (type: string, required, description and example per `contracts/auth.md`) — **complete this task first; all subsequent US1 tasks depend on it**
- [X] T031 [P] [US1] Create `LoginRequestDto` in `backend/CorporateCashFlow.Entity/DTOs/LoginRequestDto.cs`: `Email` (string, `[Required][EmailAddress]`), `Password` (string, `[Required][MinLength(8)]`)
- [X] T032 [P] [US1] Create `LoginResponseDto` in `backend/CorporateCashFlow.Entity/DTOs/LoginResponseDto.cs`: `Token` (string), `RefreshToken` (string), `ExpiresAt` (DateTimeOffset) — includes `RefreshToken` per amended OpenAPI contract (T037)
- [X] T033 [P] [US1] Create `ErrorResponseDto` in `backend/CorporateCashFlow.Entity/DTOs/ErrorResponseDto.cs`: `Type`, `Title`, `Status` (int), `Detail`, `Instance`, `Errors` (`Dictionary<string, string[]>?`)
- [X] T034 [US1] Implement `UserRepository` in `backend/CorporateCashFlow.Repository.Imp/Repository/UserRepository.cs` implementing `IUserRepository`: `GetByEmailAsync` and `GetByIdAsync` via Dapper `QuerySingleOrDefaultAsync` using injected `IDbConnection`
- [X] T035 [US1] Create `AuthService` class and implement `LoginAsync` in `backend/CorporateCashFlow.Business.Imp/Business/AuthService.cs`:
  - **Class scaffold (do this first)**: define `public class AuthService : IAuthService` with a constructor accepting and storing four private readonly dependencies: `IUserRepository`, `IRefreshTokenRepository`, `ISecurityAuditRepository`, `IOptions<JwtConfiguration>` — subsequent tasks (T044, T054, T060) add methods to this same file and depend on these fields being initialized
  - **`LoginAsync` method**: look up user by email via `IUserRepository` → if not found throw `UnauthorizedException`; bcrypt verify password → on failure log `Login.Failure` + throw `UnauthorizedException`; generate access JWT (claims: `sub`, `role`, `subsidiary_id`, `jti = Guid.NewGuid()`); generate raw refresh token (`Guid.NewGuid().ToString("N")`); SHA-256 hash it; persist new `UserRefreshToken` (new `FamilyId`) via `IRefreshTokenRepository.AddAsync`; log `Login.Success`; return `LoginResponseDto`
  - **Stub remaining interface methods** (`GetCurrentUserAsync`, `RefreshTokenAsync`, `LogoutAsync`) with `throw new NotImplementedException()` so the class compiles before US2–US4 fill them in
- [X] T036 [US1] Create `AuthController` in `backend/CorporateCashFlow/Controllers/AuthController.cs` with `[Route("api/auth")]`; add `POST login` action: `[AllowAnonymous]`, validate `LoginRequestDto` via `[FromBody]` + `ModelState`, call `_authService.LoginAsync(dto, GetIpAddress())`, return `Ok(result)`; add private `GetIpAddress()` helper

### Frontend — US1

- [X] T038 [US1] Create `frontend/src/features/auth/api/authService.ts`: `login(req: LoginRequest): Promise<LoginResponse>` calling `axiosInstance.post('/auth/login', req)` via `frontend/src/lib/axios.ts`
- [X] T039 [US1] Create `frontend/src/features/auth/hooks/useLogin.ts`: `useMutation` wrapping `authService.login`; on success store `token`, `refreshToken`, `expiresAt` in `localStorage`; invalidate `['currentUser']` query
- [X] T040 [US1] Add `POST /api/auth/login` handler to `frontend/src/mocks/handlers/authHandlers.ts`: success 200 for `jane.doe@example.com`/`S3cur3P@ss` (include `refreshToken`); 400 for missing/invalid email; 401 for any other credentials
- [X] T041 [US1] Create `LoginForm` component in `frontend/src/features/auth/components/LoginForm.tsx`: React Hook Form + Zod schema (`email` required email, `password` required min 8); call `useLogin` mutation on submit; on 401 show RFC 7807 `detail` as form-level alert; on 400 map `errors` keys to `setError` per-field; on success navigate to `location.state?.redirect ?? '/dashboard'`; use Tailwind CSS + Shadcn/ui `Input`, `Button`, `Label`, `Alert`
- [X] T042 [US1] Add `/login` route in `frontend/src/App.tsx` wrapped in `<BrowserRouter>` rendering `<LoginForm />`

**Checkpoint**: US1 complete — login works end-to-end on backend; MSW mock returns correct login response; `LoginForm` renders, validates, and stores token.

---

## Phase 4: User Story 2 — Session Context Resolution (Priority: P2)

**Goal**: An authenticated user calls `GET /api/auth/me` with a Bearer token and receives their
full identity profile (`id`, `name`, `email`, `role`, `subsidiaryId`). Missing or expired tokens
return 401.

**Independent Test**: `GET /api/auth/me` with the token from US1 returns HTTP 200 with profile.
Missing header returns 401. See quickstart.md Scenario 3.

### Backend — US2

- [X] T043 [P] [US2] Create `UserContextResponseDto` in `backend/CorporateCashFlow.Entity/DTOs/UserContextResponseDto.cs`: `Id` (Guid), `Name`, `Email`, `Role` (string), `SubsidiaryId` (Guid?)
- [X] T044 [US2] Implement `AuthService.GetCurrentUserAsync` in `backend/CorporateCashFlow.Business.Imp/Business/AuthService.cs`: call `IUserRepository.GetByIdAsync(userId)` → if null throw `UnauthorizedException`; map to `UserContextResponseDto` (Role → enum.ToString())
- [X] T045 [US2] Add `GET me` action to `backend/CorporateCashFlow/Controllers/AuthController.cs`: `[Authorize]`, extract `UserId` from `User.FindFirst("sub")?.Value`, parse to Guid, call `_authService.GetCurrentUserAsync(userId)`, return `Ok(result)`

### Frontend — US2

- [X] T046 [P] [US2] Add `getCurrentUser(): Promise<UserContextResponse>` to `frontend/src/features/auth/api/authService.ts` calling `axiosInstance.get('/auth/me')`
- [X] T047 [US2] Create `frontend/src/features/auth/hooks/useCurrentUser.ts`: `useQuery({ queryKey: ['currentUser'], queryFn: authService.getCurrentUser, enabled: !!localStorage.getItem('token'), staleTime: 5 * 60 * 1000 })`
- [X] T048 [US2] Create `AuthContext` in `frontend/src/features/auth/components/AuthContext.tsx`: `AuthContextProvider` wrapping `useCurrentUser`; expose `{ user, isAuthenticated, isLoading }` via `useAuthContext()` hook
- [X] T049 [US2] Add `GET /api/auth/me` handler to `frontend/src/mocks/handlers/authHandlers.ts`: 200 with full `UserContextResponse` for valid Authorization header (Editor user + subsidiaryId); second mock for Global Admin (role Admin, subsidiaryId null); 401 for missing/expired token

**Checkpoint**: US2 complete — `GET /me` works; `useCurrentUser` populates `AuthContext` on app load; MSW mock handles both user variants.

---

## Phase 5: User Story 3 — Silent Token Renewal (Priority: P1 from spec-002)

**Goal**: A user with an expired access token presents their refresh token to `POST /api/auth/refresh`
and receives a new access token + new refresh token without re-entering credentials. Replay attacks
trigger family invalidation. Race conditions apply first-wins with no family invalidation.

**Independent Test**: Login → call `POST /api/auth/refresh` → verify new tokens returned and old
refresh token rejected. Submit the consumed token a second time → verify 401 + all family tokens
revoked. See quickstart.md Scenarios 4–6.

### Backend — US3

- [X] T050 [P] [US3] Create `TokenRefreshRequestDto` in `backend/CorporateCashFlow.Entity/DTOs/TokenRefreshRequestDto.cs`: `AccessToken` (string, `[Required]`), `RefreshToken` (string, `[Required]`)
- [X] T051 [P] [US3] Create `TokenRefreshResponseDto` in `backend/CorporateCashFlow.Entity/DTOs/TokenRefreshResponseDto.cs`: `AccessToken` (string), `RefreshToken` (string), `ExpiresAt` (DateTimeOffset)
- [X] T052 [US3] Implement `RefreshTokenRepository` in `backend/CorporateCashFlow.Repository.Imp/Repository/RefreshTokenRepository.cs` implementing `IRefreshTokenRepository`:
  - `GetByTokenHashAsync`: EF Core `FirstOrDefaultAsync(t => t.TokenHash == hash)` with `Include(t => t.User)`
  - `ConsumeAndRotateAsync`: EF Core `ExecuteUpdateAsync WHERE Id = @id AND IsRevoked = false` (set `IsRevoked = true`, `RevokedAt = now`, `ReplacedByTokenId = newToken.Id`) + `AddAsync(newToken)` + `SaveChangesAsync`; return rows-affected (0 = race lost)
  - `RevokeAllByFamilyAsync`: `ExecuteUpdateAsync WHERE FamilyId = @familyId AND IsRevoked = false` (set `IsRevoked = true`, `RevokedAt = now`)
  - `RevokeAllByUserIdAsync`: `ExecuteUpdateAsync WHERE UserId = @userId AND IsRevoked = false` (set `IsRevoked = true`, `RevokedAt = now`)
  - `AddAsync`: `dbContext.UserRefreshTokens.AddAsync(token)` + `SaveChangesAsync`
- [X] T053 [US3] Implement `SecurityAuditRepository` in `backend/CorporateCashFlow.Repository.Imp/Repository/SecurityAuditRepository.cs` implementing `ISecurityAuditRepository`: `LogAsync` creates `SecurityAuditLog` entity + `AddAsync` + `SaveChangesAsync` wrapped in `try/catch` — audit failure must NEVER propagate to caller
- [X] T054 [US3] Implement `AuthService.RefreshTokenAsync` in `backend/CorporateCashFlow.Business.Imp/Business/AuthService.cs` (7-step state machine wrapped in DB-availability guard):
  - **Outer guard (FR-004b fail-closed)**: wrap steps 2–6 in `try/catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or System.Data.Common.DbException)` → log `Refresh.Failure.Unavailable` → throw `ServiceUnavailableException("Token validation service temporarily unavailable")`; this maps to HTTP 503 via `GlobalExceptionMiddleware`
  1. SHA-256 hash submitted `refreshToken`
  2. `GetByTokenHashAsync` — if null: log `Refresh.Failure.Expired` → throw `UnauthorizedException`
  3. If `IsRevoked = true`: log `Refresh.Failure.Replay` → `RevokeAllByFamilyAsync(record.FamilyId)` → throw `UnauthorizedException`
  4. If `record.ExpiresAt < DateTimeOffset.UtcNow`: log `Refresh.Failure.Expired` → throw `UnauthorizedException`
  5. Parse `accessToken` with `ValidateLifetime = false` → extract `jti`; if `jti != record.AccessTokenJti` throw `UnauthorizedException`; if `accessToken` is structurally invalid (not parseable) also throw `UnauthorizedException`
  6. `ConsumeAndRotateAsync(record.Id, newTokenEntity)` → if rowsAffected = 0: log `Refresh.Failure.Race` → throw `UnauthorizedException` (no family invalidation — FR-011)
  7. Generate new access JWT (new `jti`) + new raw refresh token → log `Refresh.Success` → return `TokenRefreshResponseDto`
- [X] T055 [US3] Add `POST refresh` action to `backend/CorporateCashFlow/Controllers/AuthController.cs`: `[AllowAnonymous]`, validate `TokenRefreshRequestDto`, call `_authService.RefreshTokenAsync(dto, GetIpAddress())`, return `Ok(result)`

### Frontend — US3

- [X] T056 [P] [US3] Add `refreshToken(req: TokenRefreshRequest): Promise<TokenRefreshResponse>` to `frontend/src/features/auth/api/authService.ts`
- [X] T057 [P] [US3] Create `frontend/src/features/auth/hooks/useRefreshToken.ts`: `useMutation` wrapping `authService.refreshToken`; on success update `token`, `refreshToken`, `expiresAt` in `localStorage`
- [X] T058 [US3] Add `POST /api/auth/refresh` handler to `frontend/src/mocks/handlers/authHandlers.ts`: 200 with `TokenRefreshResponse` for known refresh token; 400 with `errors.refreshToken` for missing field; 401 for unknown/revoked token; track consumed tokens in handler state (e.g., `Set<string>`) to simulate single-use enforcement; note: race condition (FR-011 first-wins) cannot be simulated by MSW — validated on the live backend only (see Phase 5 checkpoint note)
- [X] T059 [US3] Verify Axios 401 interceptor in `frontend/src/lib/axios.ts` correctly triggers `refreshToken()` on 401 responses from non-refresh endpoints, retries the original request, and navigates to `/login` on refresh failure

**Checkpoint**: US3 complete — `POST /auth/refresh` full state machine works (happy path, replay, race condition, expiry, bad binding); frontend silent refresh fires automatically via Axios interceptor.

> **Race condition validation gap (FR-011)**: The first-wins race condition path (rowsAffected = 0 → HTTP 401, **no** family invalidation) is implemented in T052/T054 but cannot be exercised by the MSW handler in T058 (which is single-threaded). To validate this path on the backend, use a `Task.WhenAll` test: fire two simultaneous `POST /auth/refresh` requests with the same refresh token and assert exactly one returns 200 and one returns 401, then confirm all active tokens of the family remain intact (not revoked). Add this as a manual verification step during T071 (quickstart sign-off).

---

## Phase 6: User Story 4 — Explicit Session Logout (Priority: P2 from spec-002)

**Goal**: An authenticated user calls `POST /api/auth/logout` with a Bearer token; all active
refresh tokens for that user are immediately revoked; subsequent requests with any of those tokens
return 401. Response is HTTP 204 No Content.

**Independent Test**: Login → `POST /api/auth/logout` → verify 204 → verify refresh token from login
returns 401 on `POST /auth/refresh`. See quickstart.md Scenarios 7–8.

### Backend — US4

- [X] T060 [US4] Implement `AuthService.LogoutAsync` in `backend/CorporateCashFlow.Business.Imp/Business/AuthService.cs`: call `IRefreshTokenRepository.RevokeAllByUserIdAsync(userId)`; log `Logout.Success` via `ISecurityAuditRepository.LogAsync`
- [X] T061 [US4] Add `POST logout` action to `backend/CorporateCashFlow/Controllers/AuthController.cs`: `[Authorize]`, extract `UserId` from `User.FindFirst("sub")`, call `_authService.LogoutAsync(userId, GetIpAddress())`, return `NoContent()`

### Frontend — US4

- [X] T062 [P] [US4] Add `logout(): Promise<void>` to `frontend/src/features/auth/api/authService.ts`: `axiosInstance.post('/auth/logout')` (expects 204)
- [X] T063 [P] [US4] Create `frontend/src/features/auth/hooks/useLogout.ts`: `useMutation` wrapping `authService.logout`; on success clear `localStorage` (token, refreshToken, expiresAt) → `queryClient.clear()` → `navigate('/login')`
- [X] T064 [US4] Add `POST /api/auth/logout` handler to `frontend/src/mocks/handlers/authHandlers.ts`: 204 for valid Authorization header; 401 for missing/invalid token
- [X] T065 [US4] Create `ProtectedRoute` component in `frontend/src/features/auth/components/ProtectedRoute.tsx`: read `{ isAuthenticated, isLoading, user }` from `AuthContext`; if `isLoading` render `<LoadingSpinner />`; if not authenticated `<Navigate to="/login" state={{ redirect: location.pathname }} />`; if `allowedRoles` prop provided and `user.role` not in list `<Navigate to="/unauthorized" replace />`; otherwise `<Outlet />`

**Checkpoint**: US4 complete — logout revokes all refresh tokens; ProtectedRoute blocks unauthenticated access.

> **Known limitation (SC-003)**: SC-003 states "100% of subsequent requests … are rejected with HTTP 401." This guarantee is fully met for the **refresh token path** — `POST /auth/refresh` returns 401 after logout. However, **stateless access tokens remain valid until their natural `expiresAt`** because full access token blacklisting is explicitly out of scope for Phase 1 (spec-002 Assumptions). `GET /auth/me` will continue returning 200 for an unexpired access token even after logout. Full invalidation of access tokens requires a server-side blacklist and is a Phase 2 security hardening backlog item.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: DIP gate verification, full routing, error standardization validation, and end-to-end
quickstart sign-off.

- [X] T066 Verify DIP gate: confirm `backend/CorporateCashFlow.Business/CorporateCashFlow.Business.csproj` and `backend/CorporateCashFlow.Business.Imp/CorporateCashFlow.Business.Imp.csproj` have **zero** `<PackageReference>` entries for `Microsoft.EntityFrameworkCore`, `Dapper`, or any ASP.NET Core web framework package
- [X] T066b Document out-of-scope edge cases in `specs/002-jwt-refresh-logout/spec.md` Edge Cases section — add "Resolved (out of scope)" markers for: **(a)** JWT signing key rotation: the ASP.NET Core `JwtBearer` middleware returns HTTP 401 for signature mismatches — this is the correct and intended behaviour for Phase 1; a dedicated key-rotation strategy is a Phase 2 security hardening item; **(b)** post-logout valid access token: see Phase 6 US4 checkpoint note — stateless JWT remains valid until `expiresAt`; full blacklisting is Phase 2 scope
- [X] T067 [P] Complete routing in `frontend/src/App.tsx`: add `<AuthContextProvider>` wrapping all routes; add protected `/dashboard` route (all authenticated roles); add protected `/admin` route (`allowedRoles={['Admin']}`); add `/unauthorized` fallback page; add `*` catch-all redirect to `/login`
- [X] T068 [P] Wire `useLogout` into a header/nav component (placeholder): render user `name` + `role` and a Logout button calling `useLogout().mutate()`; place in dashboard layout placeholder
- [X] T069 [P] Run `dotnet build` from `backend/` and resolve any compiler errors (missing references, interface mismatches, null-safety warnings)
- [X] T070 [P] Run `npm run build` from `frontend/` and resolve any TypeScript compilation errors
- [X] T071 Run quickstart.md Scenarios 1–8 against the live backend (`dotnet run`); record pass/fail for each scenario
- [X] T072 [P] Run quickstart.md Scenario 9 (MSW frontend smoke test) against `npm run dev`; verify full login → dashboard → session restore → logout cycle in browser

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 completion — **BLOCKS all user stories**
- **Phase 3 (US1 Login)**: Depends on Phase 2
- **Phase 4 (US2 Me)**: Depends on Phase 2; integrates with `UserRepository` from US1 (T034)
- **Phase 5 (US3 Refresh)**: Depends on Phase 2; requires `AuthService` constructor scaffolding from US1 (T035)
- **Phase 6 (US4 Logout)**: Depends on Phase 2; requires `RefreshTokenRepository` from US3 (T052)
- **Phase 7 (Polish)**: Depends on all user story phases complete

### User Story Dependencies

- **US1 (P1 — Login)**: First to implement — establishes `AuthService`, `AuthController`, `UserRepository`, MSW login handler
- **US2 (P2 — Me)**: Can start after Phase 2; lightly depends on US1's `UserRepository` (T034)
- **US3 (P1 — Refresh)**: Can start after Phase 2; depends on `AuthService` shell from US1 (T035) for `RefreshTokenAsync` implementation
- **US4 (P2 — Logout)**: Depends on `RefreshTokenRepository.RevokeAllByUserIdAsync` from US3 (T052)

### Backend–Frontend Parallelism

Once Phase 2 is complete, backend and frontend work within each story can proceed in parallel:

| Story | Backend tracks | Frontend tracks |
|-------|---------------|-----------------|
| US1 | T037 (first) → T031–T036 | T038–T042 (after T037) |
| US2 | T043–T045 | T046–T049 |
| US3 | T050–T055 | T056–T059 |
| US4 | T060–T061 | T062–T065 |

---

## Parallel Execution Examples

### Phase 2 — Foundational (run concurrently)

```
Agent A: T007 UserRole + T008 User entity + T009 UserRefreshToken entity + T010 SecurityAuditLog entity
Agent B: T016 IAuthService + T017 IRefreshTokenRepository + T018 ISecurityAuditRepository + T019 IUserRepository
Agent C: T021 JwtConfiguration + T022 appsettings + T025 auth.types.ts + T026 axios.ts
```

### Phase 3 — US1 Login (run concurrently after Phase 2)

```
# T037 (openapi.yaml) MUST complete first — blocks all tracks below
Sequential: T037 openapi.yaml update

Agent A (Backend): T031 LoginRequestDto → T032 LoginResponseDto → T034 UserRepository → T035 AuthService.LoginAsync → T036 AuthController POST login
Agent B (Frontend): T038 authService.ts → T039 useLogin.ts → T040 MSW login handler → T041 LoginForm → T042 routing
```

### Phase 5 — US3 Refresh (run concurrently after Phase 2 + US1 shell)

```
Agent A (Backend): T050 DTOs → T052 RefreshTokenRepository → T054 AuthService.RefreshTokenAsync → T055 AuthController POST refresh
Agent B (Backend): T053 SecurityAuditRepository (can be built in parallel with T052)
Agent C (Frontend): T056 authService → T057 useRefreshToken → T058 MSW handler → T059 Axios interceptor verification
```

---

## Implementation Strategy

### MVP First (US1 only — Login gate)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: US1 (Login)
4. **STOP and VALIDATE**: Run quickstart Scenarios 1–2 + frontend LoginForm smoke test
5. Users can log in and receive tokens — Auth gate is live

### Incremental Delivery

1. Phase 1 + 2 → Infrastructure ready
2. Phase 3 (US1) → Login functional → MVP deployed/demo'd
3. Phase 4 (US2) → Session context endpoint; frontend session hydration works
4. Phase 5 (US3) → Silent token renewal; long-running sessions no longer interrupted
5. Phase 6 (US4) → Explicit logout; security compliance satisfied
6. Phase 7 → Polish + full quickstart sign-off → Feature complete

### Parallel Team Strategy

With two developers after Phase 2:

- **Developer A**: Backend (T034–T061 sequentially within each story)
- **Developer B**: Frontend (T038–T065 sequentially within each story, unblocked by MSW)

---

## Notes

- `[P]` tasks = different files, no in-progress dependencies — safe to parallelize
- `[USx]` label = direct traceability to spec user stories
- All task descriptions include exact file paths — each task is self-contained
- `AuthService` is built incrementally: `LoginAsync` in US1, `GetCurrentUserAsync` in US2, `RefreshTokenAsync` in US3, `LogoutAsync` in US4
- `AuthController` is built incrementally: one action per user story phase
- `authHandlers.ts` is built incrementally: one handler per user story phase
- `authService.ts` is built incrementally: one function per user story phase
- DIP gate (T066) must pass before Phase 7 signs off — no EF Core in Business layer
- Stop at any checkpoint to validate the story independently before proceeding
