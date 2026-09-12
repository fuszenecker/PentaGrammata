-- ============================================================================
-- PentaGrammata practice-results.db upgrade: 1.11.x -> 1.12.0.0 (schema 3 -> 4)
--
-- Migrates a database created by 1.11.x up to the schema this branch expects.
-- The application also self-heals its schema on startup (EnsureSchemaAsync), so
-- this script is for operators who prefer to migrate a database explicitly
-- (backups, offline copies, external tooling).
--
-- Changes in this release:
--   1. practice_result_statistics gains the QSB (signal fading) columns:
--      qsb_enabled, qsb_depth_db, qsb_period_seconds.
--
-- Column order: on a fresh database the application creates the QSB columns
-- immediately before noise_type, so the settings read left to right as the audio
-- pipeline applies them (fading first, then the receiver chain). ALTER TABLE can
-- only append, so a migrated database carries them at the end of the table. That
-- difference is harmless: every query names its columns explicitly.
--
-- Existing rows are backfilled with qsb_enabled = 0 (no fading was applied) and
-- the settings defaults for depth and period, which the trends chart ignores
-- while qsb_enabled is 0.
--
-- Re-run safety: the ADD COLUMN steps are not conditional (SQLite has no "ADD
-- COLUMN IF NOT EXISTS"); running this script a second time will fail with
-- "duplicate column name: qsb_enabled", which simply means the upgrade was
-- already applied. Run it once, against a 1.11.x database.
--
-- Usage:  sqlite3 practice-results.db < upgrade-1.11.0-to-1.12.0.sql
-- Back up practice-results.db before running.
-- ============================================================================

PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- 1. QSB columns. Existing rows take the same DEFAULTs the application's
--    CREATE TABLE specifies.
ALTER TABLE practice_result_statistics
    ADD COLUMN qsb_enabled INTEGER NOT NULL DEFAULT 0;

ALTER TABLE practice_result_statistics
    ADD COLUMN qsb_depth_db REAL NOT NULL DEFAULT 10.0;

ALTER TABLE practice_result_statistics
    ADD COLUMN qsb_period_seconds REAL NOT NULL DEFAULT 5.0;

-- 2. Record the new schema version.
DELETE FROM schema_info;
INSERT INTO schema_info(version) VALUES (4);

COMMIT;
