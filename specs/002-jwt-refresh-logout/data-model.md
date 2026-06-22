# Data Model: Phase 1 — Complete Authentication Lifecycle

**Feature**: `002-jwt-refresh-logout`
**Date**: 2026-06-13

---

## Overview

This feature introduces three new entities and one existing entity (`User`) that must be confirmed:

| Entity | Table | Layer | Purpose |
|--------|-------|-------|---------|
| `User` | `Users` | `CorporateCashFlow.Entity` | Authenticated principal with role and subsidiary scope |
| `UserRefreshToken` | `UserRefreshTokens` | `CorporateCashFlow.Entity` | Server-side whitelist record for rotating refresh tokens |
| `SecurityAuditLog` | `SecurityAuditLogs` | `CorporateCashFlow.Entity` | Security event trail (distinct from financial audit trail) |

---

## Entity: `User`

### C# Class (`CorporateCashFlow.Entity/`)

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? SubsidiaryId { get; set; }       // null = Global Admin
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = [];
}
```

### SQL Server Schema

```sql
CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name            NVARCHAR(200)       NOT NULL,
    Email           NVARCHAR(320)       NOT NULL,
    PasswordHash    NVARCHAR(500)       NOT NULL,
    Role            INT                 NOT NULL,       -- 0=Admin,1=Editor,2=Auditor
    SubsidiaryId    UNIQUEIDENTIFIER    NULL,
    IsActive        BIT                 NOT NULL DEFAULT 1,
    CreatedAt       DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt       DATETIMEOFFSET      NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
```

### EF Core Configuration (`UserEntityTypeConfiguration.cs`)

```csharp
builder.ToTable("Users");
builder.HasKey(u => u.Id);
builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
builder.HasIndex(u => u.Email).IsUnique();
builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
builder.Property(u => u.Role).IsRequired();
```

---

## Entity: `UserRefreshToken`

### C# Class (`CorporateCashFlow.Entity/`)

```csharp
public class UserRefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the raw opaque refresh token value. Never store raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>JTI claim of the paired access token for session binding validation.</summary>
    public string AccessTokenJti { get; set; } = string.Empty;

    /// <summary>Shared across all tokens in a rotation chain. Used for family invalidation.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Points to the token that replaced this one. Enables rotation chain audit.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public User User { get; set; } = null!;
    public UserRefreshToken? ReplacedBy { get; set; }
}
```

### SQL Server Schema

```sql
CREATE TABLE UserRefreshTokens (
    Id                  UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId              UNIQUEIDENTIFIER    NOT NULL,
    TokenHash           NVARCHAR(256)       NOT NULL,
    AccessTokenJti      NVARCHAR(256)       NOT NULL,
    FamilyId            UNIQUEIDENTIFIER    NOT NULL,
    ExpiresAt           DATETIMEOFFSET      NOT NULL,
    IsRevoked           BIT                 NOT NULL DEFAULT 0,
    CreatedAt           DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    RevokedAt           DATETIMEOFFSET      NULL,
    ReplacedByTokenId   UNIQUEIDENTIFIER    NULL,
    CONSTRAINT PK_UserRefreshTokens PRIMARY KEY (Id),
    CONSTRAINT UQ_UserRefreshTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_UserRefreshTokens_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRefreshTokens_Self
        FOREIGN KEY (ReplacedByTokenId) REFERENCES UserRefreshTokens(Id)
);

-- Lookup by token hash (hot path — every refresh call)
CREATE UNIQUE INDEX IX_UserRefreshTokens_TokenHash
    ON UserRefreshTokens (TokenHash);

-- Family invalidation query: WHERE FamilyId = @id AND IsRevoked = 0
CREATE INDEX IX_UserRefreshTokens_FamilyId_IsRevoked
    ON UserRefreshTokens (FamilyId, IsRevoked)
    WHERE IsRevoked = 0;

-- Logout query: WHERE UserId = @id AND IsRevoked = 0
CREATE INDEX IX_UserRefreshTokens_UserId_IsRevoked
    ON UserRefreshTokens (UserId, IsRevoked)
    WHERE IsRevoked = 0;
```

### EF Core Configuration (`UserRefreshTokenEntityTypeConfiguration.cs`)

```csharp
builder.ToTable("UserRefreshTokens");
builder.HasKey(t => t.Id);
builder.Property(t => t.TokenHash).HasMaxLength(256).IsRequired();
builder.HasIndex(t => t.TokenHash).IsUnique();
builder.Property(t => t.AccessTokenJti).HasMaxLength(256).IsRequired();

