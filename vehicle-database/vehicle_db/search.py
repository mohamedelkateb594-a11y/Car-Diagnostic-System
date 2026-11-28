def _esc(s): return str(s).strip().replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_")

def search_vehicles(conn, make=None, model=None, engine=None, year=None, limit=50, variant_id=None, confirmed_only=False):
    """All filters optional; case-insensitive substring match. `year` must fall inside [year_from, year_to or open-ended]."""
    sql = """SELECT v.id AS variant_id, mf.name AS make, mo.name AS model, mo.generation, v.year_from, v.year_to,
                    v.engine_code, v.engine_desc, v.displacement_cc, v.fuel, v.market,
                    s.key AS source_key, s.license, s.verification
             FROM vehicle_variants v JOIN vehicle_models mo ON mo.id=v.model_id
             JOIN manufacturers mf ON mf.id=mo.manufacturer_id JOIN data_sources s ON s.id=v.source_id WHERE 1=1"""
    a = []
    if confirmed_only: sql += " AND s.verification IN ('cross_checked','verified')"
    if variant_id is not None: sql += " AND v.id = ?"; a.append(variant_id)
    for col, val in (("mf.name", make), ("mo.name", model)):
        if val: sql += f" AND {col} LIKE ? ESCAPE '\\'"; a.append(f"%{_esc(val)}%")
    if engine:
        sql += " AND (v.engine_code LIKE ? ESCAPE '\\' OR v.engine_desc LIKE ? ESCAPE '\\')"; a += [f"%{_esc(engine)}%"] * 2
    if year is not None:
        sql += " AND v.year_from <= ? AND (v.year_to IS NULL OR v.year_to >= ?)"; a += [int(year), int(year)]
    sql += " ORDER BY mf.name, mo.name, v.year_from LIMIT ?"; a.append(int(limit))
    return [dict(r) for r in conn.execute(sql, a)]

def list_manufacturers(conn):
    return [r[0] for r in conn.execute("SELECT name FROM manufacturers ORDER BY name")]

def get_vehicle_profile(conn, variant_id):
    """Vehicle ECUs (specific + generic) and the services/PIDs/DIDs catalog. Callers must gate by services[].risk_level."""
    found = search_vehicles(conn, variant_id=variant_id)
    if not found: return None
    q = lambda sql, *a: [dict(r) for r in conn.execute(sql, a)]
    return {"vehicle": found[0],
            "ecus": q("SELECT * FROM ecu_definitions WHERE variant_id=? OR variant_id IS NULL ORDER BY variant_id IS NULL, name", variant_id),
            "services": q("SELECT * FROM supported_services ORDER BY protocol_family, service_id"),
            "pids": q("SELECT * FROM pid_definitions ORDER BY service, pid"),
            "dids": q("SELECT * FROM did_definitions ORDER BY did")}

def lookup_dtc(conn, code):
    r = conn.execute("""SELECT d.code, d.description, d.is_generic, s.key AS source_key, s.license
                        FROM dtc_definitions d JOIN data_sources s ON s.id=d.source_id WHERE d.code=? COLLATE NOCASE""", (code.strip(),)).fetchone()
    return dict(r) if r else None

def list_dtcs(conn, confirmed_only=False, generic_only=False):
    """DTC catalogue with provenance. confirmed_only keeps cross_checked/verified sources; generic_only drops OEM-specific codes."""
    sql = """SELECT d.code, d.description, d.is_generic, s.key AS source_key, s.url AS source_url, s.verification, s.retrieved_at
             FROM dtc_definitions d JOIN data_sources s ON s.id=d.source_id WHERE 1=1"""
    if confirmed_only: sql += " AND s.verification IN ('cross_checked','verified')"
    if generic_only: sql += " AND d.is_generic=1"
    return [dict(r) for r in conn.execute(sql + " ORDER BY d.code")]
