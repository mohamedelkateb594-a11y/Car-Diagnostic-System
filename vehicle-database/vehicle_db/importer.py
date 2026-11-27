"""Import + validation. All-or-nothing transaction, idempotent upserts. Format: see README."""
import re

class ImportValidationError(ValueError):
    def __init__(self, errors):
        super().__init__("; ".join(errors[:10]) + (f" (+{len(errors)-10} more)" if len(errors) > 10 else ""))
        self.errors = errors

DTC_RE = re.compile(r"^[PBCU][0-3][0-9A-F]{3}$")
HEX_RE = re.compile(r"^[0-9A-Fa-f]{1,8}$")
VERIFY = {"standard", "cross_checked", "verified", "sample_unverified"}

def validate_payload(p):
    """Return list of error strings (empty = valid)."""
    e = []
    sources = {s.get("key"): s for s in p.get("sources", [])}
    for k, s in sources.items():
        if not k: e.append("source without key")
        for f in ("name", "license"):
            if not s.get(f): e.append(f"source '{k}': missing {f}")
        if s.get("verification") not in VERIFY: e.append(f"source '{k}': invalid verification")
        if s.get("verification") in ("cross_checked", "verified"):  # documented claims must be traceable
            for f in ("url", "retrieved_at", "notes"):
                if not s.get(f): e.append(f"source '{k}': {s.get('verification')} requires {f}")
    def src(rec, where):
        if rec.get("source") not in sources: e.append(f"{where}: unknown/missing source '{rec.get('source')}'")
    for m in p.get("manufacturers", []):
        w = f"manufacturer '{m.get('name')}'"
        if not m.get("name"): e.append("manufacturer without name")
        src(m, w)
        for mo in m.get("models", []):
            wm = f"{w}/model '{mo.get('name')}'"
            if not mo.get("name"): e.append(f"{w}: model without name")
            src(mo, wm)
            for v in mo.get("variants", []):
                wv = f"{wm}/variant '{v.get('engine_code')}'"
                src(v, wv)
                yf, yt = v.get("year_from"), v.get("year_to")
                if not isinstance(yf, int) or not 1980 <= yf <= 2100: e.append(f"{wv}: bad year_from")
                elif yt is not None and (not isinstance(yt, int) or yt < yf): e.append(f"{wv}: bad year_to")
                if not v.get("engine_code"): e.append(f"{wv}: missing engine_code")
                for ecu in v.get("ecus", []): _ecu(ecu, wv, e, src)
    for i, ecu in enumerate(p.get("generic_ecus", [])): _ecu(ecu, f"generic_ecu #{i}", e, src)
    for d in p.get("dtcs", []):
        w = f"dtc '{d.get('code')}'"
        if not DTC_RE.match(str(d.get("code", ""))): e.append(f"{w}: invalid code format")
        if not d.get("description"): e.append(f"{w}: missing description")
        src(d, w)
    for d in p.get("pids", []):
        w = f"pid {d.get('service')}/{d.get('pid')}"
        if not isinstance(d.get("pid"), int) or not 0 <= d["pid"] <= 255: e.append(f"{w}: pid out of range")
        if not isinstance(d.get("service"), int): e.append(f"{w}: bad service")
        if not isinstance(d.get("num_bytes"), int) or not 1 <= d["num_bytes"] <= 4: e.append(f"{w}: bad num_bytes")
        else:
            try:
                from .decode import safe_eval
                safe_eval(d.get("formula", ""), {n: 1 for n in "ABCD"[:d["num_bytes"]]})
            except Exception as ex:
                e.append(f"{w}: bad formula ({ex})")
        src(d, w)
    for d in p.get("dids", []):
        w = f"did {d.get('did')}"
        if not isinstance(d.get("did"), int) or not 0 <= d["did"] <= 0xFFFF: e.append(f"{w}: did out of range")
        if d.get("data_type") not in ("ascii", "hex", "uint", "bool"): e.append(f"{w}: bad data_type")
        src(d, w)
    for d in p.get("services", []):
        w = f"service {d.get('protocol_family')}/{d.get('service_id')}"
        if d.get("protocol_family") not in ("OBD", "UDS"): e.append(f"{w}: bad protocol_family")
        if d.get("risk_level") not in ("read", "write", "actuate"): e.append(f"{w}: bad risk_level")
        if not isinstance(d.get("service_id"), int) or not 0 <= d["service_id"] <= 255: e.append(f"{w}: bad service_id")
        src(d, w)
    return e

