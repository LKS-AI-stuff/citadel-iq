#!/bin/sh
# Runs once, when the Postgres volume is first initialized (docker-entrypoint-initdb.d).
# Creates the restricted runtime role the API connects as. Migrations run as the owner (POSTGRES_USER) and grant
# this role data rights on the application tables; it can never bypass row-level security.
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v app_password="$APP_DB_PASSWORD" <<'SQL'
CREATE ROLE citadeliq_app LOGIN PASSWORD :'app_password' NOSUPERUSER NOBYPASSRLS;
SQL
