# Legal, Privacy, and Third-Party Attribution Notice

**GradCast**  
Copyright © 2026 knowthankyew / The GradCast Contributors  
Repository: [https://github.com/knowthankyew/gradcast](https://github.com/knowthankyew/gradcast)

---

## 1. Educational & Financial Simulation Notice
GradCast is a higher-education return-on-investment (ROI), degree earnings transparency, and post-graduation monthly budget simulation engine powered by public federal datasets (U.S. Department of Education College Scorecard, NCES IPEDS, and HUD Fair Market Rents). All projections are informational estimates and do not constitute formal financial, tax, legal, or academic advising.

## 2. Privacy Defaults, Local Storage & Lakehouse k-Anonymity Governance
- **Local-First Saved Scenarios & Transient Simulation Processing**: Personal budget simulation inputs (`POST /api/finance/simulator`) are processed transiently in memory without server persistence, and personal saved scenarios remain strictly client-side in the user's browser `localStorage` by default; GradCast requires no user accounts, login credentials, or personal document uploads.
- **Daily-Salted SHA-256 Client Hashing**: When a user voluntarily shares an anonymous positive financial scenario (`POST /api/scenarios/share`), raw IP addresses are never persisted; only a one-way daily-rotating salted SHA-256 digest (`SHA256(date + ip + salt)`) is stored at the edge for anti-spam rate limiting (`MaxDailySharesPerClient = 50`) and removed prior to `silver.scenarios` conformance.
- **Hard k-Anonymity Boundary (`k >= 5`)**: In the Databricks Medallion Lakehouse tier, `client_hash` and `scenario_title` are stripped prior to `silver.scenarios` conformance, and public benchmark aggregates (`v_public_positive_finds` and `community_scenario_benchmarks`) enforce `HAVING COUNT(*) >= 5` so cohorts smaller than 5 individuals are never exposed.

## 3. Software Bill of Materials (SBOM)
Machine-readable CycloneDX Software Bills of Materials (`bom.json` and `lakehouse/bom.json`) are generated on build in CI and attached to workflow artifacts.

See [LICENSE](./LICENSE) for root license terms.