def _ecu(ecu, where, e, src):
    w = f"{where}/ecu '{ecu.get('name')}'"
    if not ecu.get("name") or not ecu.get("protocol"): e.append(f"{w}: name and protocol required")
    for f in ("tx_id", "rx_id"):
        if ecu.get(f) and not HEX_RE.match(ecu[f]): e.append(f"{w}: {f} not hex")
    src(ecu, w)

def _upsert(conn, table, key, values):
    where = " AND ".join(f"{k} IS ?" for k in key)
    row = conn.execute(f"SELECT id FROM {table} WHERE {where}", tuple(key.values())).fetchone()
    if row:
        if values:
            conn.execute(f"UPDATE {table} SET " + ", ".join(f"{k}=?" for k in values) + " WHERE id=?", (*values.values(), row[0]))
        return row[0], False
    cols = {**key, **values}
    cur = conn.execute(f"INSERT INTO {table} ({','.join(cols)}) VALUES ({','.join('?'*len(cols))})", tuple(cols.values()))
    return cur.lastrowid, True

def import_payload(conn, p):
    errors = validate_payload(p)
    if errors: raise ImportValidationError(errors)
    stats = {}
    def n(t, new): stats[t] = stats.get(t, 0) + (1 if new else 0)
    conn.execute("SAVEPOINT imp")
    try:
        sid = {}
        for s in p.get("sources", []):
            i, new = _upsert(conn, "data_sources", {"key": s["key"]},
                {k: s.get(k) for k in ("name", "url", "license", "license_url", "retrieved_at", "verification", "notes")})
            sid[s["key"]] = i; n("sources", new)
        def ecu(rec, variant_id):
            _, new = _upsert(conn, "ecu_definitions", {"variant_id": variant_id, "name": rec["name"], "protocol": rec["protocol"]},
                {"tx_id": rec.get("tx_id"), "rx_id": rec.get("rx_id"), "source_id": sid[rec["source"]]})
            n("ecus", new)
        for m in p.get("manufacturers", []):
            mid, new = _upsert(conn, "manufacturers", {"name": m["name"]}, {"country": m.get("country"), "source_id": sid[m["source"]]}); n("manufacturers", new)
            for mo in m.get("models", []):
                moid, new = _upsert(conn, "vehicle_models", {"manufacturer_id": mid, "name": mo["name"], "generation": mo.get("generation", "")},
                                    {"source_id": sid[mo["source"]]}); n("models", new)
                for v in mo.get("variants", []):
                    vid, new = _upsert(conn, "vehicle_variants",
                        {"model_id": moid, "year_from": v["year_from"], "engine_code": v["engine_code"], "market": v.get("market", "")},
                        {"year_to": v.get("year_to"), "engine_desc": v.get("engine_desc"), "displacement_cc": v.get("displacement_cc"),
                         "fuel": v.get("fuel"), "source_id": sid[v["source"]]}); n("variants", new)
                    for e_ in v.get("ecus", []): ecu(e_, vid)
        for e_ in p.get("generic_ecus", []): ecu(e_, None)
        for d in p.get("dtcs", []):
            _, new = _upsert(conn, "dtc_definitions", {"code": d["code"], "manufacturer_id": None},
                {"system": d["code"][0], "is_generic": 1, "description": d["description"], "source_id": sid[d["source"]]}); n("dtcs", new)
        for d in p.get("pids", []):
            _, new = _upsert(conn, "pid_definitions", {"service": d["service"], "pid": d["pid"]},
                {k: d.get(k) for k in ("name", "num_bytes", "formula", "unit", "min_value", "max_value")} | {"source_id": sid[d["source"]]}); n("pids", new)
        for d in p.get("dids", []):
            _, new = _upsert(conn, "did_definitions", {"did": d["did"], "ecu_id": None},
                {"name": d["name"], "data_type": d["data_type"], "length": d.get("length"), "source_id": sid[d["source"]]}); n("dids", new)
        for d in p.get("services", []):
            _, new = _upsert(conn, "supported_services", {"protocol_family": d["protocol_family"], "service_id": d["service_id"], "ecu_id": None},
                {"name": d["name"], "risk_level": d["risk_level"], "source_id": sid[d["source"]]}); n("services", new)
        conn.execute("RELEASE imp"); conn.commit()
    except Exception:
        conn.execute("ROLLBACK TO imp"); conn.execute("RELEASE imp"); raise
    return stats
