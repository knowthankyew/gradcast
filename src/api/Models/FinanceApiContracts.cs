using Microsoft.AspNetCore.Mvc;

namespace GradCast.Api.Models;

public record NetPayRequest(
    [property: FromQuery] decimal GrossSalary,
    [property: FromQuery] string State);

public record LoanPaymentRequest(
    [property: FromQuery] decimal Principal,
    [property: FromQuery] decimal? Rate,
    [property: FromQuery] int? TermYears);
