# Protocol definition sources and licences

Consulted 2026-11-27 and 2026-11-28. Nothing here has been verified against the paid SAE/ISO standard texts or against real hardware.

| Items | Source | Licence |
| --- | --- | --- |
| OBD-II mode 01 PIDs, names, units, formulas, support bitmap, DTC two-byte encoding, VIN field length | [Wikipedia: OBD-II PIDs](https://en.wikipedia.org/wiki/OBD-II_PIDs) | CC BY-SA 4.0 ([licence](https://creativecommons.org/licenses/by-sa/4.0/); [Wikipedia move to 4.0](https://creativecommons.org/2023/06/29/wikipedia-moves-to-cc-4-0-licenses/)) |
| Service 02 uses the service 01 PIDs; PID 02 is the trigger DTC, zero means no frame | [Wikipedia: OBD-II PIDs](https://en.wikipedia.org/wiki/OBD-II_PIDs) | CC BY-SA 4.0 |
| Service 02 request `02 PID frame` and response `42 PID frame data`, two byte examples | [AOSP Obd2CommandTest.java](https://android.googlesource.com/platform/packages/services/Car/+/master/tests/obd2_test/src/com/android/car/obd2/test/Obd2CommandTest.java), Copyright 2017 The Android Open Source Project; no code copied | Apache-2.0 |
| ISO-TP frame types, PCI layout, flow status, block size, STmin | [Wikipedia: ISO 15765-2](https://en.wikipedia.org/wiki/ISO_15765-2) (cross-checked with the [Linux kernel ISO-TP documentation](https://docs.kernel.org/7.2/networking/iso15765-2.html), facts only) | CC BY-SA 4.0 |
| UDS service identifiers 0x19/0x22, negative response format | [Wikipedia: Unified Diagnostic Services](https://en.wikipedia.org/wiki/Unified_Diagnostic_Services) | CC BY-SA 4.0 |
| Response code values, sub-function 0x02, DTC status bits and DTC bit layout | [udsoncan](https://pypi.org/project/udsoncan/) ([docs](https://udsoncan.readthedocs.io/)) by pylessard; no code copied | MIT |
| Response code 0x78 (response pending): the client keeps waiting, bounded by an overall request timeout | [udsoncan client documentation](https://udsoncan.readthedocs.io/en/v1.17.1/udsoncan/client.html); no code copied | MIT |

## Attribution and licence review

- `Obd2/Obd2PidCatalog.cs` is adapted from Wikipedia contributors and is offered under CC BY-SA 4.0.
- **Project owner review needed before commercial distribution:** CC BY-SA requires attribution and share-alike for adaptations.

## Left out on purpose

PIDs 07, 08, 09, 3D, 3E, 3F, 48 to 4C and 4E: the formula cells read as empty in the consulted text, so no formula is assumed.

## Assumptions not confirmed by the sources above

Each is marked in code and must be confirmed with real hardware:

- VIN response may carry a leading item count byte `01`.
- DTC responses with an odd body length start with a count byte.
- UDS `59 02` response layout: availability mask followed by 4-byte records.
- ISO-TP consecutive frame sequence starts at 1 and wraps to 0; reserved STmin values are treated as 127 ms.
- Response pending: the final response must arrive within the request timeout; the timeout is not extended.
- Meaning mapping of response codes to `OperationStatus`: 11/12 Unsupported, 7E/7F NotApplicable, all others Failed.
- Freeze frame: only frame 0 is read, and the service 01 support list selects the PIDs to request.
- No DTC descriptions are provided (no licensed source selected).
