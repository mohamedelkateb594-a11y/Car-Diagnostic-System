import contextlib, io, json, os, sys, tempfile, unittest
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from vehicle_db import *
from vehicle_db.cli import main

def fresh():
    c = connect(); init_db(c); load_seed(c); return c

def run(*argv):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = main(list(argv))
    return code, (json.loads(out.getvalue()) if out.getvalue().strip() else None), err.getvalue()

class PidSupport(unittest.TestCase):
    def test_wikipedia_worked_example(self):
        # BE1FA813 is the documented example response for Mode 01 PID 00
        got = decode_supported_pids(0x00, bytes.fromhex("BE1FA813"))
        self.assertEqual(got, [0x01,0x03,0x04,0x05,0x06,0x07,0x0C,0x0D,0x0E,0x0F,0x10,0x11,0x13,0x15,0x1C,0x1F,0x20])
    def test_location(self):
        self.assertEqual(pid_support_location(0x01), (0x00, 31))
        self.assertEqual(pid_support_location(0x0C), (0x00, 20))
        self.assertEqual(pid_support_location(0x20), (0x00, 0))
        self.assertEqual(pid_support_location(0x21), (0x20, 31))
        with self.assertRaises(ValueError): pid_support_location(0)
    def test_location_consistent_with_decoder(self):
        for pid in (0x05, 0x1F, 0x2F, 0x42, 0x5C):
            base, bit = pid_support_location(pid)
            data = (1 << bit).to_bytes(4, "big")
            self.assertEqual(decode_supported_pids(base, data), [pid])
    def test_bitmap_pids_in_seed_and_decodable(self):
        c = fresh()
        rows = {r["pid"]: dict(r) for r in c.execute("SELECT * FROM pid_definitions WHERE pid IN (0,32,64)")}
        self.assertEqual(sorted(rows), [0, 32, 64])
        self.assertEqual(decode_pid(rows[0], bytes.fromhex("BE1FA813")), 0xBE1FA813)
    def test_decoder_input_checks(self):
        with self.assertRaises(ValueError): decode_supported_pids(0x00, b"\x00")
        with self.assertRaises(ValueError): decode_supported_pids(0x05, b"\x00\x00\x00\x00")

class Confirmed(unittest.TestCase):
    def test_filters(self):
        c = fresh()
        self.assertEqual(len(list_dtcs(c)), 26)
        self.assertEqual(len(list_dtcs(c, confirmed_only=True)), 24)
        self.assertEqual({d["code"] for d in list_dtcs(c, generic_only=True)} & {"B0001", "C0035"}, set())
        self.assertEqual(len(search_vehicles(c, confirmed_only=True)), len(search_vehicles(c)))
    def test_unconfirmed_vehicle_filtered_out(self):
        c = fresh()
        apply_update_package(c, build_update_package({
            "sources": [{"key": "t", "name": "t", "license": "CC0", "verification": "sample_unverified"}],
            "manufacturers": [{"name": "Mazda", "source": "t", "models": [{"name": "3", "source": "t", "variants": [
                {"year_from": 2019, "engine_code": "PE-VPS", "source": "t"}]}]}]}, 1))
        self.assertEqual(len(search_vehicles(c, make="Mazda")), 1)
        self.assertEqual(search_vehicles(c, make="Mazda", confirmed_only=True), [])

class Cli(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.TemporaryDirectory(); self.db = os.path.join(self.d.name, "v.db")
    def tearDown(self): self.d.cleanup()
    def write(self, name, obj):
        p = os.path.join(self.d.name, name)
        with open(p, "w", encoding="utf-8") as f: json.dump(obj, f)
        return p
    payload = {"sources": [{"key": "t", "name": "t", "license": "CC0", "verification": "sample_unverified"}],
               "manufacturers": [{"name": "Kia", "source": "t", "models": [{"name": "Rio", "source": "t", "variants": [
                   {"year_from": 2018, "engine_code": "G4LC", "source": "t"}]}]}]}
    def test_init_info_search(self):
        code, out, _ = run("init", "--db", self.db); self.assertEqual(code, 0); self.assertEqual(out["seed"]["dtcs"], 26)
        code, out, _ = run("info", "--db", self.db)
        self.assertEqual(out["counts"]["dtc_definitions"], 26); self.assertEqual(out["data_version"], 0)
        self.assertEqual(out["sources_by_verification"]["standard"], 4)
        code, out, _ = run("search", "--db", self.db, "--make", "toyota", "--year", "2020"); self.assertEqual(len(out), 2)
        code, out, _ = run("search", "--db", self.db, "--dtcs", "--confirmed-only"); self.assertEqual(len(out), 24)
    def test_validate_good_and_bad(self):
        good = self.write("good.json", self.payload); bad = self.write("bad.json", {"dtcs": [{"code": "X1", "description": "", "source": "n"}]})
        self.assertEqual(run("validate", good)[0], 0)
        code, out, _ = run("validate", bad); self.assertEqual(code, 2); self.assertFalse(out["valid"]); self.assertTrue(out["errors"])
    def test_import_rejects_invalid_and_leaves_db_untouched(self):
        run("init", "--db", self.db, "--no-seed")
        bad = self.write("bad.json", {"dtcs": [{"code": "X1", "description": "", "source": "n"}]})
        code, _, err = run("import", "--db", self.db, bad); self.assertEqual(code, 2); self.assertIn("error:", err)
        self.assertEqual(run("info", "--db", self.db)[1]["counts"]["dtc_definitions"], 0)
    def test_import_then_pack_update_flow(self):
        run("init", "--db", self.db)
        code, out, _ = run("import", "--db", self.db, self.write("p.json", self.payload)); self.assertEqual(code, 0)
        self.assertEqual(len(run("search", "--db", self.db, "--make", "kia")[1]), 1)
        upd = {**self.payload, "manufacturers": [{**self.payload["manufacturers"][0], "name": "Mazda"}]}
        pkg_path = os.path.join(self.d.name, "pkg.json")
        code, out, _ = run("pack", self.write("u.json", upd), "--version", "1", "-o", pkg_path); self.assertEqual(code, 0)
        code, out, _ = run("update", "--db", self.db, pkg_path); self.assertEqual(code, 0); self.assertEqual(out["data_version"], 1)
        self.assertEqual(len(run("search", "--db", self.db, "--make", "mazda")[1]), 1)
        code, _, err = run("update", "--db", self.db, pkg_path); self.assertEqual(code, 2)   # same version again
    def test_tampered_package_rejected(self):
        run("init", "--db", self.db)
        pkg = build_update_package(self.payload, 1); pkg["payload"]["manufacturers"][0]["name"] = "Evil"
        code, _, err = run("update", "--db", self.db, self.write("t.json", pkg))
        self.assertEqual(code, 2); self.assertIn("checksum", err)
        self.assertEqual(run("search", "--db", self.db, "--make", "evil")[1], [])
    def test_missing_file_is_clean_error(self):
        code, _, err = run("validate", os.path.join(self.d.name, "nope.json")); self.assertEqual(code, 2); self.assertIn("error:", err)

if __name__ == "__main__": unittest.main()
