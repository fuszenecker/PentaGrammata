-- ============================================================================
-- PentaGrammata practice-results.db upgrade: 1.7.0.0 -> 1.8.0.0
--
-- Migrates a database created by 1.7.0.0 up to the schema this branch expects.
-- The application also self-heals its schema on startup (EnsureSchemaAsync), so
-- this script is for operators who prefer to migrate a database explicitly
-- (backups, offline copies, external tooling).
--
-- Changes in this release:
--   1. practice_result_statistics gains error_threshold_percent.
--   2. New practice_confusions table (per-symbol confusion records).
--   3. Supporting indexes on recorded_at and the confusion symbol pair.
--
-- NOTE: noise_level_db is unchanged. The UI now shows a signal-to-noise ratio,
-- but the stored value stays relative to the CW signal level (SNR =
-- -noise_level_db), so no data migration is needed for it.
--
-- Re-run safety: the new table and indexes use IF NOT EXISTS and are safe to
-- re-apply. The ADD COLUMN step is not conditional (SQLite has no "ADD COLUMN
-- IF NOT EXISTS"); running this script a second time will fail with "duplicate
-- column name: error_threshold_percent", which simply means the upgrade was
-- already applied. Run it once, against a 1.7.0.0 database.
--
-- Usage:  sqlite3 practice-results.db < upgrade-1.7.0-to-1.8.0.sql
-- Back up practice-results.db before running.
-- ============================================================================

PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- 1. Add error_threshold_percent. Existing rows take the same DEFAULT 0 the
--    application's CREATE TABLE specifies.
ALTER TABLE practice_result_statistics
    ADD COLUMN error_threshold_percent REAL NOT NULL DEFAULT 0;

-- 2. Confusion records table (one row per expected/actual symbol pair per save).
CREATE TABLE IF NOT EXISTS practice_confusions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    statistics_id INTEGER NOT NULL,
    recorded_at TEXT NOT NULL,
    expected_symbol TEXT NOT NULL,
    actual_symbol TEXT NOT NULL,
    distance INTEGER NOT NULL,
    count INTEGER NOT NULL,
    FOREIGN KEY(statistics_id) REFERENCES practice_result_statistics(id) ON DELETE CASCADE
);

-- 3. Indexes.
CREATE INDEX IF NOT EXISTS idx_practice_statistics_recorded_at
    ON practice_result_statistics(recorded_at);

CREATE INDEX IF NOT EXISTS idx_practice_confusions_recorded_at
    ON practice_confusions(recorded_at);

CREATE INDEX IF NOT EXISTS idx_practice_confusions_symbols
    ON practice_confusions(expected_symbol, actual_symbol);

COMMIT;
