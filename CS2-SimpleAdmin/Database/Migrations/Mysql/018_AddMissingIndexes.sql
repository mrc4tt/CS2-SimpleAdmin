-- Migration 018: the indexes migration 016 meant to add.
-- 016 was shipped fully commented out for MySQL (its `CREATE INDEX IF NOT EXISTS` only works on
-- MariaDB), so MySQL databases never got them. Plain ADD INDEX works on MySQL and MariaDB; the
-- migration runner skips "Duplicate key name" errors, so re-running this is harmless.

ALTER TABLE `sa_players_ips` ADD INDEX `idx_players_ips_used_at` (`used_at`);

ALTER TABLE `sa_bans` ADD INDEX `idx_bans_steamid_status` (`player_steamid`, `status`);
ALTER TABLE `sa_bans` ADD INDEX `idx_bans_ip_status` (`player_ip`, `status`);
ALTER TABLE `sa_bans` ADD INDEX `idx_bans_status_ends` (`status`, `ends`);
ALTER TABLE `sa_bans` ADD INDEX `idx_bans_server_status` (`server_id`, `status`, `ends`);
ALTER TABLE `sa_bans` ADD INDEX `idx_bans_created` (`created`);

ALTER TABLE `sa_admins` ADD INDEX `idx_admins_steamid` (`player_steamid`);
ALTER TABLE `sa_admins` ADD INDEX `idx_admins_server_ends` (`server_id`, `ends`);
ALTER TABLE `sa_admins` ADD INDEX `idx_admins_ends` (`ends`);

ALTER TABLE `sa_mutes` ADD INDEX `idx_mutes_status_ends` (`status`, `ends`);
ALTER TABLE `sa_mutes` ADD INDEX `idx_mutes_server_status` (`server_id`, `status`, `ends`);
ALTER TABLE `sa_mutes` ADD INDEX `idx_mutes_created` (`created`);

ALTER TABLE `sa_warns` ADD INDEX `idx_warns_steamid_status` (`player_steamid`, `status`);
ALTER TABLE `sa_warns` ADD INDEX `idx_warns_status_ends` (`status`, `ends`);
ALTER TABLE `sa_warns` ADD INDEX `idx_warns_server_status` (`server_id`, `status`, `ends`);

ALTER TABLE `sa_servers` ADD INDEX `idx_servers_hostname` (`hostname`);
