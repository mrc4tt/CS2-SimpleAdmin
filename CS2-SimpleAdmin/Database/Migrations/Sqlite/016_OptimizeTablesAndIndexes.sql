-- Migration 016: Optimize tables and indexes
-- Mirrors the MySQL counterpart's intent using SQLite-compatible syntax.

-- sa_players_ips
CREATE INDEX IF NOT EXISTS idx_players_ips_used_at ON sa_players_ips (used_at DESC);

-- sa_bans
CREATE INDEX IF NOT EXISTS idx_bans_steamid_status ON sa_bans (player_steamid, status);
CREATE INDEX IF NOT EXISTS idx_bans_ip_status ON sa_bans (player_ip, status);
CREATE INDEX IF NOT EXISTS idx_bans_status_ends ON sa_bans (status, ends);
CREATE INDEX IF NOT EXISTS idx_bans_server_status ON sa_bans (server_id, status, ends);
CREATE INDEX IF NOT EXISTS idx_bans_created ON sa_bans (created DESC);

-- sa_admins
CREATE INDEX IF NOT EXISTS idx_admins_steamid ON sa_admins (player_steamid);
CREATE INDEX IF NOT EXISTS idx_admins_server_ends ON sa_admins (server_id, ends);
CREATE INDEX IF NOT EXISTS idx_admins_ends ON sa_admins (ends);

-- sa_mutes (in addition to migration 014)
CREATE INDEX IF NOT EXISTS idx_mutes_status_ends ON sa_mutes (status, ends);
CREATE INDEX IF NOT EXISTS idx_mutes_server_status ON sa_mutes (server_id, status, ends);
CREATE INDEX IF NOT EXISTS idx_mutes_created ON sa_mutes (created DESC);

-- sa_warns
CREATE INDEX IF NOT EXISTS idx_warns_steamid_status ON sa_warns (player_steamid, status);
CREATE INDEX IF NOT EXISTS idx_warns_status_ends ON sa_warns (status, ends);
CREATE INDEX IF NOT EXISTS idx_warns_server_status ON sa_warns (server_id, status, ends);

-- sa_servers
CREATE INDEX IF NOT EXISTS idx_servers_hostname ON sa_servers (hostname);
