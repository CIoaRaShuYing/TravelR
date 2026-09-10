using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed record PayrollCalculation(decimal GrossPay, decimal TotalDeductions, decimal NetPay);

public static class PayrollCalculator
{
    public static PayrollCalculation Calculate(SavePayrollEntryRequest request) => Calculate(
        request.BaseSalary,
        request.PerformanceSalary,
        request.Bonus,
        request.Allowance,
        request.OtherIncrease,
        request.SocialSecurityDeduction,
        request.HousingFundDeduction,
        request.IndividualIncomeTax,
        request.OtherDeduction,
        request.Note);

    public static PayrollCalculation Calculate(PayrollEntry entry) => Calculate(
        entry.BaseSalary,
        entry.PerformanceSalary,
        entry.Bonus,
        entry.Allowance,
        entry.OtherIncrease,
        entry.SocialSecurityDeduction,
        entry.HousingFundDeduction,
        entry.IndividualIncomeTax,
        entry.OtherDeduction,
        entry.Note);

    private static PayrollCalculation Calculate(
        decimal baseSalary,
        decimal performanceSalary,
        decimal bonus,
        decimal allowance,
        decimal otherIncrease,
        decimal socialSecurityDeduction,
        decimal housingFundDeduction,
        decimal individualIncomeTax,
        decimal otherDeduction,
        string? note)
    {
        var amounts = new Dictionary<string, decimal>
        {
            ["baseSalary"] = baseSalary,
            ["performanceSalary"] = performanceSalary,
            ["bonus"] = bonus,
            ["allowance"] = allowance,
            ["otherIncrease"] = otherIncrease,
            ["socialSecurityDeduction"] = socialSecurityDeduction,
            ["housingFundDeduction"] = housingFundDeduction,
            ["individualIncomeTax"] = individualIncomeTax,
            ["otherDeduction"] = otherDeduction
        };
        var errors = amounts
            .Where(item => item.Value < 0 || item.Value > 999999999m || decimal.Round(item.Value, 2, MidpointRounding.AwayFromZero) != item.Value)
            .ToDictionary(item => item.Key, _ => new[] { "金额必须在 0 至 999999999 之间并最多保留两位小数。" });
        if (errors.Count > 0)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "PAYROLL_AMOUNT_INVALID", "工资金额不符合要求。", errors);

        var grossPay = baseSalary + performanceSalary + bonus + allowance + otherIncrease;
        var deductions = socialSecurityDeduction + housingFundDeduction + individualIncomeTax + otherDeduction;
        if (deductions > grossPay)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "PAYROLL_NET_NEGATIVE", "工资扣款不能大于应发工资。", new Dictionary<string, string[]> { ["netPay"] = ["实发工资不能小于零。"] });
        if ((otherIncrease > 0 || otherDeduction > 0) && string.IsNullOrWhiteSpace(note))
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "PAYROLL_ADJUSTMENT_NOTE_REQUIRED", "存在其他增加或其他扣款时必须填写备注。", new Dictionary<string, string[]> { ["note"] = ["请说明调整原因。"] });
        return new PayrollCalculation(grossPay, deductions, grossPay - deductions);
    }
}

public sealed record PayrollPeriodSummary(
    Guid Id,
    DateOnly PayrollMonth,
    PayrollPeriodStatus Status,
    int EmployeeCount,
    int PaidCount,
    int PendingCount,
    decimal GrossTotal,
    decimal DeductionTotal,
    decimal NetTotal,
    decimal PaidNetTotal,
    decimal PendingNetTotal,
    Guid ConcurrencyToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LockedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt);

public sealed record PayrollPayoutReceiptRow(
    decimal Amount,
    string RecipientName,
    string BankCardLastFour,
    Guid ConfirmedById,
    string? ConfirmedByDisplayName,
    DateTimeOffset ConfirmedAt,
    string? Note);

public sealed record PayrollEntryRow(
    Guid Id,
    Guid UserId,
    string EmployeeDisplayNameSnapshot,
    string? EmployeePersonalNameSnapshot,
    string? BankCardLastFourSnapshot,
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
    string? Note,
    PayoutStatus PayoutStatus,
    DateTimeOffset? PaidAt,
    Guid ConcurrencyToken,
    PayrollPayoutReceiptRow? PayoutRecord);

