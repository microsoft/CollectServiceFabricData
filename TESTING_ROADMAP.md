# Testing & Quality Improvement Roadmap

Branch: feature/testing-roadmap

Status: Active (Phase 0 initialization)

Owner: (fill in)

Last Updated: 2025-09-11

## Goals

Establish sustainable, high-confidence test & quality framework for CollectSFData enabling safe refactors, feature velocity, and reliability.

## Success Metrics

- Progressive code coverage thresholds (core logic): 50% → 70% → 85%+
- <5% flaky test rate over rolling 30 days
- Zero secret tokens/keys in logs or saved configs (automated grep gate)
- Mutation score (core) ≥65% by Phase 5
- Time to add new ingestion type reduced (qualitative dev feedback)

## Phase Plan

| Phase | Focus | Deliverables | Target Coverage | Status |
|-------|-------|-------------|-----------------|--------|
| 0 | Baseline setup | Test infra, initial unit tests (time, file type, table naming), coverage reporting | ~10-15% | In Progress |
| 1 | Seams & Decomposition | Interfaces (IClock, IFileSystem, IHttp), config refactor facade | 25-35% | Pending |
| 2 | Core Logic | Validation tests, merge precedence, golden-file parser fixtures | 40-50% | Pending |
| 3 | Integration Harness | Azurite setup, Kusto stub, E2E CLI happy/empty/error | 55-65% | Pending |
| 4 | Concurrency & Reliability | Replace CustomTaskManager, cancellation & timeout tests | 65-75% | Pending |
| 5 | Performance & Mutation | Benchmarks, Stryker, hotspot optimizations | 70-80% | Pending |
| 6 | Security & Hardening | Secret redaction, threat model checklist | 75-85% | Pending |
| 7 | Legacy Rationalization | TFM review (drop net462?), simplify conditionals | 80-85% | Pending |
| 8 | Governance & Polish | Analyzer enforcement, docs, contributor test guidelines | 85%+ | Pending |

## Immediate Tasks (Phase 0)

- [ ] Add test project `CollectSFData.Tests`
- [ ] Add baseline unit tests:
  - [ ] `ConfigurationOptions.ConvertToUtcTime`
  - [ ] `ConfigurationOptions.CleanTableName` (indirect via reflection or wrapper)
  - [ ] File type parsing (private ConvertFileType via public side-effect of GatherType)
- [ ] Add coverage tooling (Coverlet + ReportGenerator)
- [ ] Add CI step (deferred to follow-up PR) for `dotnet test --collect:"XPlat Code Coverage"`
- [ ] Add README section pointing to roadmap
- [ ] Add redaction helper placeholder & tests (ensure no direct secret echo)

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Static singletons limit test isolation | Slows test adoption | Introduce seam interfaces early (Phase 1) |
| Custom scheduler complexity | Flaky concurrency tests | Replace with channels workers (Phase 4) |
| Large refactor churn | Merge conflicts | Short-lived incremental PRs per phase |
| Legacy TFMs blocking APIs | Limits modernization | Evaluate usage metrics before removal |

## Test Strategy Summary

See prior analysis for detailed rationale. Key layers: unit (pure logic), integration (local Azurite + stubs), E2E CLI, performance, mutation, security redaction.

## Progress Log

| Date | Phase | Change | Author |
|------|-------|--------|--------|
| 2025-09-11 | 0 | Roadmap file created, test project scaffolded | (fill) |
| 2025-09-11 | 0 | Multi-target framework support (net462;net48;net6.0;net8.0) | (fill) |
| 2025-09-11 | 0 | Initial ConfigurationOptions unit tests (time parsing, file type, table naming) | (fill) |
| 2025-09-11 | 0 | Tests passing on modern frameworks (net6.0, net8.0); legacy signing issue identified | (fill) |

## Current Status (2025-09-11)

✅ **Completed:**
- Test project `CollectSFData.Tests` created with multi-target framework support
- 10 baseline unit tests implemented for `ConfigurationOptions`
- Coverage collection infrastructure in place (Coverlet + TRX)
- Modern frameworks (net6.0, net8.0) passing all tests
- Branch `feature/testing-roadmap` active

⚠️ **Blocked/Issues:**
- Legacy frameworks (net462, net48) failing due to strong name verification
- Error: "Strong name signature could not be verified" for CollectSFDataDll assembly
- Need to resolve assembly signing strategy for test scenarios

🔄 **Next Steps:**
1. Address strong name verification for legacy framework tests
2. Generate initial coverage report and establish baseline
3. Add test project to solution file
4. Document testing guidelines in CONTRIBUTING.md

## Next Steps

1. Scaffold test project & add first three unit tests.
2. Integrate Coverlet & initial coverage badge generation script.
3. Document redaction approach & add failing test (TDD) before implementation.

---

This document is the authoritative plan; update table & log on each material change.
