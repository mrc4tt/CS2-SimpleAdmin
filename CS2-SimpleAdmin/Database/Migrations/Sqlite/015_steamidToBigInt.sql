-- SQLite uses dynamic type affinity, so the BIGINT/INT ALTER TABLEs from the
-- MySQL counterpart of this migration would be no-ops here. Only the
-- admin_steamid sanitization is portable, so that is all this migration does.
UPDATE `sa_bans`
SET admin_steamid = '0'
WHERE admin_steamid NOT GLOB '[0-9]*';

UPDATE `sa_mutes`
SET admin_steamid = '0'
WHERE admin_steamid NOT GLOB '[0-9]*';

UPDATE `sa_warns`
SET admin_steamid = '0'
WHERE admin_steamid NOT GLOB '[0-9]*';

UPDATE `sa_admins`
SET player_steamid = '0'
WHERE player_steamid NOT GLOB '[0-9]*';