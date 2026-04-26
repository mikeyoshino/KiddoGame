-- scraper/sql/003_add_provider_fields.sql
ALTER TABLE games
  ADD COLUMN IF NOT EXISTS provider          TEXT NOT NULL DEFAULT 'GameDistribute',
  ADD COLUMN IF NOT EXISTS provider_game_id  TEXT,
  ADD COLUMN IF NOT EXISTS game_url          TEXT;

-- Backfill existing GD records
UPDATE games SET provider = 'GameDistribute' WHERE provider IS NULL OR provider = '';

CREATE INDEX IF NOT EXISTS idx_games_provider ON games (provider);
