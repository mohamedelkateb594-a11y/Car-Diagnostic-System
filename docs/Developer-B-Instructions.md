# Developer B — Vehicle Database & Data Sources

## Mission
Build the vehicle-definition data model, licensed datasets and import/validation pipeline. You own:
- `data/vehicle-definitions/`
- `tools/data-import/`
- `tests/Data.Tests/`
- database schema and data documentation

## Read first
`PROJECT_CONTEXT.md`, `DEVELOPMENT_RULES.md`, `ARCHITECTURE.md`, `API_CONTRACTS.md`, and `TASKS.md`.

## Work sequence
1. Review the shared repository interface in API_CONTRACTS.md.
2. Design SQLite schema for makes, models, variants, ECU definitions, parameters, DTC metadata and sources.
3. Add provenance and license fields before importing substantive data.
4. Create a tiny sample dataset clearly marked as sample/demo.
5. Build import validation, duplicate detection and schema-version handling.
6. Provide repository query examples and tests.
7. Publish a data coverage report describing gaps.

## Minimum provenance fields
Each source/dataset should record:
- Source ID, name and canonical URL.
- License name/identifier and license URL or terms reference.
- Required attribution.
- Collection date and source version.
- Applicable market/vehicle/ECU range.
- Review status and reviewer/date.
- Notes on redistribution and commercial-use restrictions.

## Source policy
Potential sources to evaluate include NHTSA vPIC, OpenDBC and OBDb. Evaluate each source independently. A public repository is not automatically public domain or unrestricted for commercial redistribution. Do not import data until its license and intended use are checked.

## Out of scope
Do not implement protocol communication or WPF UI. Do not modify Diagnostic Core. If a repository interface needs a change, propose it through a contract PR.

## Definition of done
- Schema is documented and versioned.
- Sample data is labeled and traceable.
- Import rejects or flags records without adequate provenance.
- Tests cover schema, validation and duplicate handling.
- Data limitations and license obligations are explicit.

## Handoff
Report Task ID, schema changes, sample/import counts, source/license review, test results, unresolved data gaps, and integration notes for A/C.
