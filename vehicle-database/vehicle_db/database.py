import json, sqlite3
from pathlib import Path

SCHEMA_VERSION = "1"

def connect(path=":memory:"):
    conn = sqlite3.connect(path)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn

def init_db(conn):
    conn.executescript((Path(__file__).parent / "schema.sql").read_text(encoding="utf-8"))
    conn.execute("INSERT OR IGNORE INTO schema_meta VALUES ('schema_version', ?)", (SCHEMA_VERSION,))
    conn.execute("INSERT OR IGNORE INTO schema_meta VALUES ('data_version', '0')")
    conn.commit()
    return conn

def load_seed(conn):
    """Load bundled seed data (idempotent). Returns import stats."""
    from .importer import import_payload
    seed = Path(__file__).parent / "seed" / "seed_data.json"
    return import_payload(conn, json.loads(seed.read_text(encoding="utf-8")))
