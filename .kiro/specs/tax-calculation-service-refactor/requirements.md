# Requirements Document

## Introduction

This feature refactors `TaxCalculationService` in the GradCast ASP.NET Core API to introduce a clean abstraction boundary between tax calculation logic and the source of tax configuration data. Today, the service loads a versioned JSON file from disk in its constructor, taking a hard dependency on `IWebHostEnvironment`. This couples the calculation logic to ASP.NET hosting infrastructure, prevents unit testing without spinning up a host, and makes it impossible to swap the config source without modifying the service itself.

The refactor introduces two interfaces: `ITaxCalculationService` (so endpoints bind to an abstraction) and `ITaxConfigProvider` (so the service receives tax data through a seam rather than reading it directly). A `FileTaxConfigProvider` preserves the current "load the latest versioned JSON from disk" behavior. `TaxCalculationService` is made unit-testable without ASP.NET hosting infrastructure. The door is opened to future config sources (database, remote API, multi-year selection) without touching calculation logic.

## Glossary

- **TaxCalculationService**: The existing concrete class that computes federal/FICA/state taxes and returns a `NetPayResult`. Post-refactor, it depends on `ITaxConfigProvider` instead of `IWebHostEnvironment`.
- **ITaxCalculationService**: New interface extracted from `TaxCalculationService`, exposing `Calculate(decimal grossAnnualSalary, string state)` and `TaxYear`.
- **ITaxConfigProvider**: New interface that abstracts the source of tax configuration data. Exposes a synchronous `GetConfig()` method returning a `TaxConfig` record.
- **TaxConfig**: An immutable value object (C# `record`) carrying all data needed for a tax calculation: tax year, standard deduction, FICA rates/caps, federal brackets, and state rates. Must be `public` so tests and providers can construct it without reflection.
- **TaxBracket**: An immutable value object (C# `record`) representing a single federal tax bracket with `UpperBound` and `Rate`. Must be `public` for the same reason.
- **FileTaxConfigProvider**: Concrete implementation of `ITaxConfigProvider` that preserves the existing "find the latest `tax_config_YYYY.json` in `Configuration/TaxData/`, parse it, return a `TaxConfig`" behavior.
- **FinanceEndpoints**: The existing minimal-API endpoint class that currently takes `TaxCalculationService` as a concrete parameter. Post-refactor, it depends on `ITaxCalculationService`.
- **NetPayResult**: Existing record returned by `Calculate`, unchanged.

---

## Requirements

### Requirement 1: Extract ITaxCalculationService Interface

**User Story:** As a developer, I want the finance endpoints to depend on an abstraction rather than a concrete class, so that the service can be swapped or mocked without changing endpoint code.

#### Acceptance Criteria

1. THE `ITaxCalculationService` interface SHALL declare a synchronous `NetPayResult Calculate(decimal grossAnnualSalary, string state)` method.
2. THE `ITaxCalculationService` interface SHALL declare a read-only `int TaxYear` property.
3. THE `TaxCalculationService` class SHALL implement `ITaxCalculationService`.
4. WHEN the `CalculateNetPay` handler in `FinanceEndpoints` declares its tax service parameter, THE declared parameter type SHALL be `ITaxCalculationService`, not `TaxCalculationService`.
5. THE `Program` SHALL register `TaxCalculationService` as the singleton implementation of `ITaxCalculationService` in the DI container (preventing scoped or transient registrations that would break the file-loaded config pattern).

---

### Requirement 2: Extract ITaxConfigProvider Abstraction

**User Story:** As a developer, I want tax configuration loading decoupled from tax calculation, so that I can supply different config sources (file, database, API) without modifying calculation logic.

#### Acceptance Criteria

1. THE `ITaxConfigProvider` interface SHALL declare a single synchronous method `TaxConfig GetConfig()` that returns a fully populated `TaxConfig` instance containing all fields required for tax calculation (tax year, standard deduction, Social Security rate, Social Security wage cap, Medicare rate, federal brackets, state tax rates).
2. THE `TaxCalculationService` constructor SHALL accept `ITaxConfigProvider` as its sole parameter, with no dependency on `IWebHostEnvironment` or any other ASP.NET infrastructure type.
3. THE `TaxCalculationService` SHALL call `ITaxConfigProvider.GetConfig()` to obtain tax configuration data and SHALL NOT read files, environment variables, or any configuration infrastructure directly.
4. THE `TaxConfig` type SHALL be a `public` immutable C# `record` with all required fields declared as constructor parameters or `init`-only properties, so providers can construct instances without inheritance or reflection.
5. IF `ITaxConfigProvider.GetConfig()` returns a `TaxConfig` with a `null` or empty `FederalBrackets` collection, THEN `TaxCalculationService.Calculate` SHALL return a `NetPayResult` with `FederalTaxAnnual` of `0` (not throw), treating the empty bracket list as zero federal tax.

---

### Requirement 3: Implement FileTaxConfigProvider

**User Story:** As an operator, I want the existing versioned-JSON-from-disk behavior preserved after the refactor, so that the live application continues to work without any config file changes.

#### Acceptance Criteria

1. THE `FileTaxConfigProvider` SHALL implement `ITaxConfigProvider`.
2. WHEN `FileTaxConfigProvider` is constructed, THE `FileTaxConfigProvider` SHALL accept `IWebHostEnvironment` to resolve the content root path where tax config files are stored.
3. WHEN `FileTaxConfigProvider` provides tax config, THE `FileTaxConfigProvider` SHALL scan the `Configuration/TaxData/` directory under the content root for files matching the pattern `tax_config_*.json`, sort the matching filenames in descending lexicographic order, and select the first file in that sorted list (preserving the "highest year wins" behavior).
4. IF the `Configuration/TaxData/` directory does not exist under the content root, THEN THE `FileTaxConfigProvider` SHALL throw an `InvalidOperationException` with an error message identifying the expected directory path.
5. IF the `Configuration/TaxData/` directory exists but contains no files matching `tax_config_*.json`, THEN THE `FileTaxConfigProvider` SHALL throw an `InvalidOperationException` with an error message indicating that no matching tax configuration files were found.
6. WHEN `FileTaxConfigProvider` parses the selected JSON file, THE `FileTaxConfigProvider` SHALL populate a `TaxConfig` record with the following fields mapped from the JSON: `taxYear` (integer), `standardDeduction` (decimal), `socialSecurityRate` (decimal), `socialSecurityWageCap` (decimal), `medicareRate` (decimal), each element of `federalBrackets` as a `TaxBracket` with `upperBound` (decimal) and `rate` (decimal), and each entry of `stateTaxRates` as a state-code-to-decimal-rate mapping with state codes normalized to uppercase.
7. IF the selected JSON file cannot be parsed or is missing any required field listed in criterion 6, THEN THE `FileTaxConfigProvider` SHALL allow the resulting `JsonException` or `InvalidOperationException` to propagate to the caller without catching it.
8. THE `Program` SHALL register `FileTaxConfigProvider` as the implementation of `ITaxConfigProvider` in the DI container with singleton lifetime.

---

### Requirement 4: Preserve Calculation Correctness

**User Story:** As a user, I want the tax calculation results to remain identical after the refactor, so that existing consumers of the `/api/finance/net-pay` endpoint are not affected.

#### Acceptance Criteria

1. WHEN `Calculate` is called with a `grossAnnualSalary` ≥ 0 and a valid `state` code, THE `TaxCalculationService` SHALL produce a `NetPayResult` where each monetary field (`GrossAnnual`, `GrossMonthly`, `FederalTaxAnnual`, `FederalTaxMonthly`, `FicaAnnual`, `FicaMonthly`, `StateTaxAnnual`, `StateTaxMonthly`, `NetAnnual`, `NetMonthly`) equals the value that would be produced by applying the pre-refactor formulas to the same inputs and the same `TaxConfig` data, rounded to 2 decimal places.
2. THE `TaxCalculationService` SHALL compute federal taxable income as `Math.Max(0, grossAnnualSalary - config.StandardDeduction)` before applying brackets, preserving the standard-deduction floor at zero.
3. THE `TaxCalculationService` SHALL compute the Social Security component of FICA as `Math.Min(grossAnnualSalary, config.SocialSecurityWageCap) * config.SocialSecurityRate`, preserving the wage-cap ceiling.
4. IF a state code is not present in the `TaxConfig` state rates dictionary, THEN THE `TaxCalculationService` SHALL apply a fallback rate of `0.04m` (4%) to `grossAnnualSalary` (not taxable income) when computing `StateTaxAnnual`, matching the current default behavior.
5. THE `TaxCalculationService` SHALL apply `Math.Round(..., 2)` to `GrossMonthly`, `FederalTaxAnnual`, `FederalTaxMonthly`, `FicaAnnual`, `FicaMonthly`, `StateTaxAnnual`, `StateTaxMonthly`, `NetAnnual`, and `NetMonthly` before populating `NetPayResult`, preserving existing rounding semantics across all monetary fields.
6. THE `TaxCalculationService` SHALL perform state code lookup case-insensitively (e.g., "ca", "CA", and "Ca" all resolve to the same rate), matching the current `state.ToUpperInvariant()` normalization.

---

### Requirement 5: Unit Testability Without ASP.NET Infrastructure

**User Story:** As a developer, I want to test `TaxCalculationService` by supplying a stub `ITaxConfigProvider`, so that tests do not require an `IWebHostEnvironment` or access to the file system.

#### Acceptance Criteria

1. THE `TaxCalculationService` constructor SHALL accept only `ITaxConfigProvider` as a parameter, with no dependency on `IWebHostEnvironment`, `IConfiguration`, `IHostEnvironment`, or any other ASP.NET infrastructure type, so that a test can instantiate it with `new TaxCalculationService(stubProvider)`.
2. WHEN a test supplies a stub `ITaxConfigProvider.GetConfig()` returning a fully specified `TaxConfig`, THE resulting `NetPayResult` fields (`GrossAnnual`, `FederalTaxAnnual`, `FicaAnnual`, `StateTaxAnnual`, `NetAnnual`, `NetMonthly`, `EffectiveTaxRate`) SHALL each equal the value calculated by hand from the stub config values and the given inputs.
3. THE `TaxConfig` record and `TaxBracket` record SHALL be `public` types constructable inline in a test (e.g., `new TaxConfig(...)`) without any file I/O, host environment, or DI container.
4. IF a test supplies a stub `ITaxConfigProvider` for an unrecognised state code, THE `TaxCalculationService` SHALL apply the 4% fallback rate defined in Requirement 4, Criterion 4, producing a verifiable `StateTaxAnnual` of `grossAnnualSalary * 0.04m` rounded to 2 decimal places.
