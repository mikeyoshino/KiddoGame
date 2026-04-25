-- scraper/sql/update_get_distinct_categories.sql
-- Replaces the existing get_distinct_categories function to also return
-- the count of games per category, sorted by count descending.

DROP FUNCTION IF EXISTS get_distinct_categories();

CREATE OR REPLACE FUNCTION get_distinct_categories()
RETURNS TABLE(category text, count bigint)
LANGUAGE sql
AS $$
  SELECT unnest(categories) AS category, COUNT(*) AS count
  FROM games
  GROUP BY 1
  ORDER BY 2 DESC;
$$;
