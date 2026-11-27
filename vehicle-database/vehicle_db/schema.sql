-- Vehicle Database schema v1 (Developer B). Every data row references data_sources (origin + license).
CREATE TABLE IF NOT EXISTS schema_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS data_sources (
  id INTEGER PRIMARY KEY,
  key TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  url TEXT,
  license TEXT NOT NULL,
  license_url TEXT,
  retrieved_at TEXT,
  verification TEXT NOT NULL CHECK (verification IN ('standard','verified','sample_unverified')),
  notes TEXT
);

CREATE TABLE IF NOT EXISTS manufacturers (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE COLLATE NOCASE,
  country TEXT,
  source_id INTEGER NOT NULL REFERENCES data_sources(id)
);

CREATE TABLE IF NOT EXISTS vehicle_models (
  id INTEGER PRIMARY KEY,
  manufacturer_id INTEGER NOT NULL REFERENCES manufacturers(id),
  name TEXT NOT NULL COLLATE NOCASE,
  generation TEXT NOT NULL DEFAULT '',
  source_id INTEGER NOT NULL REFERENCES data_sources(id),
  UNIQUE (manufacturer_id, name, generation)
);

CREATE TABLE IF NOT EXISTS vehicle_variants (
  id INTEGER PRIMARY KEY,
  model_id INTEGER NOT NULL REFERENCES vehicle_models(id),
  year_from INTEGER NOT NULL CHECK (year_from BETWEEN 1980 AND 2100),
  year_to INTEGER CHECK (year_to IS NULL OR year_to >= year_from),
  engine_code TEXT NOT NULL COLLATE NOCASE,
  engine_desc TEXT,
  displacement_cc INTEGER,
  fuel TEXT,
  market TEXT NOT NULL DEFAULT '',
  source_id INTEGER NOT NULL REFERENCES data_sources(id),
  UNIQUE (model_id, year_from, engine_code, market)
);

CREATE TABLE IF NOT EXISTS ecu_definitions (
  id INTEGER PRIMARY KEY,
  variant_id INTEGER REFERENCES vehicle_variants(id),   -- NULL = generic (any OBD-II vehicle)
  name TEXT NOT NULL,
  protocol TEXT NOT NULL,
  tx_id TEXT, rx_id TEXT,
  source_id INTEGER NOT NULL REFERENCES data_sources(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ecu ON ecu_definitions(COALESCE(variant_id,0), name, protocol);

CREATE TABLE IF NOT EXISTS dtc_definitions (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL,
  system TEXT NOT NULL CHECK (system IN ('P','B','C','U')),
  is_generic INTEGER NOT NULL DEFAULT 1,
  manufacturer_id INTEGER REFERENCES manufacturers(id),
  description TEXT NOT NULL,
  source_id INTEGER NOT NULL REFERENCES data_sources(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_dtc ON dtc_definitions(code, COALESCE(manufacturer_id,0));

CREATE TABLE IF NOT EXISTS pid_definitions (
  id INTEGER PRIMARY KEY,
  service INTEGER NOT NULL,
  pid INTEGER NOT NULL CHECK (pid BETWEEN 0 AND 255),
  name TEXT NOT NULL,
  num_bytes INTEGER NOT NULL CHECK (num_bytes BETWEEN 1 AND 4),
  formula TEXT NOT NULL,
  unit TEXT,
  min_value REAL, max_value REAL,
  source_id INTEGER NOT NULL REFERENCES data_sources(id),
  UNIQUE (service, pid)
);

CREATE TABLE IF NOT EXISTS did_definitions (
  id INTEGER PRIMARY KEY,
  did INTEGER NOT NULL CHECK (did BETWEEN 0 AND 65535),
  name TEXT NOT NULL,
  data_type TEXT NOT NULL CHECK (data_type IN ('ascii','hex','uint','bool')),
  length INTEGER,
  ecu_id INTEGER REFERENCES ecu_definitions(id),
  source_id INTEGER NOT NULL REFERENCES data_sources(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_did ON did_definitions(did, COALESCE(ecu_id,0));

CREATE TABLE IF NOT EXISTS supported_services (
  id INTEGER PRIMARY KEY,
  protocol_family TEXT NOT NULL CHECK (protocol_family IN ('OBD','UDS')),
  service_id INTEGER NOT NULL CHECK (service_id BETWEEN 0 AND 255),
  name TEXT NOT NULL,
  risk_level TEXT NOT NULL CHECK (risk_level IN ('read','write','actuate')),
  ecu_id INTEGER REFERENCES ecu_definitions(id),
  source_id INTEGER NOT NULL REFERENCES data_sources(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_svc ON supported_services(protocol_family, service_id, COALESCE(ecu_id,0));

CREATE TABLE IF NOT EXISTS db_updates (
  id INTEGER PRIMARY KEY,
  package_version INTEGER NOT NULL,
  sha256 TEXT NOT NULL,
  applied_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS ix_variant_year ON vehicle_variants(year_from, year_to);
CREATE INDEX IF NOT EXISTS ix_variant_engine ON vehicle_variants(engine_code);
