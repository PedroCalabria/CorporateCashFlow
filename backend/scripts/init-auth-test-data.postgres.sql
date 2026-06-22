/*
  Corporate Cash Flow — Auth schema + seed data (PostgreSQL)
  Target: PostgreSQL 14+

  Senha de todos os usuários de teste: S3cur3P@ss
  Hash bcrypt (cost 12): $2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G

  Uso:
    psql -U postgres -f init-auth-test-data.postgres.sql

  ATENÇÃO: A API atual usa EF Core + SQL Server. Este script serve apenas se você
  migrar o backend para Npgsql. Com a configuração atual, use o script .sql (SQL Server).

  Roles: 0 = Admin | 1 = Editor | 2 = Auditor
*/

-- Ajuste o nome do banco/usuário conforme seu ambiente
SELECT 'CREATE DATABASE corporate_cash_flow'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'corporate_cash_flow')\gexec

\c corporate_cash_flow

-- Drop (ordem respeita FKs)
DROP TABLE IF EXISTS user_refresh_tokens CASCADE;
DROP TABLE IF EXISTS security_audit_logs CASCADE;
DROP TABLE IF EXISTS users CASCADE;

-- Tables
CREATE TABLE users
(
    id              UUID            NOT NULL PRIMARY KEY,
    name            VARCHAR(200)    NOT NULL,
    email           VARCHAR(320)    NOT NULL UNIQUE,
    password_hash   VARCHAR(500)    NOT NULL,
    role            INTEGER         NOT NULL,
    subsidiary_id   UUID            NULL,
    is_active       BOOLEAN         NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL,
    updated_at      TIMESTAMPTZ     NULL
);

CREATE TABLE user_refresh_tokens
(
    id                    UUID            NOT NULL PRIMARY KEY,
    user_id               UUID            NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash            VARCHAR(256)    NOT NULL UNIQUE,
    access_token_jti      VARCHAR(256)    NOT NULL,
    family_id             UUID            NOT NULL,
    expires_at            TIMESTAMPTZ     NOT NULL,
    is_revoked            BOOLEAN         NOT NULL,
    created_at            TIMESTAMPTZ     NOT NULL,
    revoked_at            TIMESTAMPTZ     NULL,
    replaced_by_token_id  UUID            NULL REFERENCES user_refresh_tokens (id)
);

CREATE INDEX ix_user_refresh_tokens_family_id_is_revoked
    ON user_refresh_tokens (family_id, is_revoked)
    WHERE is_revoked = FALSE;

CREATE INDEX ix_user_refresh_tokens_user_id_is_revoked
    ON user_refresh_tokens (user_id, is_revoked)
    WHERE is_revoked = FALSE;

CREATE INDEX ix_user_refresh_tokens_replaced_by_token_id
    ON user_refresh_tokens (replaced_by_token_id);

CREATE TABLE security_audit_logs
(
    id          BIGSERIAL       NOT NULL PRIMARY KEY,
    user_id     UUID            NULL,
    action      VARCHAR(50)     NOT NULL,
    outcome     VARCHAR(20)     NOT NULL,
    ip_address  VARCHAR(45)     NULL,
    detail      VARCHAR(500)    NULL,
    occurred_at TIMESTAMPTZ     NOT NULL
);

-- Seed
INSERT INTO users (id, name, email, password_hash, role, subsidiary_id, is_active, created_at)
VALUES
    ('3fa85f64-5717-4562-b3fc-2c963f66afa6', 'Jane Doe', 'jane.doe@example.com',
     '$2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G', 1,
     '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d', TRUE, NOW()),
    ('1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d', 'Admin User', 'admin@example.com',
     '$2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G', 0,
     NULL, TRUE, NOW()),
    ('8c7b6a59-4830-4e2f-9a1b-2c3d4e5f6071', 'Audit User', 'auditor@example.com',
     '$2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G', 2,
     '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d', TRUE, NOW()),
    ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'Inactive User', 'inactive@example.com',
     '$2a$12$6ALjsu8B9je4CM.pNdC94eIE2s9NIt5qwmtvLgmhUns.5RfDb6x7G', 1,
     '9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d', FALSE, NOW());
