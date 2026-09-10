using TravelReimbursement.Api.Services;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Tests;

public sealed class PayrollExportServiceTests
{
    [Fact]
    public void Export_rows_include_all_components_and_total()
    {
        var rows = PayrollExportService.CreateRows([
            new PayrollExportRow(Guid.NewGuid(), "李四", 10000m, 1000m, 500m, 300m, 200m, 800m, 600m, 250m, 50m, 12000m, 1700m, 10300m, PayoutStatus.Paid, "1234", DateTimeOffset.Parse("2026-09-10T08:00:00Z"), "调整说明"),
            new PayrollExportRow(Guid.NewGuid(), "王五", 8000m, 0m, 0m, 200m, 0m, 600m, 400m, 100m, 0m, 8200m, 1100m, 7100m, PayoutStatus.Pending, "5678", null, null)
        ]);

        Assert.Equal(4, rows.Count);
        Assert.Equal("员工姓名", rows[0][0]);
        Assert.Equal("李四", rows[1][0]);
        Assert.Equal("王五", rows[2][0]);
        Assert.Equal("合计", rows[3][0]);
        Assert.Equal(20200m, rows[3][6]);
        Assert.Equal(17400m, rows[3][12]);
        Assert.Equal("已发放", rows[1][13]);
        Assert.Equal("待发放", rows[2][13]);
    }
}
