# Implementation Plan: Tax Calculation Service Refactor

## Overview

Introduce `ITaxCalculationService` and `ITaxConfigProvider` interfaces to decouple tax calculation logic from file-based config loading. Extract `FileTaxConfigProvider` to carry the existing disk-read behavior, promote `TaxConfig`/`TaxBracket` to public records, update `FinanceEndpoints` to bind against the new interface, rewire DI in `Program.cs`, and add a unit/property-test project using xUnit + FsCheck.

## Tasks

- [ ] 1. Promote `TaxConfig` and `TaxBracket` to public records
  - [ ] 1.1 Create `src/api/Services/TaxConfig.cs` defining `public record TaxBracket(decimal UpperBound, decimal Rate)` and `public record TaxConfig` with all required `init`-only properties (`TaxYear`, `StandardDeduction`, `SocialSecurityRate`, `SocialSecurityWageCap`, `MedicareRate`, `FederalBrackets`, `StateTaxRates`)
    - Use `IReadOnlyList<TaxBracket>` for `FederalBrackets` (default `[]`)
    - Use `IReadOnlyDictionary<string, decimal>` for `StateTaxRates` (default `new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)`)
    - Both types must be `public` so test code and providers can construct them without reflection or inheritance
    - _Requirements: 2.4, 5.3_

- [ ] 2. Define `ITaxConfigProvider` and `ITaxCalculationService` interfaces
  - [ ] 2.1 Create `src/api/Services/ITaxConfigProvider.cs` declaring `public interface ITaxConfigProvider` with a single synchronous method `TaxConfig GetConfig()`
    - _Requirements: 2.1_
  - [ ] 2.2 Create `src/api/Services/ITaxCalculationService.cs` declaring `public interface ITaxCalculationService` with `int TaxYear { get; }` and `NetPayResult Calculate(decimal grossAnnualSalary, string state)`
    - _Requirements: 1.1, 1.2_

- [ ] 3. Refactor `TaxCalculationService` to implement `ITaxCalculationService` and depend on `ITaxConfigProvider`
  - [ ] 3.1 Modify `src/api/Services/TaxCalculationService.cs`:
    - Add `ITaxCalculationService` to the class declaration
    - Replace the `IWebHostEnvironment` constructor parameter with `ITaxConfigProvider configProvider`
    - Replace the `LoadConfig` call with `_config = configProvider.GetConfig()`
    - Remove the private `LoadConfig` static method and the private nested `TaxConfig`/`TaxBracket` types (now replaced by public ones)
    - Keep `Calculate` and `CalculateFederalTax` implementations verbatim — no formula changes
    - _Requirements: 1.3, 2.2, 2.3, 5.1_
  - [ ]* 3.2 Write unit tests for `TaxCalculationService` constructor and basic calculation
    - Verify instantiation with `new TaxCalculationService(stubProvider)` — no `IWebHostEnvironment` needed
    - Verify `GetConfig()` is called exactly once during construction (counting stub)
    - Verify a known salary/state combination (e.g. $75,000 in CA) produces expected rounded `NetPayResult` fields
    - Verify SS wage cap edge case: salary above and below `SocialSecurityWageCap`
    - Verify standard deduction floor: salary below `StandardDeduction` produces `FederalTaxAnnual = 0`
    - _Requirements: 5.1, 5.2, 4.1, 4.2, 4.3_

