# DTC verification report (26 codes)

Date: 2026-11-28. Scope: generic OBD-II DTCs in `vehicle_db` seed data.

## Method and limits
- `cross_checked` = confirmed in 2+ public sources, including OEM bulletins hosted by NHTSA where available. **No code is `verified`**: that level needs the SAE J2012 text (paywalled) or OEM documentation of the definition itself, and none was available.
- Original wording is recorded in each source's `notes` (short labels only, one quote per source, no copied text). Wording differences listed below are cosmetic unless stated.
- `UNCHECKED` codes are left as is with the reason; they are flagged `is_generic = 0` so they are not presented as a definition for every manufacturer.
- Changing a stored description was limited to P0171, U0100, U0121 (aligned to the source wording) and B0001, C0035 (neutralised, OEM-specific).

## Summary
| status | count | codes |
|---|---|---|
| cross_checked (this pass) | 9 | P0171, P0300, P0301, P0302, P0303, P0304, P0420, U0100, U0121 |
| cross_checked (pass of 2026-11-27) | 15 | P0100, P0101, P0105, P0110, P0115, P0120, P0130, P0172, P0335, P0340, P0442, P0455, P0500, P0505, P0562 |
| UNCHECKED (OEM-specific) | 2 | B0001, C0035 |
| verified | 0 | - |

## Per code
| code | status | generic | stored description | source key | primary source URL |
|---|---|---|---|---|---|
| B0001 | UNCHECKED | no | Driver frontal stage 1 deployment control (OEM-specific wording; sub-fault in suffix) | dtc-b0001 | https://static.nhtsa.gov/odi/tsbs/2014/SB-10056433-5627.pdf |
| C0035 | UNCHECKED | no | Wheel speed sensor circuit (OEM-specific; GM: left front) | dtc-c0035 | https://static.nhtsa.gov/odi/tsbs/2020/MC-10173400-9999.pdf |
| P0100 | cross_checked | yes | MAF sensor circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0101 | cross_checked | yes | MAF sensor range/performance problem | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0105 | cross_checked | yes | MAP/barometric pressure circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0110 | cross_checked | yes | Intake air temperature circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0115 | cross_checked | yes | Engine coolant temperature circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0120 | cross_checked | yes | Throttle/pedal position sensor A circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0130 | cross_checked | yes | O2 sensor circuit malfunction (bank 1 sensor 1) | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0171 | cross_checked | yes | System too lean (bank 1) | dtc-p0171 | https://static.nhtsa.gov/odi/tsbs/2016/SB-10082722-0699.pdf |
| P0172 | cross_checked | yes | Fuel system too rich (bank 1) | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0300 | cross_checked | yes | Random/multiple cylinder misfire detected | dtc-p0300 | https://static.nhtsa.gov/odi/tsbs/2016/SB-10093297-0699.pdf |
| P0301 | cross_checked | yes | Cylinder 1 misfire detected | dtc-p0301 | https://www.edmunds.com/obd-dtc/p0301.html |
| P0302 | cross_checked | yes | Cylinder 2 misfire detected | dtc-p0302 | https://static.nhtsa.gov/odi/tsbs/2014/SB-10099730-2532.pdf |
| P0303 | cross_checked | yes | Cylinder 3 misfire detected | dtc-p0303 | https://static.nhtsa.gov/odi/tsbs/2015/SB-10099235-2532.pdf |
| P0304 | cross_checked | yes | Cylinder 4 misfire detected | dtc-p0304 | https://static.nhtsa.gov/odi/tsbs/2023/MC-10239677-0001.pdf |
| P0335 | cross_checked | yes | Crankshaft position sensor A circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0340 | cross_checked | yes | Camshaft position sensor A circuit malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0420 | cross_checked | yes | Catalyst system efficiency below threshold (bank 1) | dtc-p0420 | https://static.nhtsa.gov/odi/tsbs/2024/MC-11005589-0001.pdf |
| P0442 | cross_checked | yes | EVAP system small leak detected | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0455 | cross_checked | yes | EVAP system large leak detected | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0500 | cross_checked | yes | Vehicle speed sensor malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0505 | cross_checked | yes | Idle control system malfunction | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| P0562 | cross_checked | yes | System voltage low | sae-j2012-dtc | https://www.edmunds.com/obd-dtc |
| U0100 | cross_checked | yes | Lost communication with ECM/PCM A | dtc-u0100 | https://static.nhtsa.gov/odi/inv/2012/INRD-PE12017-56148P.pdf |
| U0121 | cross_checked | yes | Lost communication with anti-lock brake system (ABS) control module | dtc-u0121 | https://static.nhtsa.gov/odi/inv/2017/INRD-EA17004-10126.pdf |

## Notes on the 11 codes of this pass
- **P0171**: Hyundai bulletin and an FCA bulletin differ only in punctuation. An FCA diesel calibration remaps P0171 to P026C, so remapping exists for specific OEM/engine combinations; the stored meaning is the generic one.
- **P0300-P0304**: OEM bulletins use `Cyl.N`, `Cylinder No.N` or `Cylinder N`; same meaning. P0300 is sometimes shortened to 'random misfire'.
- **P0420**: word order and 'Catalyst'/'Catalytic Converter' differ between OEMs; same meaning. The code alone does not prove the converter is faulty.
- **U0100**: standard label carries an "A" designator (ECM/PCM "A", vs "B" = U0115); some OEM bulletins drop it. Stored as 'ECM/PCM A'.
- **U0121**: some OEMs call the module 'Electronic Brake Control Module'; same module.
- **B0001**: left UNCHECKED. Base title seen in one OEM bulletin with fault type in a suffix (-11, -12, -13, -2B); another OEM labels the same number with 'Ignition Loop'. Wording is manufacturer-dependent.
- **C0035**: left UNCHECKED. A GM bulletin labels it left-front wheel speed sensor circuit; a secondary source says some GM models label it right-front supply. Manufacturer-specific; GM is not in the manufacturers table yet.

## Review flags from the earlier pass
- **P0105**: iatn samples call it a MAP sensor performance fault, the generic list says MAP/BARO circuit malfunction (range/performance is P0106). Stored description follows the generic list. The earlier statement that the meaning matches was too strong; treat as a variance to confirm against SAE J2012.
- The 15 earlier codes have a shared batch source (`sae-j2012-dtc`); their per-code original wording was not recorded and should be if they are promoted later.

## Code changes in this pass
- Importer: optional boolean `is_generic` on DTC records (default true). Validator rejects non-boolean values. No public function signatures or return shapes changed.
- Seed data: 11 per-code sources; old shared unchecked source removed.

## Tests (python3 -m unittest discover -s tests -v)
Result: **22 tests, 22 OK** (run 2026-11-28 on the files in this branch). New tests: all 26 codes traceable (url, date, notes); status counts (24 cross_checked, 2 unchecked, 0 verified); OEM-specific codes not generic and marked UNCHECKED; the 11 codes have their own sources; `is_generic` must be boolean.
