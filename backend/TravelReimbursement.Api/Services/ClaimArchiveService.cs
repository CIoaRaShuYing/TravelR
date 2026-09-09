using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed record ClaimArchiveBlockedReason(string Reason, int Count);

public sealed record ClaimArchivePreviewResult(
    DateOnly SubmittedFrom,
    DateOnly SubmittedTo,
    int MatchingClaimCount,
    int EligibleClaimCount,
    int BlockedClaimCount,
    decimal ReimbursementAmount,
    int MealAllowanceCount,
    decimal MealAllowanceAmount,
    IReadOnlyList<ClaimArchiveBlockedReason> BlockedReasons);

public sealed record ClaimArchiveBatchSummary(
    Guid Id,
    string Name,
    DateOnly SubmittedFrom,
    DateOnly SubmittedTo,
    int ClaimCount,
    decimal ReimbursementAmount,
    int MealAllowanceCount,
    decimal MealAllowanceAmount,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyToken);

public sealed class ClaimArchiveService(AppDbContext db)
{
    public async Task<ClaimArchivePreviewResult> PreviewAsync(
        DateOnly submittedFrom,
        DateOnly submittedTo,
        CancellationToken cancellationToken)
    {
        ValidateRange(submittedFrom, submittedTo);
        var claims = await LoadRangeClaims(submittedFrom, submittedTo, tracking: false, cancellationToken);
        return CreatePreview(submittedFrom, submittedTo, claims);
    }

    public async Task<ClaimArchiveBatchSummary> CreateAsync(
        Guid administratorId,
        CreateClaimArchiveBatchRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var name = NormalizeDisplayName(request.Name);
        var normalizedName = NormalizeName(name);
        ValidateRange(request.SubmittedFrom, request.SubmittedTo);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.ClaimArchiveBatches.AnyAsync(batch => batch.NormalizedName == normalizedName, cancellationToken))
            throw Conflict("ARCHIVE_NAME_CONFLICT", "归档名称已存在，请使用其他名称。");

        var claims = await LoadRangeClaims(request.SubmittedFrom, request.SubmittedTo, tracking: true, cancellationToken);
        if (claims.Count == 0)
            throw Conflict("ARCHIVE_SCOPE_EMPTY", "所选提交日期范围内没有报销。");

        var preview = CreatePreview(request.SubmittedFrom, request.SubmittedTo, claims);
        if (preview.BlockedClaimCount > 0)
        {
            var reasons = string.Join("；", preview.BlockedReasons.Select(item => $"{item.Reason}{item.Count}笔"));
            throw Conflict("ARCHIVE_SCOPE_NOT_READY", $"所选范围存在不可归档报销：{reasons}。");
        }

        var now = DateTimeOffset.UtcNow;
        var batch = new ClaimArchiveBatch
        {
            Name = name,
            NormalizedName = normalizedName,
            SubmittedFrom = request.SubmittedFrom,
            SubmittedTo = request.SubmittedTo,
            CreatedById = administratorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var claim in claims)
        {
            claim.ArchiveBatch = batch;
            claim.ConcurrencyToken = Guid.NewGuid();
        }

