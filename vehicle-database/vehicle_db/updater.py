"""Runtime data updates without rebuilding the app.
Package = {"format":"cds-data-package","version":<int>,"payload":{...},"sha256":<hex of canonical payload JSON>}.
NOTE (temporary decision): sha256 gives integrity only, not authenticity. Add a signature (e.g. Ed25519) before
distributing packages from a remote server; hook point is `_verify_integrity`."""
import hashlib, json
from .importer import import_payload, validate_payload, ImportValidationError

class UpdateError(Exception): pass

def _canon(payload): return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")

def build_update_package(payload, version):
    return {"format": "cds-data-package", "version": version, "payload": payload, "sha256": hashlib.sha256(_canon(payload)).hexdigest()}

def _verify_integrity(pkg):
    if pkg.get("format") != "cds-data-package": raise UpdateError("unknown package format")
    if hashlib.sha256(_canon(pkg.get("payload", {}))).hexdigest() != pkg.get("sha256"): raise UpdateError("checksum mismatch")

def current_data_version(conn):
    return int(conn.execute("SELECT value FROM schema_meta WHERE key='data_version'").fetchone()[0])

def apply_update_package(conn, pkg):
    """Verify, validate and apply atomically. Rejects tampered, invalid, or non-increasing versions."""
    if isinstance(pkg, (str, bytes)): pkg = json.loads(pkg)
    _verify_integrity(pkg)
    v = pkg.get("version")
    if not isinstance(v, int) or v <= current_data_version(conn): raise UpdateError(f"package version {v} is not newer than {current_data_version(conn)}")
    try:
        stats = import_payload(conn, pkg["payload"])
    except ImportValidationError as ex:
        raise UpdateError(f"invalid payload: {ex}") from ex
    conn.execute("UPDATE schema_meta SET value=? WHERE key='data_version'", (str(v),))
    conn.execute("INSERT INTO db_updates(package_version, sha256) VALUES (?,?)", (v, pkg["sha256"]))
    conn.commit()
    return stats