- [ ] 4. Implement `FileTaxConfigProvider`
  - [ ] 4.1 Create `src/api/Services/FileTaxConfigProvider.cs` implementing `ITaxConfigProvider`:
    - Accept `IWebHostEnvironment` in the constructor and store `env.ContentRootPath`
    - In `GetConfig()`: resolve `Configuration/TaxData/` under content root; throw `InvalidOperationException` (with path in message) if directory missing; throw `InvalidOperationException` (with "no matching" in message) if no `tax_config_*.json` files found; select the lexicographically largest filename (descending sort, first element); parse JSON using `JsonDocument` into a new `TaxConfig` record; normalize all state code keys to uppercase
    - Let `JsonException` / `InvalidOperationException` from malformed JSON propagate without catching
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_
  - [ ]* 4.2 Write unit tests for `FileTaxConfigProvider`
    - Verify `InvalidOperationException` thrown when tax data directory is missing (use a temp path that doesn't exist)
    - Verify `InvalidOperationException` thrown when directory exists but contains no `tax_config_*.json` files
    - Verify `JsonException` propagates for malformed JSON
    - Verify correct file selected when multiple `tax_config_*.json` files are present
    - _Requirements: 3.4, 3.5, 3.7_

- [ ] 5. Checkpoint — verify core logic compiles and unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Create the `GradCast.Api.Tests` project and add property-based tests
  - [ ] 6.1 Create `src/api.tests/GradCast.Api.Tests.csproj` targeting `net10.0`:
    - Add `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` package references (pinned versions)
    - Add `FsCheck` and `FsCheck.Xunit` package references (pinned versions)
    - Add a `ProjectReference` to `../api/GradCast.Api.csproj`
    - Add the new test project to the solution (`GradCast.slnx`)
    - _Requirements: 5.1, 5.2, 5.3_
  - [ ] 6.2 Implement custom FsCheck generators in `src/api.tests/Generators/TaxGenerators.cs`:
    - Non-negative decimal salaries in [0, 10_000_000]
    - Random valid `TaxConfig` instances with arbitrary bracket lists and state rate maps
    - Known and unknown 2-letter state codes
    - Arbitrary-case variants of a known state code (for Property 4)
    - Random sets of `tax_config_*.json`-format filename strings (for Property 6)
    - Well-formed tax config JSON strings with arbitrary state code casing (for Property 7)
    - _Requirements: 5.3_
  - [ ]* 6.3 Write property test for Property 1: Empty bracket list produces zero federal tax
    - **Property 1: Empty bracket list produces zero federal tax**
    - For any non-negative salary, when `TaxConfig.FederalBrackets` is empty, `FederalTaxAnnual` must be `0` and no exception thrown
    - **Validates: Requirements 2.5**
  - [ ]* 6.4 Write property test for Property 2: All monetary output fields are rounded to 2 decimal places
    - **Property 2: All monetary output fields are rounded to 2 decimal places**
    - For any non-negative salary, any state, and any valid `TaxConfig`, every monetary field in `NetPayResult` satisfies `Math.Round(field, 2) == field`
    - **Validates: Requirements 4.5**
  - [ ]* 6.5 Write property test for Property 3: Unknown state code applies 4% fallback rate
    - **Property 3: Unknown state code applies 4% fallback rate**
    - For any non-negative salary and any state code absent from `TaxConfig.StateTaxRates`, `StateTaxAnnual == Math.Round(grossAnnualSalary * 0.04m, 2)`
    - **Validates: Requirements 4.4, 5.4**
  - [ ]* 6.6 Write property test for Property 4: State code lookup is case-insensitive
    - **Property 4: State code lookup is case-insensitive**
    - For any non-negative salary and known state code `S`, `Calculate(salary, S.ToLower())` and `Calculate(salary, S.ToUpper())` produce identical `NetPayResult` values across all fields
    - **Validates: Requirements 4.6**
  - [ ]* 6.7 Write property test for Property 5: Calculation correctness — output matches formula applied to inputs
    - **Property 5: Calculation correctness — output matches formula applied to inputs**
    - For any non-negative salary and state code, each `NetPayResult` field equals the value produced by manually applying the documented formulas with 2-decimal rounding
    - **Validates: Requirements 4.1, 4.2, 4.3, 5.2**
  - [ ]* 6.8 Write property test for Property 6: FileTaxConfigProvider always selects the lexicographically largest filename
    - **Property 6: FileTaxConfigProvider always selects the lexicographically largest filename**
    - For any non-empty set of `tax_config_*.json` filename strings, sorting descending and taking the first element always yields the filename with the highest version string
    - **Validates: Requirements 3.3**
  - [ ]* 6.9 Write property test for Property 7: State codes are normalized to uppercase during JSON parsing
    - **Property 7: State codes are normalized to uppercase during JSON parsing**
    - For any valid tax config JSON where `stateTaxRates` keys use arbitrary casing, `FileTaxConfigProvider.GetConfig()` returns a `TaxConfig` where all state keys are uppercase and lookups succeed for both cases
    - **Validates: Requirements 3.6**

- [ ] 7. Update `FinanceEndpoints` and `Program.cs` to use the new interfaces
  - [ ] 7.1 Modify `src/api/Endpoints/FinanceEndpoints.cs`:
    - Change the `CalculateNetPay` handler's tax service parameter type from `TaxCalculationService` to `ITaxCalculationService`
    - No other logic changes
    - _Requirements: 1.4_
  - [ ] 7.2 Modify `src/api/Program.cs`:
    - Replace `builder.Services.AddSingleton<TaxCalculationService>()` with two singleton registrations: `AddSingleton<ITaxConfigProvider, FileTaxConfigProvider>()` and `AddSingleton<ITaxCalculationService, TaxCalculationService>()`
    - _Requirements: 1.5, 3.8_
  - [ ]* 7.3 Write integration/smoke tests using `WebApplicationFactory<Program>`:
    - Verify DI resolves `ITaxCalculationService` → `TaxCalculationService`
    - Verify DI resolves `ITaxConfigProvider` → `FileTaxConfigProvider`
    - Smoke-test `GET /api/finance/net-pay?grossSalary=75000&state=CA` returns HTTP 200 with a valid `NetPayResult`
    - _Requirements: 1.5, 3.8_

- [ ] 8. Final checkpoint — ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Sub-tasks without `*` are required and must be implemented
- Property tests use FsCheck with a minimum of 100 iterations per property
- `TaxConfig` and `TaxBracket` move to a dedicated file; the private nested types in `TaxCalculationService` are removed in task 3.1
- No formula or endpoint contract changes — `NetPayResult` shape and `/api/finance/net-pay` behavior are preserved
- The test project (`src/api.tests/`) needs to be added to `GradCast.slnx` in task 6.1

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "4.1"] },
    { "id": 3, "tasks": ["3.2", "4.2", "6.1"] },
    { "id": 4, "tasks": ["6.2", "7.1", "7.2"] },
    { "id": 5, "tasks": ["6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9"] },
    { "id": 6, "tasks": ["7.3"] }
  ]
}
```
