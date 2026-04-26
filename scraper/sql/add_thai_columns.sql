-- Add Thai translation columns to games table
ALTER TABLE games
    ADD COLUMN IF NOT EXISTS description_th      TEXT,
    ADD COLUMN IF NOT EXISTS instruction_th      TEXT,
    ADD COLUMN IF NOT EXISTS translation_status  TEXT; -- null | 'translated' | 'failed'

-- Optional: index to quickly find untranslated records
CREATE INDEX IF NOT EXISTS idx_games_translation_status
    ON games (translation_status)
    WHERE translation_status IS NULL;
