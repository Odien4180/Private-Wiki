# Validation

`project-wiki validate --wiki <path>` performs deterministic structure validation for aliases, redirects, wiki links, and backlinks.

`project-wiki validate --wiki <path> --require-documents --min-coverage 0.7` also performs document quality validation. Results distinguish `structureIssues` from `qualityIssues`.

Quality issue codes include:

- `no_system_documents`
- `no_feature_documents`
- `no_architecture_prose`
- `empty_agent_block`
- `undocumented_important_entity`
- `first_party_coverage_too_low`
- `third_party_noise_too_high`
- `missing_source_evidence`
- `stale_agent_document`

A wiki with only `overview.md` must fail `--require-documents`. Passing validation requires meaningful architecture/system/feature prose, source evidence, linkable documents, and acceptable first-party coverage.
