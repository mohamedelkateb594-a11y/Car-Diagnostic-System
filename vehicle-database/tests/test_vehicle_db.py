import copy, json, sys, tempfile, unittest, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from vehicle_db import *
from vehicle_db.updater import current_data_version

def fresh():
    c = connect(); init_db(c); load_seed(c); return c

class Search(unittest.TestCase):
    def setUp(self): self.c = fresh()
    def test_by_make_model_year_engine(self):
        r = search_vehicles(self.c, make="toyota", model="corolla", year=2020)
        self.assertEqual(len(r), 2)
        self.assertEqual(len(search_vehicles(self.c, engine="L15B7", year=2018)), 1)
        self.assertEqual(len(search_vehicles(self.c, engine="tdi")), 1)
    def test_year_bounds(self):
        self.assertEqual(search_vehicles(self.c, make="Honda", year=2022), [])
        self.assertEqual(len(search_vehicles(self.c, make="Nissan", year=2030)), 1)  # open-ended range
    def test_like_wildcards_escaped(self):
        self.assertEqual(search_vehicles(self.c, make="%"), [])
    def test_source_and_license_on_results(self):
        for r in search_vehicles(self.c):
            self.assertTrue(r["license"]); self.assertEqual(r["verification"], "cross_checked")
    def test_profile_and_dtc(self):
        vid = search_vehicles(self.c, make="Ford")[0]["variant_id"]
        p = get_vehicle_profile(self.c, vid)
        self.assertTrue(p["ecus"] and p["pids"] and p["dids"])
        self.assertIsNone(get_vehicle_profile(self.c, 99999))
        self.assertIn("misfire", lookup_dtc(self.c, "p0300")["description"].lower())
        self.assertIsNone(lookup_dtc(self.c, "P9999"))
    def test_documented_vehicle_facts(self):
        # facts confirmed against public sources on 2026-11-27 (see data_sources.notes)
        one = lambda **k: search_vehicles(self.c, **k)[0]
        self.assertEqual(one(engine="M1DA")["displacement_cc"], 998)
        self.assertEqual(one(engine="CZCA")["year_from"], 2014)
        self.assertIsNone(one(engine="M20A-FKS")["year_to"])
        self.assertEqual(len(search_vehicles(self.c, make="Toyota", year=2025)), 2)
    def test_risk_levels_present(self):
        p = get_vehicle_profile(self.c, search_vehicles(self.c)[0]["variant_id"])
        risk = {s["name"]: s["risk_level"] for s in p["services"]}
        self.assertEqual(risk["Input/output control by identifier"], "actuate")
        self.assertEqual(risk["Write data by identifier"], "write")

class Decode(unittest.TestCase):
    def test_pids(self):
        c = fresh()
        row = lambda pid: dict(c.execute("SELECT * FROM pid_definitions WHERE pid=?", (pid,)).fetchone())
        self.assertEqual(decode_pid(row(0x0C), bytes([0x1A, 0xF8])), 1726.0)
        self.assertEqual(decode_pid(row(0x05), bytes([0x7B])), 83)
        with self.assertRaises(ValueError): decode_pid(row(0x0C), bytes([1]))
    def test_formula_rejects_code(self):
        for bad in ("__import__('os')", "A.real", "A**9", "open('x')"):
            with self.assertRaises(Exception): safe_eval(bad, {"A": 1})

class Import(unittest.TestCase):
    def payload(self):
        with open(os.path.join(os.path.dirname(__file__), "..", "vehicle_db", "seed", "seed_data.json"), encoding="utf-8") as f:
            return json.load(f)
    def test_idempotent(self):
        c = fresh(); n = c.execute("SELECT COUNT(*) FROM vehicle_variants").fetchone()[0]
        stats = load_seed(c)
        self.assertEqual(c.execute("SELECT COUNT(*) FROM vehicle_variants").fetchone()[0], n)
        self.assertTrue(all(v == 0 for v in stats.values()))
    def test_validation_errors(self):
        p = self.payload()
        p["dtcs"].append({"code": "X123", "description": "", "source": "nope"})
        p["pids"].append({"service": 1, "pid": 300, "name": "x", "num_bytes": 1, "formula": "__import__('os')", "source": "sae-j1979-pids"})
        p["manufacturers"][0]["models"][0]["variants"].append({"year_from": 2020, "year_to": 2010, "engine_code": "X", "source": "toyota-corolla-e210"})
        p["sources"].append({"key": "nolic", "name": "n", "verification": "verified"})
        p["sources"].append({"key": "nourl", "name": "n", "license": "x", "verification": "cross_checked"})
        errs = validate_payload(p)
        for frag in ("invalid code format", "missing description", "unknown/missing source", "pid out of range", "bad formula", "bad year_to", "missing license", "requires url"):
            self.assertTrue(any(frag in e for e in errs), frag)
    def test_atomic_rollback(self):
        c = connect(); init_db(c)
        p = self.payload(); p["dtcs"][0]["code"] = "bad"
        with self.assertRaises(ImportValidationError): import_payload(c, p)
        self.assertEqual(c.execute("SELECT COUNT(*) FROM manufacturers").fetchone()[0], 0)
    def test_every_row_has_source(self):
        c = fresh()
        for t in ("manufacturers","vehicle_models","vehicle_variants","ecu_definitions","dtc_definitions","pid_definitions","did_definitions","supported_services"):
            self.assertEqual(c.execute(f"SELECT COUNT(*) FROM {t} WHERE source_id IS NULL").fetchone()[0], 0, t)

class Update(unittest.TestCase):
    def pkg(self, v=1, name="Mazda"):
        return build_update_package({
            "sources": [{"key": "t", "name": "t", "license": "CC0", "verification": "sample_unverified"}],
            "manufacturers": [{"name": name, "source": "t", "models": [{"name": "3", "source": "t", "variants": [
                {"year_from": 2019, "engine_code": "PE-VPS", "source": "t"}]}]}]}, v)
    def test_apply_and_search_without_rebuild(self):
        c = fresh(); self.assertEqual(search_vehicles(c, make="Mazda"), [])
        apply_update_package(c, self.pkg()); self.assertEqual(len(search_vehicles(c, make="Mazda")), 1)
        self.assertEqual(current_data_version(c), 1)
    def test_rejects_old_version_and_tamper(self):
        c = fresh(); apply_update_package(c, self.pkg(2))
        with self.assertRaises(UpdateError): apply_update_package(c, self.pkg(2, "Kia"))
        bad = self.pkg(3); bad["payload"]["manufacturers"][0]["name"] = "Evil"
        with self.assertRaises(UpdateError): apply_update_package(c, bad)
        self.assertEqual(search_vehicles(c, make="Evil"), [])
    def test_rejects_invalid_payload_and_keeps_version(self):
        c = fresh(); p = self.pkg(); p["payload"]["manufacturers"][0]["source"] = "missing"
        p = build_update_package(p["payload"], 1)
        with self.assertRaises(UpdateError): apply_update_package(c, p)
        self.assertEqual(current_data_version(c), 0)
    def test_file_db_persists(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "v.db"); c = connect(path); init_db(c); load_seed(c); apply_update_package(c, self.pkg()); c.close()
            c2 = connect(path)
            self.assertEqual(len(search_vehicles(c2, make="Mazda")), 1)
            c2.close()

if __name__ == "__main__": unittest.main()
