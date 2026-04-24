CREATE OR REPLACE FUNCTION get_distinct_categories()
RETURNS TABLE(category TEXT)
LANGUAGE sql STABLE
AS $$
  SELECT DISTINCT unnest(categories) AS category
  FROM games
  WHERE status = 'done'
  ORDER BY category;
$$;