builder.HasOne(t => t.User)
    .WithMany(u => u.RefreshTokens)
    .HasForeignKey(t => t.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(t => t.ReplacedBy)
    .WithMany()
    .HasForeignKey(t => t.ReplacedByTokenId)
    .OnDelete(DeleteBehavior.NoAction);

// Filtered index for family invalidation
builder.HasIndex(t => new { t.FamilyId, t.IsRevoked })
    .HasFilter("IsRevoked = 0");

// Filtered index for logout
builder.HasIndex(t => new { t.UserId, t.IsRevoked })
    .HasFilter("IsRevoked = 0");
```

---

## Entity: `SecurityAuditLog`

### C# Class (`CorporateCashFlow.Entity/`)

```csharp
public class SecurityAuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }    // null for anonymous attempts (login failure before user found)
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
```

### Action Values

| Action Constant | Trigger |
|----------------|---------|
| `Login.Success` | Successful credential verification |
| `Login.Failure` | Invalid email or password |
| `Refresh.Success` | Successful token rotation |
| `Refresh.Failure.Expired` | Token expired or revoked in whitelist |
| `Refresh.Failure.Replay` | Replay attack — token already consumed (family revoked) |
| `Refresh.Failure.Race` | Optimistic lock lost — benign concurrent request |
| `Refresh.Failure.Unavailable` | Whitelist store unavailable (503 path) |
| `Logout.Success` | Session successfully invalidated |

### SQL Server Schema

```sql
CREATE TABLE SecurityAuditLogs (
    Id          BIGINT              NOT NULL IDENTITY(1,1),
    UserId      UNIQUEIDENTIFIER    NULL,
    Action      NVARCHAR(50)        NOT NULL,
    Outcome     NVARCHAR(20)        NOT NULL,
    IpAddress   NVARCHAR(45)        NULL,
    Detail      NVARCHAR(500)       NULL,
    OccurredAt  DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_SecurityAuditLogs PRIMARY KEY (Id)
);

CREATE INDEX IX_SecurityAuditLogs_UserId_OccurredAt
    ON SecurityAuditLogs (UserId, OccurredAt DESC)
    WHERE UserId IS NOT NULL;
```

### EF Core Configuration (`SecurityAuditLogEntityTypeConfiguration.cs`)

```csharp
builder.ToTable("SecurityAuditLogs");
builder.HasKey(l => l.Id);
builder.Property(l => l.Id).UseIdentityColumn();
builder.Property(l => l.Action).HasMaxLength(50).IsRequired();
builder.Property(l => l.Outcome).HasMaxLength(20).IsRequired();
builder.Property(l => l.IpAddress).HasMaxLength(45);
builder.Property(l => l.Detail).HasMaxLength(500);
```

---

## Enums: `UserRole` (`CorporateCashFlow.Entity/Enums/`)

```csharp
public enum UserRole
{
    Admin = 0,
    Editor = 1,
    Auditor = 2
}
```

---

## Relationship Diagram

```
Users (1) ──────────────── (N) UserRefreshTokens
  │                                │
  │  Id (PK)                       │  Id (PK)
  │  Email (unique)                │  UserId (FK → Users.Id)
  │  PasswordHash                  │  TokenHash (unique)
  │  Role                          │  AccessTokenJti
  │  SubsidiaryId?                 │  FamilyId  ◄── shared across rotation chain
  │  IsActive                      │  ExpiresAt
  │  CreatedAt                     │  IsRevoked
                                   │  ReplacedByTokenId? (FK self-ref)

SecurityAuditLogs (append-only, no FK)
  │  UserId? (nullable — no FK enforced for write speed)
  │  Action
  │  Outcome
  │  OccurredAt
```

> **Note on `SecurityAuditLogs.UserId`**: No FK constraint is enforced to avoid blocking audit
> writes when the user record does not exist (e.g., login failure for unknown email). The column
> is a soft reference only.

---

## CQS Split for This Feature

| Operation | Method | Technology | Reason |
|-----------|--------|------------|--------|
| Login (validate + persist token) | Write | EF Core | Transactional token write |
| Get current user (`/me`) | Read | Dapper | Pure claim read — no state mutation |
| Refresh (consume + rotate token) | Write | EF Core | Atomic `ExecuteUpdateAsync` CAS |
| Logout (revoke tokens) | Write | EF Core | Bulk revoke via `ExecuteUpdateAsync` |
| Security audit write | Write | EF Core | Append-only, separate `DbContext` call |

> `GET /auth/me` does not hit the database at all in the happy path — claims are decoded directly
> from the validated JWT. A Dapper call is only made to hydrate `Name` and `Email` (not in claims).
