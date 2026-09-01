# Quality Gates

Use this reference before completing any authoring task.

## Required order

Backlinks must exist before validation. Run:

```bash
project-wiki build --wiki <wiki>
project-wiki validate --wiki <wiki> --require-documents --min-coverage 0.7
```

`project-wiki navigation build --wiki <wiki>` may be used instead of a full site build when only backlinks need refreshing.

## Issue fixes

- `no_system_documents`: create at least one meaningful system document from first-party evidence.
- `no_feature_documents`: create at least one feature document tied to user-visible behavior or workflow.
- `no_architecture_prose`: add architecture explanation in an AGENT block with source-grounded claims.
- `empty_agent_block`: write evidence-backed prose or remove the empty AGENT block.
- `undocumented_important_entity`: document or link the important first-party entity.
- `first_party_coverage_too_low`: cover more important first-party entities; do not pad docs with noisy lists.
- `third_party_noise_too_high`: reduce external package focus or refine scope exclusions.
- `missing_source_evidence`: add exact source paths and known lines where available.
- `stale_agent_document`: update or remove links to deleted/renamed entities after checking update impact.

## Completion criteria

A complete wiki has architecture, system, and feature prose; source captions; unambiguous wiki links; rebuilt backlinks; passing validation; and a built static site.
