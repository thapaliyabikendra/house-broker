-- docs/db-roles.sql
-- Least-privilege database roles for HouseBroker (single database: HouseBroker).
-- Run as a sysadmin/DB owner after creating the database and applying migrations.

USE [HouseBroker];
GO

-- ---------------------------------------------------------------------------
-- Database roles
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'HB_ApiRole' AND type = 'R')
    CREATE ROLE [HB_ApiRole];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'HB_AuthRole' AND type = 'R')
    CREATE ROLE [HB_AuthRole];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'HB_MigratorRole' AND type = 'R')
    CREATE ROLE [HB_MigratorRole];
GO

-- ---------------------------------------------------------------------------
-- Runtime access: API reads/writes business tables only; no schema changes.
-- ---------------------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[dbo] TO [HB_ApiRole];
GRANT SELECT ON SCHEMA::[identity] TO [HB_ApiRole]; -- read-only where required by configuration
GO

-- ---------------------------------------------------------------------------
-- Runtime access: SSO reads/writes identity tables only.
-- ---------------------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[identity] TO [HB_AuthRole];
GO

-- ---------------------------------------------------------------------------
-- Migrator: schema management, but no data read/write beyond what migrations need.
-- ---------------------------------------------------------------------------
GRANT CREATE TABLE, ALTER, REFERENCES TO [HB_MigratorRole];
GRANT VIEW DEFINITION TO [HB_MigratorRole];
GO

-- ---------------------------------------------------------------------------
-- Suggested logins/users (supply secrets through your platform secret store).
-- ---------------------------------------------------------------------------
-- CREATE LOGIN [hb_api] WITH PASSWORD = '<from-secret-store>';
-- CREATE USER [hb_api] FOR LOGIN [hb_api];
-- ALTER ROLE [HB_ApiRole] ADD MEMBER [hb_api];
--
-- CREATE LOGIN [hb_auth] WITH PASSWORD = '<from-secret-store>';
-- CREATE USER [hb_auth] FOR LOGIN [hb_auth];
-- ALTER ROLE [HB_AuthRole] ADD MEMBER [hb_auth];
--
-- CREATE LOGIN [hb_migrator] WITH PASSWORD = '<from-secret-store>';
-- CREATE USER [hb_migrator] FOR LOGIN [hb_migrator];
-- ALTER ROLE [HB_MigratorRole] ADD MEMBER [hb_migrator];
GO