public sealed record PayrollPeriodDetail(PayrollPeriodSummary Period, IReadOnlyList<PayrollEntryRow> Entries);

public sealed record PayrollCandidate(
    Guid Id,
    string DisplayName,
    string? PersonalName,
    string PhoneNumber,
    bool IsActive,
    bool PersonalNameReady,
    bool BankCardReady);

public sealed record MyPayrollRow(
    Guid Id,
    DateOnly PayrollMonth,
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
    string? Note,
    string BankCardLastFour,
    DateTimeOffset PaidAt);

public sealed class PayrollService(AppDbContext db, IBankCardProtector bankCardProtector)
{
    public async Task<IReadOnlyList<PayrollPeriodSummary>> ListPeriodsAsync(CancellationToken cancellationToken)
    {
        var periods = await db.PayrollPeriods.AsNoTracking().Include(x => x.Entries)
            .OrderByDescending(x => x.PayrollMonth).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return periods.Select(ToSummary).ToList();
    }

    public async Task<PayrollPeriodDetail> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .Include(x => x.Entries).ThenInclude(x => x.PayoutRecord)
            .SingleOrDefaultAsync(x => x.Id == periodId, cancellationToken)
            ?? throw NotFound();
        return await ToDetailAsync(period, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> CreatePeriodAsync(Guid administratorId, CreatePayrollPeriodRequest request, string? traceId, CancellationToken cancellationToken)
    {
        if (request.PayrollMonth.Day != 1)
            throw Validation("payrollMonth", "工资月份必须使用当月第一天。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.PayrollPeriods.AnyAsync(x => x.PayrollMonth == request.PayrollMonth && x.Status != PayrollPeriodStatus.Cancelled, cancellationToken))
            throw Conflict("PAYROLL_PERIOD_EXISTS", "该月份已经存在有效工资表。");

        var applicantRoleIds = db.Roles.Where(role => role.Name == "Applicant").Select(role => role.Id);
        var users = await db.Users.Where(user => user.IsActive && user.PhoneNumber != null
                && db.UserRoles.Any(userRole => userRole.UserId == user.Id && applicantRoleIds.Contains(userRole.RoleId)))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.PhoneNumber)
            .ToListAsync(cancellationToken);
        var period = new PayrollPeriod
        {
            PayrollMonth = request.PayrollMonth,
            CreatedById = administratorId
        };
        period.Entries.AddRange(users.Select(user => NewEntry(period.Id, user, administratorId)));
        db.PayrollPeriods.Add(period);
        AddAudit(administratorId, "PayrollPeriodCreated", "PayrollPeriod", period.Id, traceId, new { period.PayrollMonth, employeeCount = period.Entries.Count });
        await SaveChangesAsync("PAYROLL_PERIOD_EXISTS", "该月份已经存在有效工资表。", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollCandidate>> ListCandidatesAsync(Guid periodId, string? keyword, CancellationToken cancellationToken)
    {
        if (!await db.PayrollPeriods.AsNoTracking().AnyAsync(x => x.Id == periodId, cancellationToken)) throw NotFound();
        var existingIds = db.PayrollEntries.Where(x => x.PayrollPeriodId == periodId).Select(x => x.UserId);
        var formalRoleIds = db.Roles.Where(role => role.Name == "Applicant" || role.Name == "Administrator").Select(role => role.Id);
        var query = db.Users.AsNoTracking().Where(user => user.PhoneNumber != null && !existingIds.Contains(user.Id)
            && db.UserRoles.Any(userRole => userRole.UserId == user.Id && formalRoleIds.Contains(userRole.RoleId)));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(user => user.DisplayName.Contains(term) || (user.PersonalName != null && user.PersonalName.Contains(term)) || user.PhoneNumber!.Contains(term));
        }
        return await query.OrderByDescending(user => user.IsActive).ThenBy(user => user.DisplayName)
            .Select(user => new PayrollCandidate(user.Id, user.DisplayName, user.PersonalName, user.PhoneNumber!, user.IsActive,
                user.PersonalName != null && user.PersonalName != string.Empty,
                user.BankCardProtected != null && user.BankCardProtected != string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollPeriodDetail> AddEntriesAsync(Guid administratorId, Guid periodId, AddPayrollEntriesRequest request, string? traceId, CancellationToken cancellationToken)
    {
        var userIds = request.UserIds.Distinct().ToList();
        if (userIds.Count == 0 || userIds.Count != request.UserIds.Count)
            throw Validation("userIds", "请选择至少一名且不能重复的正式用户。");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.PeriodConcurrencyToken);
        EnsureDraft(period);
        if (period.Entries.Any(entry => userIds.Contains(entry.UserId)))
            throw Conflict("PAYROLL_EMPLOYEE_EXISTS", "所选人员已经在该月工资表中。");
        var formalRoleIds = db.Roles.Where(role => role.Name == "Applicant" || role.Name == "Administrator").Select(role => role.Id);
        var users = await db.Users.Where(user => userIds.Contains(user.Id) && user.PhoneNumber != null
                && db.UserRoles.Any(userRole => userRole.UserId == user.Id && formalRoleIds.Contains(userRole.RoleId)))
            .ToListAsync(cancellationToken);
        if (users.Count != userIds.Count) throw Validation("userIds", "所选人员中包含非正式用户。");
        db.PayrollEntries.AddRange(users.Select(user => NewEntry(period.Id, user, administratorId)));
        Touch(period);
        AddAudit(administratorId, "PayrollEntriesAdded", "PayrollPeriod", period.Id, traceId, new { userIds });
        await SaveChangesAsync("PAYROLL_EMPLOYEE_EXISTS", "所选人员已经在该月工资表中。", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> RemoveEntryAsync(Guid administratorId, Guid periodId, Guid entryId, RemovePayrollEntryRequest request, string? traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.PeriodConcurrencyToken);
        EnsureDraft(period);
        var entry = period.Entries.SingleOrDefault(x => x.Id == entryId) ?? throw EntryNotFound();
        EnsureEntryExpected(entry, request.EntryConcurrencyToken);
        db.PayrollEntries.Remove(entry);
        Touch(period);
        AddAudit(administratorId, "PayrollEntryRemoved", "PayrollPeriod", period.Id, traceId, new { entry.Id, entry.UserId, entry.EmployeeDisplayNameSnapshot });
        await SaveChangesAsync(null, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> SaveEntriesAsync(Guid administratorId, Guid periodId, SavePayrollEntriesRequest request, string? traceId, CancellationToken cancellationToken)
    {
        if (request.Entries.Count == 0 || request.Entries.Select(x => x.Id).Distinct().Count() != request.Entries.Count)
            throw Validation("entries", "请提交至少一条且不能重复的工资明细。");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.PeriodConcurrencyToken);
        EnsureDraft(period);
        var changes = new List<object>();
        foreach (var input in request.Entries)
        {
            var entry = period.Entries.SingleOrDefault(x => x.Id == input.Id) ?? throw EntryNotFound();
            EnsureEntryExpected(entry, input.EntryConcurrencyToken);
            var calculation = PayrollCalculator.Calculate(input);
            var before = AmountSnapshot(entry);
            Apply(entry, input, calculation, administratorId);
            changes.Add(new { entry.Id, before, after = AmountSnapshot(entry) });
        }
        Touch(period);
        AddAudit(administratorId, "PayrollEntriesUpdated", "PayrollPeriod", period.Id, traceId, new { changes });
        await SaveChangesAsync(null, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> LockAsync(Guid administratorId, Guid periodId, PayrollPeriodActionRequest request, string? traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.ConcurrencyToken);
        EnsureDraft(period);
        if (period.Entries.Count == 0) throw Conflict("PAYROLL_PERIOD_EMPTY", "工资表没有人员，不能锁定。");
        var errors = new Dictionary<string, string[]>();
        foreach (var entry in period.Entries)
        {
            try
            {
                var calculation = PayrollCalculator.Calculate(entry);
                var recipient = GetRecipient(entry.User);
                entry.GrossPay = calculation.GrossPay;
                entry.TotalDeductions = calculation.TotalDeductions;
                entry.NetPay = calculation.NetPay;
                entry.EmployeeDisplayNameSnapshot = entry.User.DisplayName;
                entry.EmployeePersonalNameSnapshot = recipient.PersonalName;
                entry.BankCardLastFourSnapshot = recipient.BankCardLastFour;
            }
            catch (ApiProblemException problem)
            {
                errors[$"entries.{entry.Id}"] = [$"{entry.EmployeeDisplayNameSnapshot}：{problem.Message}"];
            }
        }
        if (errors.Count > 0)
            throw new ApiProblemException(StatusCodes.Status409Conflict, "PAYROLL_LOCK_BLOCKED", "部分人员收款资料不完整，工资表未锁定。", errors);
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in period.Entries)
        {
            entry.PayoutStatus = PayoutStatus.Pending;
            entry.UpdatedAt = now;
            entry.UpdatedById = administratorId;
            entry.ConcurrencyToken = Guid.NewGuid();
        }
        period.Status = PayrollPeriodStatus.ReadyForPayout;
        period.LockedAt = now;
        Touch(period, now);
        AddAudit(administratorId, "PayrollPeriodLocked", "PayrollPeriod", period.Id, traceId, new { period.PayrollMonth, employeeCount = period.Entries.Count, netTotal = period.Entries.Sum(x => x.NetPay) });
        await SaveChangesAsync(null, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> ReturnToDraftAsync(Guid administratorId, Guid periodId, PayrollPeriodActionRequest request, string? traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.ConcurrencyToken);
        if (period.Status != PayrollPeriodStatus.ReadyForPayout)
            throw Conflict("PAYROLL_STATUS_CONFLICT", "只有待发放工资表可以退回草稿。");
        if (period.Entries.Any(x => x.PayoutStatus == PayoutStatus.Paid))
            throw Conflict("PAYROLL_ALREADY_PARTIALLY_PAID", "已有工资确认发放，不能退回草稿。");
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in period.Entries)
        {
            entry.PayoutStatus = PayoutStatus.NotApplicable;
            entry.EmployeePersonalNameSnapshot = null;
            entry.BankCardLastFourSnapshot = null;
            entry.UpdatedAt = now;
            entry.UpdatedById = administratorId;
            entry.ConcurrencyToken = Guid.NewGuid();
        }
        period.Status = PayrollPeriodStatus.Draft;
        period.LockedAt = null;
        Touch(period, now);
        AddAudit(administratorId, "PayrollPeriodReturnedToDraft", "PayrollPeriod", period.Id, traceId, new { period.PayrollMonth });
        await SaveChangesAsync(null, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> CancelAsync(Guid administratorId, Guid periodId, PayrollPeriodActionRequest request, string? traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.ConcurrencyToken);
        EnsureDraft(period);
        var now = DateTimeOffset.UtcNow;
        period.Status = PayrollPeriodStatus.Cancelled;
        period.CancelledAt = now;
        Touch(period, now);
        AddAudit(administratorId, "PayrollPeriodCancelled", "PayrollPeriod", period.Id, traceId, new { period.PayrollMonth, employeeCount = period.Entries.Count });
        await SaveChangesAsync(null, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<PayrollPeriodDetail> ConfirmPayoutAsync(Guid administratorId, Guid periodId, Guid entryId, ConfirmPayrollPayoutRequest request, string? traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var period = await LoadPeriodAsync(periodId, cancellationToken);
        EnsurePeriodExpected(period, request.PeriodConcurrencyToken);
        if (period.Status != PayrollPeriodStatus.ReadyForPayout)
            throw Conflict("PAYROLL_STATUS_CONFLICT", "只有待发放工资表可以确认发放。");
        var entry = period.Entries.SingleOrDefault(x => x.Id == entryId) ?? throw EntryNotFound();
        EnsureEntryExpected(entry, request.EntryConcurrencyToken);
        if (entry.PayoutStatus != PayoutStatus.Pending || entry.PayoutRecord is not null)
            throw Conflict("PAYROLL_ALREADY_PAID", "该员工工资已经确认发放或不在待发放状态。");
        var recipient = GetRecipient(entry.User);
        if (!string.Equals(entry.EmployeePersonalNameSnapshot, recipient.PersonalName, StringComparison.Ordinal)
            || !string.Equals(entry.BankCardLastFourSnapshot, recipient.BankCardLastFour, StringComparison.Ordinal))
            throw Conflict("PAYROLL_PAYMENT_PROFILE_CHANGED", "收款资料在工资表锁定后发生变化，请核对后处理。");
        var now = DateTimeOffset.UtcNow;
        entry.PayoutStatus = PayoutStatus.Paid;
        entry.PaidAt = entry.UpdatedAt = now;
        entry.UpdatedById = administratorId;
        entry.ConcurrencyToken = Guid.NewGuid();
        db.PayrollPayoutRecords.Add(new PayrollPayoutRecord
        {
            PayrollEntryId = entry.Id,
            PayrollMonth = period.PayrollMonth,
            Amount = entry.NetPay,
            RecipientName = recipient.PersonalName,
            BankCardLastFour = recipient.BankCardLastFour,
            ConfirmedById = administratorId,
            ConfirmedAt = now,
            Note = TrimOrNull(request.Note)
        });
        if (period.Entries.All(x => x.PayoutStatus == PayoutStatus.Paid))
        {
            period.Status = PayrollPeriodStatus.Completed;
            period.CompletedAt = now;
        }
        Touch(period, now);
        AddAudit(administratorId, "PayrollPayoutConfirmed", "PayrollEntry", entry.Id, traceId, new { period.Id, period.PayrollMonth, entry.UserId, amount = entry.NetPay });
        await SaveChangesAsync("PAYROLL_ALREADY_PAID", "该员工工资已经确认发放。", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MyPayrollRow>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.PayrollEntries.AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.PayoutStatus == PayoutStatus.Paid && entry.PayoutRecord != null)
            .OrderByDescending(entry => entry.PayrollPeriod.PayrollMonth)
            .Select(entry => new MyPayrollRow(
                entry.Id,
                entry.PayrollPeriod.PayrollMonth,
                entry.EmployeePersonalNameSnapshot!,
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
                entry.Note,
                entry.PayoutRecord!.BankCardLastFour,
                entry.PayoutRecord.ConfirmedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<PayrollPeriod> LoadPeriodAsync(Guid periodId, CancellationToken cancellationToken) =>
        await db.PayrollPeriods.Include(x => x.Entries).ThenInclude(x => x.User)
            .Include(x => x.Entries).ThenInclude(x => x.PayoutRecord)
            .SingleOrDefaultAsync(x => x.Id == periodId, cancellationToken) ?? throw NotFound();

    private async Task<PayrollPeriodDetail> ToDetailAsync(PayrollPeriod period, CancellationToken cancellationToken)
    {
        var actorIds = period.Entries.Where(x => x.PayoutRecord != null).Select(x => x.PayoutRecord!.ConfirmedById).Distinct().ToList();
        var actorNames = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var entries = period.Entries.OrderBy(x => x.EmployeePersonalNameSnapshot ?? x.EmployeeDisplayNameSnapshot).ThenBy(x => x.Id)
            .Select(entry => new PayrollEntryRow(
                entry.Id, entry.UserId, entry.EmployeeDisplayNameSnapshot, entry.EmployeePersonalNameSnapshot, entry.BankCardLastFourSnapshot,
                entry.BaseSalary, entry.PerformanceSalary, entry.Bonus, entry.Allowance, entry.OtherIncrease,
                entry.SocialSecurityDeduction, entry.HousingFundDeduction, entry.IndividualIncomeTax, entry.OtherDeduction,
                entry.GrossPay, entry.TotalDeductions, entry.NetPay, entry.Note, entry.PayoutStatus, entry.PaidAt, entry.ConcurrencyToken,
                entry.PayoutRecord is null ? null : new PayrollPayoutReceiptRow(entry.PayoutRecord.Amount, entry.PayoutRecord.RecipientName,
                    entry.PayoutRecord.BankCardLastFour, entry.PayoutRecord.ConfirmedById,
                    actorNames.GetValueOrDefault(entry.PayoutRecord.ConfirmedById), entry.PayoutRecord.ConfirmedAt, entry.PayoutRecord.Note)))
            .ToList();
        return new PayrollPeriodDetail(ToSummary(period), entries);
    }

    private static PayrollPeriodSummary ToSummary(PayrollPeriod period)
    {
        var paid = period.Entries.Where(x => x.PayoutStatus == PayoutStatus.Paid).ToList();
        var pending = period.Entries.Where(x => x.PayoutStatus == PayoutStatus.Pending).ToList();
        return new PayrollPeriodSummary(period.Id, period.PayrollMonth, period.Status, period.Entries.Count, paid.Count, pending.Count,
            period.Entries.Sum(x => x.GrossPay), period.Entries.Sum(x => x.TotalDeductions), period.Entries.Sum(x => x.NetPay),
            paid.Sum(x => x.NetPay), pending.Sum(x => x.NetPay), period.ConcurrencyToken,
            period.CreatedAt, period.UpdatedAt, period.LockedAt, period.CompletedAt, period.CancelledAt);
    }

    private static PayrollEntry NewEntry(Guid periodId, AppUser user, Guid actorId) => new()
    {
        PayrollPeriodId = periodId,
        UserId = user.Id,
        EmployeeDisplayNameSnapshot = user.DisplayName,
        UpdatedById = actorId
    };

    private static void Apply(PayrollEntry entry, SavePayrollEntryRequest input, PayrollCalculation calculation, Guid actorId)
    {
        entry.BaseSalary = input.BaseSalary;
        entry.PerformanceSalary = input.PerformanceSalary;
        entry.Bonus = input.Bonus;
        entry.Allowance = input.Allowance;
        entry.OtherIncrease = input.OtherIncrease;
        entry.SocialSecurityDeduction = input.SocialSecurityDeduction;
        entry.HousingFundDeduction = input.HousingFundDeduction;
        entry.IndividualIncomeTax = input.IndividualIncomeTax;
        entry.OtherDeduction = input.OtherDeduction;
        entry.GrossPay = calculation.GrossPay;
        entry.TotalDeductions = calculation.TotalDeductions;
        entry.NetPay = calculation.NetPay;
        entry.Note = TrimOrNull(input.Note);
        entry.UpdatedById = actorId;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyToken = Guid.NewGuid();
    }

    private static object AmountSnapshot(PayrollEntry entry) => new
    {
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
        entry.Note
    };

    private (string PersonalName, string BankCardLastFour) GetRecipient(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(user.PersonalName) || string.IsNullOrWhiteSpace(user.BankCardProtected))
            throw Conflict("PROFILE_INCOMPLETE", "个人姓名或银行卡号尚未填写。");
        string bankCardNumber;
        try { bankCardNumber = bankCardProtector.Unprotect(user.BankCardProtected); }
        catch { throw Conflict("BANK_CARD_INVALID", "银行卡信息无法读取，请先更新个人资料。"); }
        if (bankCardNumber.Length < 4 || !bankCardNumber.All(char.IsDigit))
            throw Conflict("BANK_CARD_INVALID", "银行卡信息无效，请先更新个人资料。");
        return (user.PersonalName.Trim(), bankCardNumber[^4..]);
    }

    private static void EnsurePeriodExpected(PayrollPeriod period, Guid token)
    {
        if (period.ConcurrencyToken != token) throw Conflict("PAYROLL_STALE", "工资表已被其他操作更新，请刷新后重试。");
    }

    private static void EnsureEntryExpected(PayrollEntry entry, Guid token)
    {
        if (entry.ConcurrencyToken != token) throw Conflict("PAYROLL_STALE", "工资明细已被其他操作更新，请刷新后重试。");
    }

    private static void EnsureDraft(PayrollPeriod period)
    {
        if (period.Status != PayrollPeriodStatus.Draft) throw Conflict("PAYROLL_STATUS_CONFLICT", "只有草稿工资表可以修改。");
    }

    private static void Touch(PayrollPeriod period, DateTimeOffset? now = null)
    {
        period.UpdatedAt = now ?? DateTimeOffset.UtcNow;
        period.ConcurrencyToken = Guid.NewGuid();
    }

    private async Task SaveChangesAsync(string? uniqueCode, string? uniqueMessage, CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Conflict("PAYROLL_STALE", "工资表已被其他操作更新，请刷新后重试。"); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Conflict(uniqueCode ?? "PAYROLL_CONFLICT", uniqueMessage ?? "工资数据与现有记录冲突，请刷新后重试。");
        }
    }

    private void AddAudit(Guid? actorId, string action, string entityType, Guid entityId, string? traceId, object context) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Context = JsonSerializer.Serialize(context),
            TraceId = traceId ?? string.Empty
        });

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiProblemException NotFound() => new(StatusCodes.Status404NotFound, "PAYROLL_PERIOD_NOT_FOUND", "工资表不存在。");
    private static ApiProblemException EntryNotFound() => new(StatusCodes.Status404NotFound, "PAYROLL_ENTRY_NOT_FOUND", "工资明细不存在。");
    private static ApiProblemException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    private static ApiProblemException Validation(string field, string message) => new(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "提交内容不符合要求。", new Dictionary<string, string[]> { [field] = [message] });
}
