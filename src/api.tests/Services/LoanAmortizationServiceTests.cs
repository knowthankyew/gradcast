using GradCast.Api.Services;

namespace GradCast.Api.Tests;

public class LoanAmortizationServiceTests
{
    [Fact]
    public void LoanAmortizationServiceCalculatesMonthlyPaymentAcrossDefaultAndCustomTerms()
    {
        var loan = new LoanAmortizationService();

        var result = loan.Calculate(12000m, 0.12m, 1);

        Assert.Equal(12_000m, result.Principal);
        Assert.Equal(1066.19m, result.MonthlyPayment);
        Assert.Equal(12794.23m, result.TotalPaid);
        Assert.Equal(794.23m, result.TotalInterest);
        Assert.Equal(0.12m, result.AnnualRate);
        Assert.Equal(1, result.TermYears);
    }

    [Fact]
    public void LoanAmortizationServiceCalculatesZeroInterestCorrectly()
    {
        var loan = new LoanAmortizationService();

        var result = loan.Calculate(12000m, 0m, 1);

        Assert.Equal(12_000m, result.Principal);
        Assert.Equal(1000m, result.MonthlyPayment);
        Assert.Equal(12000m, result.TotalPaid);
        Assert.Equal(0m, result.TotalInterest);
        Assert.Equal(0m, result.AnnualRate);
        Assert.Equal(1, result.TermYears);
    }
}
