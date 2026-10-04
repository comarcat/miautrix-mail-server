-- ==============================================================================
-- Miautrix Mail Server - PostgreSQL User, Database & Permissions Bootstrap
-- ==============================================================================
-- Fixes PostgreSQL 15+ "ERROR: 42501: permission denied for schema public"
-- Run this as superuser 'postgres' against the database cluster.

-- 1. Create or alter user mmdb-user
DO
$do$
BEGIN
   IF NOT EXISTS (
      SELECT FROM pg_catalog.pg_roles
      WHERE  rolname = 'mmdb-user') THEN
      CREATE ROLE "mmdb-user" WITH LOGIN PASSWORD 'Mi@usito#2026!' CREATEDB;
   ELSE
      ALTER ROLE "mmdb-user" WITH LOGIN PASSWORD 'Mi@usito#2026!' CREATEDB;
   END IF;
END
$do$;

-- 2. Provision and configure target database: miautrix-mail-pro
SELECT 'CREATE DATABASE "miautrix-mail-pro" OWNER "mmdb-user"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'miautrix-mail-pro')\gexec

ALTER DATABASE "miautrix-mail-pro" OWNER TO "mmdb-user";
GRANT ALL PRIVILEGES ON DATABASE "miautrix-mail-pro" TO "mmdb-user";

\c "miautrix-mail-pro"

GRANT ALL ON SCHEMA public TO "mmdb-user";
ALTER SCHEMA public OWNER TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO "mmdb-user";

-- 3. Provision and configure dev database: miautrix-mail-dev
\c postgres

SELECT 'CREATE DATABASE "miautrix-mail-dev" OWNER "mmdb-user"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'miautrix-mail-dev')\gexec

ALTER DATABASE "miautrix-mail-dev" OWNER TO "mmdb-user";
GRANT ALL PRIVILEGES ON DATABASE "miautrix-mail-dev" TO "mmdb-user";

\c "miautrix-mail-dev"

GRANT ALL ON SCHEMA public TO "mmdb-user";
ALTER SCHEMA public OWNER TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "mmdb-user";
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "mmdb-user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO "mmdb-user";
