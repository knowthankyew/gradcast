using Dapper;
using GradCast.Api.Models;
using GradCast.Data;
using GradCast.Data.Entities;

namespace GradCast.Api.Data;

public class ScenarioRepository : IScenarioRepository
{
    private readonly ISqliteConnectionFactory _dbFactory;

    public ScenarioRepository(ISqliteConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<string> InsertScenarioAsync(SharedScenario scenario, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO shared_scenarios (
                id, created_at, school_id, cip_code, credential_level, cbsa_code,
                housing_type, salary_mode, reported_annual_salary, monthly_net_take_home,
                monthly_rent, monthly_loan_payment, monthly_disposable, disposable_ratio,
                sentiment_tag, scenario_title, client_hash
            ) VALUES (
                @Id, @CreatedAt, @SchoolId, @CipCode, @CredentialLevel, @CbsaCode,
                @HousingType, @SalaryMode, @ReportedAnnualSalary, @MonthlyNetTakeHome,
                @MonthlyRent, @MonthlyLoanPayment, @MonthlyDisposable, @DisposableRatio,
                @SentimentTag, @ScenarioTitle, @ClientHash
            );
        """;

        await conn.ExecuteAsync(new CommandDefinition(sql, scenario, cancellationToken: ct));
        return scenario.Id;
    }

    public async Task<SharedScenarioDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                s.id AS Id,
                s.created_at AS CreatedAt,
                s.school_id AS SchoolId,
                sch.name AS SchoolName,
                s.cip_code AS CipCode,
                s.credential_level AS CredentialLevel,
                s.cbsa_code AS CbsaCode,
                loc.name AS LocationName,
                s.housing_type AS HousingType,
                s.salary_mode AS SalaryMode,
                s.reported_annual_salary AS ReportedAnnualSalary,
                s.monthly_net_take_home AS MonthlyNetTakeHome,
                s.monthly_rent AS MonthlyRent,
                s.monthly_loan_payment AS MonthlyLoanPayment,
                s.monthly_disposable AS MonthlyDisposable,
                s.disposable_ratio AS DisposableRatio,
                s.sentiment_tag AS SentimentTag,
                s.scenario_title AS ScenarioTitle
            FROM shared_scenarios s
            LEFT JOIN schools sch ON sch.id = s.school_id
            LEFT JOIN cbsa_locations loc ON loc.cbsa_code = s.cbsa_code
            WHERE s.id = @Id
            LIMIT 1;
        """;

        return await conn.QuerySingleOrDefaultAsync<SharedScenarioDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SharedScenarioDto>> GetPositiveFindsAsync(
        string? cbsaCode = null,
        string? cipFamily = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);

        const string sql = """
            SELECT 
                s.id AS Id,
                s.created_at AS CreatedAt,
                s.school_id AS SchoolId,
                sch.name AS SchoolName,
                s.cip_code AS CipCode,
                s.credential_level AS CredentialLevel,
                s.cbsa_code AS CbsaCode,
                loc.name AS LocationName,
                s.housing_type AS HousingType,
                s.salary_mode AS SalaryMode,
                s.reported_annual_salary AS ReportedAnnualSalary,
                s.monthly_net_take_home AS MonthlyNetTakeHome,
                s.monthly_rent AS MonthlyRent,
                s.monthly_loan_payment AS MonthlyLoanPayment,
                s.monthly_disposable AS MonthlyDisposable,
                s.disposable_ratio AS DisposableRatio,
                s.sentiment_tag AS SentimentTag,
                s.scenario_title AS ScenarioTitle
            FROM shared_scenarios s
            LEFT JOIN schools sch ON sch.id = s.school_id
            LEFT JOIN cbsa_locations loc ON loc.cbsa_code = s.cbsa_code
            WHERE s.monthly_disposable > 0
              AND (@CbsaCode IS NULL OR s.cbsa_code = @CbsaCode)
              AND (@CipFamily IS NULL OR substr(s.cip_code, 1, 2) = @CipFamily)
            ORDER BY s.monthly_disposable DESC, s.created_at DESC
            LIMIT @Limit;
        """;

        var items = await conn.QueryAsync<SharedScenarioDto>(
            new CommandDefinition(sql, new { CbsaCode = cbsaCode, CipFamily = cipFamily, Limit = boundedLimit }, cancellationToken: ct));

        return items.ToList();
    }

    public async Task<int> GetDailySubmissionCountByClientHashAsync(string clientHash, string datePrefixUtc, CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT COUNT(*) 
            FROM shared_scenarios
            WHERE client_hash = @ClientHash
              AND created_at >= @DatePrefixUtc
              AND created_at < date(@DatePrefixUtc, '+1 day');
        """;

        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ClientHash = clientHash, DatePrefixUtc = datePrefixUtc }, cancellationToken: ct));
    }

    public async Task<bool> TryInsertScenarioWithDailyLimitAsync(
        SharedScenario scenario,
        int maxDailyLimit,
        string datePrefixUtc,
        CancellationToken ct = default)
    {
        await using var conn = await _dbFactory.CreateOpenConnectionAsync(ct);

        const string sql = """
            INSERT INTO shared_scenarios (
                id, created_at, school_id, cip_code, credential_level, cbsa_code,
                housing_type, salary_mode, reported_annual_salary, monthly_net_take_home,
                monthly_rent, monthly_loan_payment, monthly_disposable, disposable_ratio,
                sentiment_tag, scenario_title, client_hash
            )
            SELECT 
                @Id, @CreatedAt, @SchoolId, @CipCode, @CredentialLevel, @CbsaCode,
                @HousingType, @SalaryMode, @ReportedAnnualSalary, @MonthlyNetTakeHome,
                @MonthlyRent, @MonthlyLoanPayment, @MonthlyDisposable, @DisposableRatio,
                @SentimentTag, @ScenarioTitle, @ClientHash
            WHERE (
                SELECT COUNT(*) 
                FROM shared_scenarios
                WHERE client_hash = @ClientHash
                  AND created_at >= @DatePrefixUtc
                  AND created_at < date(@DatePrefixUtc, '+1 day')
            ) < @MaxDailyLimit;
        """;

        var parameters = new DynamicParameters(scenario);
        parameters.Add("MaxDailyLimit", maxDailyLimit);
        parameters.Add("DatePrefixUtc", datePrefixUtc);

        var rowsAffected = await conn.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return rowsAffected > 0;
    }
}
