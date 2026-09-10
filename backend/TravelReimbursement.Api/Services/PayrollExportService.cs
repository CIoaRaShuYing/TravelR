using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed record PayrollExportResult(byte[] Content, string FileName, DateOnly PayrollMonth, int EmployeeCount);

public sealed class PayrollExportService(AppDbContext db)
{
    public async Task<PayrollExportResult> CreateAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .Where(value => value.Id == periodId)
            .Select(value => new
            {
                value.PayrollMonth,
                value.Status,
                Entries = value.Entries.Select(entry => new PayrollExportRow(
                    entry.Id,
                    entry.EmployeePersonalNameSnapshot ?? entry.EmployeeDisplayNameSnapshot,
                    entry.BaseSalary,
                    entry.PerformanceSalary,
                    entry.Bonus,
                    entry.Allowance,
                    entry.OtherIncrease,
                    entry.SocialSecurityDeduction,
                    entry.HousingFundDeduction,
                    entry.IndividualIncomeTax,
                    entry.OtherDeduction,
                    entry.GrossPay,
                    entry.TotalDeductions,
                    entry.NetPay,
                    entry.PayoutStatus,
                    entry.PayoutRecord != null ? entry.PayoutRecord.BankCardLastFour : entry.BankCardLastFourSnapshot,
                    entry.PayoutRecord != null ? entry.PayoutRecord.ConfirmedAt : null,
                    entry.Note)).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiProblemException(StatusCodes.Status404NotFound, "PAYROLL_PERIOD_NOT_FOUND", "工资表不存在。");

        var rows = CreateRows(period.Entries);
        var workbook = XlsxWorkbookWriter.Write([new XlsxSheet("工资表", rows)]);
        var month = period.PayrollMonth.ToString("yyyyMM", CultureInfo.InvariantCulture);
        return new PayrollExportResult(workbook, $"工资表_{month}.xlsx", period.PayrollMonth, period.Entries.Count);
    }

    internal static IReadOnlyList<object?[]> CreateRows(IEnumerable<PayrollExportRow> entries)
    {
        var ordered = entries.OrderBy(entry => entry.EmployeeName, StringComparer.Ordinal).ThenBy(entry => entry.Id).ToList();
        var rows = new List<object?[]>
        {
            new object?[] { "员工姓名", "基本工资", "绩效工资", "奖金", "津贴", "其他增加", "应发工资", "社保个人扣款", "公积金个人扣款", "个人所得税", "其他扣款", "扣款合计", "实发工资", "发放状态", "银行卡尾号", "发放时间", "备注" }
        };
        rows.AddRange(ordered.Select(entry => new object?[]
        {
            entry.EmployeeName,
            entry.BaseSalary,
            entry.PerformanceSalary,
            entry.Bonus,
            entry.Allowance,
            entry.OtherIncrease,
            entry.GrossPay,
            entry.SocialSecurityDeduction,
            entry.HousingFundDeduction,
            entry.IndividualIncomeTax,
            entry.OtherDeduction,
            entry.TotalDeductions,
            entry.NetPay,
            PayoutStatusLabel(entry.PayoutStatus),
            entry.BankCardLastFour,
            entry.ConfirmedAt?.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            entry.Note
        }));
        rows.Add(new object?[]
        {
            "合计",
            ordered.Sum(x => x.BaseSalary),
            ordered.Sum(x => x.PerformanceSalary),
            ordered.Sum(x => x.Bonus),
            ordered.Sum(x => x.Allowance),
            ordered.Sum(x => x.OtherIncrease),
            ordered.Sum(x => x.GrossPay),
            ordered.Sum(x => x.SocialSecurityDeduction),
            ordered.Sum(x => x.HousingFundDeduction),
            ordered.Sum(x => x.IndividualIncomeTax),
            ordered.Sum(x => x.OtherDeduction),
            ordered.Sum(x => x.TotalDeductions),
            ordered.Sum(x => x.NetPay),
            null, null, null, null
        });
        return rows;
    }

    private static string PayoutStatusLabel(PayoutStatus status) => status switch
    {
        PayoutStatus.NotApplicable => "未锁定",
        PayoutStatus.Pending => "待发放",
        PayoutStatus.Paid => "已发放",
        _ => status.ToString()
    };
}

internal sealed record PayrollExportRow(
    Guid Id,
    string EmployeeName,
    decimal BaseSalary,
    decimal PerformanceSalary,
    decimal Bonus,
    decimal Allowance,
    decimal OtherIncrease,
    decimal SocialSecurityDeduction,
    decimal HousingFundDeduction,
    decimal IndividualIncomeTax,
    decimal OtherDeduction,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    PayoutStatus PayoutStatus,
    string? BankCardLastFour,
    DateTimeOffset? ConfirmedAt,
    string? Note);
