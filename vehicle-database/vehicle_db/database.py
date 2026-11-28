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
    """Load every bundled seed file in seed/*.json (sorted, idempotent). Returns summed import stats."""
    from .importer import import_payload
    total = {}
    for f in sorted((Path(__file__).parent / "seed").glob("*.json")):
        for k, v in import_payload(conn, json.loads(f.read_text(encoding="utf-8"))).items():
            total[k] = total.get(k, 0) + v
    return total
