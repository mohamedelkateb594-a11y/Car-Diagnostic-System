# vehicle_db — Developer B (Vehicle Database & Data Sources)
Pure-Python (stdlib only, SQLite). Run tests from this folder: `python3 -m unittest discover -s tests -v` (35 tests).

## Use as a library
```python
from vehicle_db import *
c = connect("vehicles.db"); init_db(c); load_seed(c)
search_vehicles(c, make="toyota", model="corolla", year=2020, engine="M20A", confirmed_only=True)
get_vehicle_profile(c, variant_id)      # ECUs + services (risk_level read/write/actuate) + PIDs + DIDs
lookup_dtc(c, "P0300"); list_dtcs(c, confirmed_only=True, generic_only=True)
decode_pid(pid_row, response_bytes)     # PID formulas are evaluated safely (no code execution)
decode_supported_pids(0x00, bytes.fromhex("BE1FA813"))   # which PIDs a car says it supports
pid_support_location(0x0C)              # -> (0x00, 20): the bitmap PID and bit that flag PID 0x0C
apply_update_package(c, pkg)            # runtime data update, no app rebuild
```

## Local import / update tool
```
python -m vehicle_db init     --db vehicles.db            # schema + bundled seed (use --no-seed for empty)
python -m vehicle_db validate payload.json               # check a payload without touching a database
python -m vehicle_db import   --db vehicles.db payload.json   # all-or-nothing, idempotent
python -m vehicle_db pack     payload.json --version 3 -o update_v3.json
python -m vehicle_db update   --db vehicles.db update_v3.json # checksum + strictly increasing version
python -m vehicle_db info     --db vehicles.db            # counts, data version, sources by verification level
python -m vehicle_db search   --db vehicles.db --make honda --year 2018 [--confirmed-only]
python -m vehicle_db search   --db vehicles.db --dtcs --confirmed-only
```
Exit code 0 = ok, 2 = invalid input / rejected package (message on stderr, database left unchanged).

## Payload format
See `vehicle_db/seed/*.json` (all files there are loaded by `init`). Every record needs `source` (a key in `sources`, which requires `license` and `verification`). Sources marked `cross_checked` or `verified` must also have `url`, `retrieved_at` and `notes` (enforced by the validator). DTC records accept optional boolean `is_generic` (default true; false for manufacturer-specific codes). PID records carry code, name, unit, formula, min/max and `num_bytes`; support information comes from the bitmap PIDs 0x00/0x20/0x40 (decoded with `decode_supported_pids`), which is read from the actual car at run time.

## Verification levels (`data_sources.verification`)
| level | meaning |
|---|---|
| `standard` | public standard identifier, not yet independently cross-checked (also used for UNCHECKED OEM-specific codes, see their notes) |
| `cross_checked` | confirmed against 2+ public sources (URLs in `notes`), including OEM bulletins hosted by NHTSA where available; NOT the SAE/ISO standard text |
| `verified` | confirmed against the standard text or OEM definition documentation (none yet) |
| `sample_unverified` | illustrative only |

`--confirmed-only` / `confirmed_only=True` returns only `cross_checked` and `verified` data, so confirmed and unconfirmed sets stay separable.

## Data sources and licences (as of 2026-11-28)
| dataset | source | licence / terms | status |
|---|---|---|---|
| Vehicles (6 models, 9 engines) | Wikipedia articles, NHTSA TSBs, parts/fitment catalogs (URLs per source in `notes`) | facts only; Wikipedia text CC BY-SA 4.0; no text copied | cross_checked |
| PIDs 0x04-0x5C (15) + bitmaps 0x00/0x20/0x40 | SAE J1979 as documented by Wikipedia OBD-II PIDs, csselectronics, x-engineer, slideshare deck | facts only; project-written names | cross_checked |
| DTCs, 15 codes | edmunds.com, iatn.net, generic DTC list | code numbers are identifiers; descriptions are project paraphrases | cross_checked (batch) |
| DTCs, 9 codes | OEM bulletins and NHTSA documents, one source per code | short label quoted only | cross_checked |
| DTCs B0001, C0035 | OEM-specific, evidence summarised in notes | - | UNCHECKED, `is_generic = 0` |
| UDS services/DIDs, CAN IDs | ISO 14229 / ISO 15765-4 identifiers, project-written names | facts only | standard (not cross-checked) |

Per-code DTC detail: [docs/DTC_VERIFICATION_REPORT.md](docs/DTC_VERIFICATION_REPORT.md). Nothing is `verified`: that needs the SAE/ISO texts or OEM documentation.

## Known limits
- Hyundai Elantra AD 1.6 engine code is market-dependent (G4FG vs G4FC); Toyota 2ZR-FXE displacement 1797 vs 1798 cc across sources.
- Update packages: sha256 integrity only; add signature verification before remote distribution (hook: `updater._verify_integrity`).
- Python was chosen because the project stack was not known; `schema.sql` and the JSON formats are language-neutral.
