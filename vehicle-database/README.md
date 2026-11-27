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
Import payload: see `vehicle_db/seed/seed_data.json`. Every record needs `source` (key into `sources`, which requires `license` and `verification`).

## Data status (important)
- PIDs / DTC codes / UDS services+DIDs / CAN IDs: standardized identifiers; descriptions are project-written. Verify against SAE J1979/J2012 and ISO 14229/15765 before release.
- Vehicles (`sample-vehicles`): **sample_unverified** — approximate, not checked against manufacturer docs. Do not ship as "verified".
- Update packages: sha256 integrity only; add signature verification before remote distribution (hook: `updater._verify_integrity`).
- Python was chosen because the project stack was not known; schema.sql + JSON formats are language-neutral.
