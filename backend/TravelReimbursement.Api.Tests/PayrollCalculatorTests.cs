using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class PayrollCalculatorTests
{
    [Fact]
    public void Calculates_gross_deductions_and_net_pay()
    {
        var result = PayrollCalculator.Calculate(Request(baseSalary: 10000m, performanceSalary: 1500m, bonus: 500m,
            allowance: 300m, otherIncrease: 200m, socialSecurity: 800m, housingFund: 600m, incomeTax: 250m, otherDeduction: 50m, note: "月度调整"));

        Assert.Equal(12500m, result.GrossPay);
        Assert.Equal(1700m, result.TotalDeductions);
        Assert.Equal(10800m, result.NetPay);
    }

    [Fact]
    public void Rejects_more_than_two_decimal_places()
    {
        var problem = Assert.Throws<ApiProblemException>(() => PayrollCalculator.Calculate(Request(baseSalary: 100.001m)));

        Assert.Equal("PAYROLL_AMOUNT_INVALID", problem.Code);
        Assert.Contains("baseSalary", problem.Errors!);
    }

    [Fact]
    public void Rejects_deductions_greater_than_gross_pay()
    {
        var problem = Assert.Throws<ApiProblemException>(() => PayrollCalculator.Calculate(Request(baseSalary: 100m, incomeTax: 101m)));

        Assert.Equal("PAYROLL_NET_NEGATIVE", problem.Code);
    }

    [Fact]
    public void Requires_note_for_other_adjustments()
    {
        var problem = Assert.Throws<ApiProblemException>(() => PayrollCalculator.Calculate(Request(baseSalary: 100m, otherIncrease: 1m)));

        Assert.Equal("PAYROLL_ADJUSTMENT_NOTE_REQUIRED", problem.Code);
    }

    [Fact]
    public void Recalculates_persisted_entry_without_trusting_stored_totals()
    {
        var result = PayrollCalculator.Calculate(new PayrollEntry
        {
            BaseSalary = 10000m,
            PerformanceSalary = 1000m,
            SocialSecurityDeduction = 800m,
            GrossPay = 1m,
            TotalDeductions = 1m,
            NetPay = 0m
        });

        Assert.Equal(11000m, result.GrossPay);
        Assert.Equal(800m, result.TotalDeductions);
        Assert.Equal(10200m, result.NetPay);
    }

    private static SavePayrollEntryRequest Request(
        decimal baseSalary = 0,
        decimal performanceSalary = 0,
        decimal bonus = 0,
        decimal allowance = 0,
        decimal otherIncrease = 0,
        decimal socialSecurity = 0,
        decimal housingFund = 0,
        decimal incomeTax = 0,
        decimal otherDeduction = 0,
        string? note = null) => new(
            Guid.NewGuid(), Guid.NewGuid(), baseSalary, performanceSalary, bonus, allowance, otherIncrease,
            socialSecurity, housingFund, incomeTax, otherDeduction, note);
}
