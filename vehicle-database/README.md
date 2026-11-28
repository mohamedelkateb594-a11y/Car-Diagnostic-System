# vehicle_db — Developer B (Vehicle Database & Data Sources)
Pure-Python (stdlib only, SQLite). Run tests from this folder: `python3 -m unittest discover -s tests -v`

```python
from vehicle_db import *
c = connect("vehicles.db"); init_db(c); load_seed(c)
search_vehicles(c, make="toyota", model="corolla", year=2020, engine="M20A")
get_vehicle_profile(c, variant_id)      # ECUs + services (risk_level read/write/actuate) + PIDs + DIDs
lookup_dtc(c, "P0300")
apply_update_package(c, pkg)            # runtime data update, no app rebuild
```
Import payload: see `vehicle_db/seed/seed_data.json`. Every record needs `source` (key into `sources`, which requires `license` and `verification`). Sources marked `cross_checked` or `verified` must also have `url`, `retrieved_at` and `notes` (enforced by the validator). DTC records accept an optional boolean `is_generic` (default true; set false for manufacturer-specific codes).

## Verification levels (`data_sources.verification`)
| level | meaning |
|---|---|
| `standard` | public standard identifier, not yet independently cross-checked (also used for UNCHECKED OEM-specific codes, see their notes) |
| `cross_checked` | confirmed against 2+ public sources (URLs in `notes`), including OEM bulletins hosted by NHTSA where available; NOT the SAE/ISO standard text |
| `verified` | confirmed against the standard text or OEM definition documentation (none yet) |
| `sample_unverified` | illustrative only |

## Data status (as of 2026-11-28)
- **Vehicles (6 models, 9 engines):** `cross_checked`. Each source's `notes` lists the cross-check URLs and discrepancies (e.g. Hyundai Elantra AD 1.6 engine code is market-dependent: G4FG vs G4FC; Toyota 2ZR-FXE displacement 1797 vs 1798 cc).
- **PIDs:** all 15 formulas cross-checked.
- **DTCs (26):** 24 `cross_checked`, 2 UNCHECKED (B0001, C0035: OEM-specific, `is_generic = 0`), 0 `verified`. Full per-code status, sources and variances: [docs/DTC_VERIFICATION_REPORT.md](docs/DTC_VERIFICATION_REPORT.md).
- **UDS services/DIDs, CAN IDs:** project-written, not yet cross-checked. Verify against SAE J1979/J2012 and ISO 14229/15765 before release.
- Update packages: sha256 integrity only; add signature verification before remote distribution (hook: `updater._verify_integrity`).
- Python was chosen because the project stack was not known; schema.sql + JSON formats are language-neutral.
