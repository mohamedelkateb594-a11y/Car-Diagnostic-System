"""Local data tool: python -m vehicle_db <command>. All output is JSON on stdout; errors go to stderr with exit code 2.
Commands: init, validate, import, update, pack, info, search."""
import argparse, json, sys
from .database import connect, init_db, load_seed
from .importer import validate_payload, import_payload, ImportValidationError
from .updater import apply_update_package, build_update_package, current_data_version, UpdateError
from .search import search_vehicles, list_dtcs

def _load(path):
    with open(path, encoding="utf-8") as f: return json.load(f)

def _out(obj): print(json.dumps(obj, ensure_ascii=False, indent=2))

def _db(args):
    return init_db(connect(args.db))

def main(argv=None):
    ap = argparse.ArgumentParser(prog="vehicle_db", description="Vehicle database import/update tool")
    sub = ap.add_subparsers(dest="cmd", required=True)
    def db_arg(p): p.add_argument("--db", required=True, help="SQLite file (created if missing)")
    p = sub.add_parser("init", help="create schema (and load bundled seed data)"); db_arg(p); p.add_argument("--no-seed", action="store_true")
    p = sub.add_parser("validate", help="validate a payload file without touching any database"); p.add_argument("file")
    p = sub.add_parser("import", help="import a payload file (all-or-nothing)"); db_arg(p); p.add_argument("file")
    p = sub.add_parser("update", help="apply a signed-by-checksum update package (version must increase)"); db_arg(p); p.add_argument("file")
    p = sub.add_parser("pack", help="wrap a payload into an update package"); p.add_argument("file"); p.add_argument("--version", type=int, required=True); p.add_argument("-o", "--out")
    p = sub.add_parser("info", help="row counts, data version, verification breakdown"); db_arg(p)
    p = sub.add_parser("search", help="search vehicles or list DTCs"); db_arg(p)
    for a in ("make", "model", "engine"): p.add_argument("--" + a)
    p.add_argument("--year", type=int); p.add_argument("--confirmed-only", action="store_true"); p.add_argument("--dtcs", action="store_true", help="list DTCs instead of vehicles")
    args = ap.parse_args(argv)
    try:
        if args.cmd == "validate":
            errs = validate_payload(_load(args.file)); _out({"valid": not errs, "errors": errs}); return 0 if not errs else 2
        if args.cmd == "pack":
            pkg = build_update_package(_load(args.file), args.version)
            if args.out:
                with open(args.out, "w", encoding="utf-8") as f: json.dump(pkg, f, ensure_ascii=False)
                _out({"written": args.out, "version": pkg["version"], "sha256": pkg["sha256"]})
            else: _out(pkg)
            return 0
        c = _db(args)
        try:
            if args.cmd == "init": _out({"db": args.db, "seed": None if args.no_seed else load_seed(c)})
            elif args.cmd == "import": _out({"imported": import_payload(c, _load(args.file))})
            elif args.cmd == "update": _out({"applied": apply_update_package(c, _load(args.file)), "data_version": current_data_version(c)})
            elif args.cmd == "info":
                n = lambda t: c.execute(f"SELECT COUNT(*) FROM {t}").fetchone()[0]
                tables = ("manufacturers", "vehicle_models", "vehicle_variants", "ecu_definitions", "dtc_definitions", "pid_definitions", "did_definitions", "supported_services", "data_sources")
                _out({"data_version": current_data_version(c), "counts": {t: n(t) for t in tables},
                      "sources_by_verification": {r[0]: r[1] for r in c.execute("SELECT verification, COUNT(*) FROM data_sources GROUP BY verification")}})
            elif args.cmd == "search":
                if args.dtcs: _out(list_dtcs(c, confirmed_only=args.confirmed_only))
                else: _out(search_vehicles(c, make=args.make, model=args.model, engine=args.engine, year=args.year, confirmed_only=args.confirmed_only))
        finally:
            c.close()
        return 0
    except (ImportValidationError, UpdateError, OSError, json.JSONDecodeError) as ex:
        print(f"error: {ex}", file=sys.stderr); return 2

if __name__ == "__main__":
    sys.exit(main())
