CREATE TABLE IF NOT EXISTS games (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  object_id     TEXT UNIQUE NOT NULL,
  slug          TEXT UNIQUE NOT NULL,
  title         TEXT NOT NULL,
  company       TEXT,
  thumbnail_url TEXT,
  description   TEXT,
  instruction   TEXT,
  categories    TEXT[],
  tags          TEXT[],
  languages     TEXT[],
  gender        TEXT[],
  age_group     TEXT[],
  status        TEXT NOT NULL DEFAULT 'pending',
  view_count    INTEGER NOT NULL DEFAULT 0,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS games_status_idx ON games (status);
CREATE INDEX IF NOT EXISTS games_object_id_idx ON games (object_id);
