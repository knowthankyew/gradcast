using GradCast.Api.Models;

namespace GradCast.Api.Services;

public interface ITaxCalculationService
{
    int TaxYear { get; }
    NetPayResult Calculate(decimal grossAnnualSalary, string state);
}