        db.ClaimArchiveBatches.Add(batch);
        AddAudit(administratorId, "ClaimArchiveBatchCreated", batch.Id, traceId, new
        {
            batch.Name,
            batch.SubmittedFrom,
            batch.SubmittedTo,
            preview.EligibleClaimCount,
            preview.ReimbursementAmount,
            preview.MealAllowanceCount,
            preview.MealAllowanceAmount
        });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(batch.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimArchiveBatchSummary>> ListAsync(CancellationToken cancellationToken) =>
        await ProjectSummaries(db.ClaimArchiveBatches.AsNoTracking()
            .OrderByDescending(batch => batch.SubmittedTo)
            .ThenByDescending(batch => batch.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<ClaimArchiveBatchSummary> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ProjectSummaries(db.ClaimArchiveBatches.AsNoTracking().Where(batch => batch.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound();

    public async Task<ClaimArchiveBatchSummary> RenameAsync(
        Guid administratorId,
        Guid id,
        RenameClaimArchiveBatchRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var name = NormalizeDisplayName(request.Name);
        var normalizedName = NormalizeName(name);
        var batch = await db.ClaimArchiveBatches.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound();
        if (batch.ConcurrencyToken != request.ConcurrencyToken)
            throw Conflict("ARCHIVE_BATCH_STALE", "归档批次已被其他操作更新，请刷新后重试。");
        if (await db.ClaimArchiveBatches.AnyAsync(item => item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
            throw Conflict("ARCHIVE_NAME_CONFLICT", "归档名称已存在，请使用其他名称。");

        var oldName = batch.Name;
        batch.Name = name;
        batch.NormalizedName = normalizedName;
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        batch.ConcurrencyToken = Guid.NewGuid();
        AddAudit(administratorId, "ClaimArchiveBatchRenamed", batch.Id, traceId, new { oldName, newName = name });
        await SaveChangesAsync(cancellationToken);
        return await GetAsync(batch.Id, cancellationToken);
    }

    private async Task<List<ReimbursementClaim>> LoadRangeClaims(
        DateOnly submittedFrom,
        DateOnly submittedTo,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var (fromInstant, toExclusive) = ChinaDateRange(submittedFrom, submittedTo);
        var query = db.ReimbursementClaims
            .Include(claim => claim.CurrentVersion)!.ThenInclude(version => version!.MealAllowance)
            .Where(claim => claim.CurrentVersion != null
                && claim.SubmittedAt != null
                && claim.SubmittedAt >= fromInstant
                && claim.SubmittedAt < toExclusive)
            .OrderBy(claim => claim.SubmittedAt)
            .ThenBy(claim => claim.ClaimNumber);
        return await (tracking ? query : query.AsNoTracking()).ToListAsync(cancellationToken);
    }

    private static ClaimArchivePreviewResult CreatePreview(
        DateOnly submittedFrom,
        DateOnly submittedTo,
        IReadOnlyList<ReimbursementClaim> claims)
    {
        var blocked = claims
            .Select(ClaimArchiveEligibility.GetBlockingReason)
            .Where(reason => reason is not null)
            .GroupBy(reason => reason!)
            .Select(group => new ClaimArchiveBlockedReason(group.Key, group.Count()))
            .OrderBy(item => item.Reason)
            .ToList();
        var eligible = claims.Where(claim => ClaimArchiveEligibility.GetBlockingReason(claim) is null).ToList();
        var reimbursementAmount = eligible
            .Where(claim => claim is { Status: ClaimStatus.Approved, PayoutStatus: PayoutStatus.Paid })
            .Sum(claim => claim.CurrentVersion!.TotalAmount);
        var paidMeals = eligible
            .Select(claim => claim.CurrentVersion!.MealAllowance)
            .Where(meal => meal is { Status: MealAllowanceStatus.Approved, PayoutStatus: PayoutStatus.Paid, TotalAmount: not null })
            .ToList();

        return new ClaimArchivePreviewResult(
            submittedFrom,
            submittedTo,
            claims.Count,
            eligible.Count,
            claims.Count - eligible.Count,
            reimbursementAmount,
            paidMeals.Count,
            paidMeals.Sum(meal => meal!.TotalAmount!.Value),
            blocked);
    }

    private static IQueryable<ClaimArchiveBatchSummary> ProjectSummaries(IQueryable<ClaimArchiveBatch> query) =>
        query.Select(batch => new ClaimArchiveBatchSummary(
            batch.Id,
            batch.Name,
            batch.SubmittedFrom,
            batch.SubmittedTo,
            batch.Claims.Count,
            batch.Claims.Where(claim => claim.Status == ClaimStatus.Approved && claim.PayoutStatus == PayoutStatus.Paid)
                .Sum(claim => (decimal?)claim.CurrentVersion!.TotalAmount) ?? 0m,
            batch.Claims.Count(claim => claim.CurrentVersion!.MealAllowance != null
                && claim.CurrentVersion.MealAllowance.Status == MealAllowanceStatus.Approved
                && claim.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Paid),
            batch.Claims.Where(claim => claim.CurrentVersion!.MealAllowance != null
                    && claim.CurrentVersion.MealAllowance.Status == MealAllowanceStatus.Approved
                    && claim.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Paid)
                .Sum(claim => claim.CurrentVersion!.MealAllowance!.TotalAmount) ?? 0m,
            batch.CreatedById,
            batch.CreatedBy.DisplayName,
            batch.CreatedAt,
            batch.UpdatedAt,
            batch.ConcurrencyToken));

    private static (DateTimeOffset FromInstant, DateTimeOffset ToExclusive) ChinaDateRange(DateOnly from, DateOnly to) =>
        (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime(),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime());

    private static string NormalizeDisplayName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 200)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "归档名称长度必须为 1 至 200 个字符。",
                new() { ["name"] = ["归档名称长度必须为 1 至 200 个字符。"] });
        return name;
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "ARCHIVE_DATE_INVALID", "归档结束日期不能早于开始日期。");
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("ARCHIVE_SCOPE_CHANGED", "报销或归档批次已发生变化，请刷新预览后重试。");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Conflict("ARCHIVE_NAME_CONFLICT", "归档名称已存在，请使用其他名称。");
        }
    }

    private void AddAudit(Guid actorId, string action, Guid batchId, string? traceId, object context) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = "ClaimArchiveBatch",
            EntityId = batchId.ToString(),
            TraceId = traceId ?? string.Empty,
            Context = JsonSerializer.Serialize(context)
        });

    private static ApiProblemException Conflict(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, message);

    private static ApiProblemException NotFound() =>
        new(StatusCodes.Status404NotFound, "ARCHIVE_BATCH_NOT_FOUND", "归档批次不存在。");
}
