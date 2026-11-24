-- Normalised, sorted schema fingerprint of the public schema (read-only). One line per object, so two
-- databases can be compared with a plain diff. Contains no row data, only structure and EF history IDs.
SELECT line FROM (
  SELECT 'migration ' || "MigrationId" AS line FROM "__EFMigrationsHistory"
  UNION ALL
  SELECT format('column %s.%s %s null=%s default=%s', table_name, column_name,
                CASE WHEN data_type = 'character varying' THEN 'varchar(' || coalesce(character_maximum_length::text, '') || ')'
                     WHEN data_type = 'USER-DEFINED' THEN udt_name ELSE data_type END,
                is_nullable, coalesce(column_default, '-'))
  FROM information_schema.columns WHERE table_schema = 'public'
  UNION ALL
  SELECT format('constraint %s %s %s', conrelid::regclass, conname, pg_get_constraintdef(oid))
  FROM pg_constraint WHERE connamespace = 'public'::regnamespace
  UNION ALL
  SELECT format('index %s %s', tablename, regexp_replace(indexdef, '^CREATE (UNIQUE )?INDEX \S+ ON \S+ ', 'CREATE \1INDEX ON '))
         || ' name=' || indexname
  FROM pg_indexes WHERE schemaname = 'public'
  UNION ALL
  SELECT format('sequence %s', sequencename) FROM pg_sequences WHERE schemaname = 'public'
  UNION ALL
  SELECT format('view %s', viewname) FROM pg_views WHERE schemaname = 'public'
  UNION ALL
  SELECT format('function %s(%s)', p.proname, pg_get_function_identity_arguments(p.oid))
  FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace
  UNION ALL
  SELECT format('trigger %s %s', tgrelid::regclass, tgname) FROM pg_trigger WHERE NOT tgisinternal
    AND tgrelid IN (SELECT oid FROM pg_class WHERE relnamespace = 'public'::regnamespace)
) s ORDER BY line;
