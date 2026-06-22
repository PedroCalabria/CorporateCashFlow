/*
  Corporate Cash Flow — Auth schema + seed data (Phase 1)
  Target: SQL Server 2019+ / LocalDB / Azure SQL

  Senha de todos os usuários de teste: S3cur3P@ss
  Hash bcrypt (cost 12): $2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G

  Uso:
    1. Ajuste o nome do banco abaixo, se necessário
    2. Execute no SSMS, Azure Data Studio ou:
       sqlcmd -S "(localdb)\mssqllocaldb" -I -i backend\scripts\init-auth-test-data.sql
       (-I liga QUOTED_IDENTIFIER, necessário para índices filtrados)

  Roles: 0 = Admin | 1 = Editor | 2 = Auditor
*/

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Database
-- ---------------------------------------------------------------------------
IF DB_ID(N'CorporateCashFlow') IS NULL
BEGIN
    CREATE DATABASE [CorporateCashFlow];
END;
GO

USE [CorporateCashFlow];
GO

-- ---------------------------------------------------------------------------
-- Drop (ordem respeita FKs)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.UserRefreshTokens', N'U') IS NOT NULL
    DROP TABLE dbo.UserRefreshTokens;
GO

IF OBJECT_ID(N'dbo.SecurityAuditLogs', N'U') IS NOT NULL
    DROP TABLE dbo.SecurityAuditLogs;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    DROP TABLE dbo.Users;
GO

-- ---------------------------------------------------------------------------
-- Tables
-- ---------------------------------------------------------------------------
CREATE TABLE dbo.Users
(
    Id              UNIQUEIDENTIFIER    NOT NULL,
    Name            NVARCHAR(200)       NOT NULL,
    Email           NVARCHAR(320)       NOT NULL,
    PasswordHash    NVARCHAR(500)       NOT NULL,
    Role            INT                 NOT NULL,
    SubsidiaryId    UNIQUEIDENTIFIER    NULL,
    IsActive        BIT                 NOT NULL,
    CreatedAt       DATETIMEOFFSET      NOT NULL,
    UpdatedAt       DATETIMEOFFSET      NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

CREATE TABLE dbo.UserRefreshTokens
(
    Id                  UNIQUEIDENTIFIER    NOT NULL,
    UserId              UNIQUEIDENTIFIER    NOT NULL,
    TokenHash           NVARCHAR(256)       NOT NULL,
    AccessTokenJti      NVARCHAR(256)       NOT NULL,
    FamilyId            UNIQUEIDENTIFIER    NOT NULL,
    ExpiresAt           DATETIMEOFFSET      NOT NULL,
    IsRevoked           BIT                 NOT NULL,
    CreatedAt           DATETIMEOFFSET      NOT NULL,
    RevokedAt           DATETIMEOFFSET      NULL,
    ReplacedByTokenId   UNIQUEIDENTIFIER    NULL,
    CONSTRAINT PK_UserRefreshTokens PRIMARY KEY (Id),
    CONSTRAINT FK_UserRefreshTokens_Users
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRefreshTokens_Self
        FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.UserRefreshTokens (Id)
);
GO

CREATE UNIQUE INDEX IX_UserRefreshTokens_TokenHash
    ON dbo.UserRefreshTokens (TokenHash);
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE INDEX IX_UserRefreshTokens_FamilyId_IsRevoked
    ON dbo.UserRefreshTokens (FamilyId, IsRevoked)
    WHERE IsRevoked = 0;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE INDEX IX_UserRefreshTokens_UserId_IsRevoked
    ON dbo.UserRefreshTokens (UserId, IsRevoked)
    WHERE IsRevoked = 0;
GO

CREATE INDEX IX_UserRefreshTokens_ReplacedByTokenId
    ON dbo.UserRefreshTokens (ReplacedByTokenId);
GO

CREATE TABLE dbo.SecurityAuditLogs
(
    Id          BIGINT              NOT NULL IDENTITY(1, 1),
    UserId      UNIQUEIDENTIFIER    NULL,
    Action      NVARCHAR(50)        NOT NULL,
    Outcome     NVARCHAR(20)        NOT NULL,
    IpAddress   NVARCHAR(45)        NULL,
    Detail      NVARCHAR(500)       NULL,
    OccurredAt  DATETIMEOFFSET      NOT NULL,
    CONSTRAINT PK_SecurityAuditLogs PRIMARY KEY (Id)
);
GO

-- ---------------------------------------------------------------------------
-- Seed: Users
-- ---------------------------------------------------------------------------
DECLARE @PasswordHash NVARCHAR(500) = N'$2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO dbo.Users (Id, Name, Email, PasswordHash, Role, SubsidiaryId, IsActive, CreatedAt)
VALUES
    -- Editor com subsidiária (login principal do quickstart / MSW)
    (
        '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        N'Jane Doe',
        N'jane.doe@example.com',
        @PasswordHash,
        1,
        '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
        1,
        @Now
    ),
    -- Admin global (SubsidiaryId NULL)
    (
        '1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d',
        N'Admin User',
        N'admin@example.com',
        @PasswordHash,
        0,
        NULL,
        1,
        @Now
    ),
    -- Auditor com subsidiária (teste de RBAC)
    (
        '8c7b6a59-4830-4e2f-9a1b-2c3d4e5f6071',
        N'Audit User',
        N'auditor@example.com',
        @PasswordHash,
        2,
        '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
        1,
        @Now
    ),
    -- Usuário inativo (login deve falhar)
    (
        'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
        N'Inactive User',
        N'inactive@example.com',
        @PasswordHash,
        1,
        '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
        0,
        @Now
    );
GO

-- UserRefreshTokens e SecurityAuditLogs ficam vazios:
-- são populados automaticamente pela API em login, refresh e logout.

PRINT 'Schema criado e usuários de teste inseridos.';
PRINT 'Credenciais: jane.doe@example.com | admin@example.com | auditor@example.com';
PRINT 'Senha: S3cur3P@ss';
GO
