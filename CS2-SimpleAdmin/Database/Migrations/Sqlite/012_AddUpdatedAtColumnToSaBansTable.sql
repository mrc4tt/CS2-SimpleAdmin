-- SQLite forbids non-constant defaults (CURRENT_TIMESTAMP) in ALTER TABLE ADD
-- COLUMN, so the MySQL counterpart's `DEFAULT CURRENT_TIMESTAMP` cannot be used
-- directly. Add the column with a sentinel constant, then backfill existing
-- rows to now. New rows that don't set updated_at explicitly get the sentinel,
-- which is fine because CacheManager's incremental fetch falls back to
-- `created > @lastUpdate`. SQLite also has no `ON UPDATE CURRENT_TIMESTAMP`
-- equivalent — that semantic is not preserved here.
ALTER TABLE `sa_bans` ADD COLUMN `updated_at` TIMESTAMP NOT NULL DEFAULT '1970-01-01 00:00:00';

UPDATE `sa_bans` SET `updated_at` = CURRENT_TIMESTAMP;
