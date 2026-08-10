using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed class ApiProblemException(
    int statusCode,
    string code,
    string message,
    Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}

public sealed class ClaimWorkflowService(AppDbContext db, IBankCardProtector bankCardProtector)
{
    public async Task<ReimbursementClaim> CreateAsync(
        Guid applicantId,
        CreateClaimRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken);
        if (project is null || !project.IsActive)
            throw Validation("projectId", "请选择有效的启用项目。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = new ReimbursementClaim
        {
            ApplicantId = applicantId,
            ClaimNumber = $"BX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Type = request.Type
        };
        db.ReimbursementClaims.Add(claim);
        await SaveChangesAsync(cancellationToken);

        var version = await BuildVersionAsync(
            claim,
            project,
            1,
            applicantId,
            request.Description,
            request.TravelItinerary,
            request.ExpenseItems,
            cancellationToken);
        db.ClaimVersions.Add(version);
        await SaveChangesAsync(cancellationToken);

        claim.CurrentVersionId = version.Id;
        claim.ConcurrencyToken = Guid.NewGuid();
        AddAudit(applicantId, "ClaimCreated", "ReimbursementClaim", claim.Id, traceId, new { version.Id, version.VersionNumber, version.ProjectId });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadClaimAsync(claim.Id, cancellationToken) ?? claim;
    }

    public async Task<ReimbursementClaim> SaveNewVersionAsync(
        Guid applicantId,
        Guid claimId,
        CreateClaimVersionRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureOwner(claim, applicantId);
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ConcurrencyToken);
        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Cancelled)
            throw Conflict("CLAIM_NOT_EDITABLE", "该报销已经批准或作废，不能继续编辑。");

        var current = claim.CurrentVersion!;
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken);
        if (project is null || (!project.IsActive && project.Id != current.ProjectId))
            throw Validation("projectId", "请选择有效的启用项目。");

        var oldStatus = claim.Status;
        var newVersion = await BuildVersionAsync(
            claim,
            project,
            current.VersionNumber + 1,
            applicantId,
            request.Description,
            request.TravelItinerary,
            request.ExpenseItems,
            cancellationToken);

        current.SupersededAt = DateTimeOffset.UtcNow;
        db.ClaimVersions.Add(newVersion);
        claim.CurrentVersionId = newVersion.Id;
        claim.Status = ClaimStatus.Draft;
        claim.PayoutStatus = PayoutStatus.NotApplicable;
        claim.SubmittedAt = null;
        claim.ReviewedAt = null;
        claim.UpdatedAt = DateTimeOffset.UtcNow;
        claim.ConcurrencyToken = Guid.NewGuid();

        if (oldStatus != ClaimStatus.Draft)
        {
            db.ApprovalRecords.Add(new ApprovalRecord
            {
                ClaimId = claim.Id,
                ClaimVersionId = current.Id,
                FromStatus = oldStatus,
                ToStatus = ClaimStatus.Draft,
                ActorId = applicantId,
                Comment = "申请人修改报销，原版本已作废。"
            });
        }
        AddAudit(applicantId, "ClaimVersionCreated", "ReimbursementClaim", claim.Id, traceId, new { oldVersionId = current.Id, newVersionId = newVersion.Id, newVersion.VersionNumber });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadClaimAsync(claim.Id, cancellationToken) ?? claim;
    }

    public async Task<ReimbursementClaim> SubmitAsync(
        Guid applicantId,
        Guid claimId,
        ClaimActionRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureOwner(claim, applicantId);
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ConcurrencyToken);
        if (claim.Status != ClaimStatus.Draft)
            throw Conflict("CLAIM_STATUS_CONFLICT", "只有当前草稿可以提交审核。");

        var errors = ClaimSubmissionValidator.Validate(claim.Type, claim.CurrentVersion!);
        if (errors.Count > 0) throw new ApiProblemException(StatusCodes.Status400BadRequest, "CLAIM_VALIDATION_FAILED", "报销信息不完整。", errors);

        claim.Status = ClaimStatus.Submitted;
        claim.SubmittedAt = claim.UpdatedAt = DateTimeOffset.UtcNow;
        claim.ConcurrencyToken = Guid.NewGuid();
        if (claim.CurrentVersion!.MealAllowance is not null)
        {
            var meal = claim.CurrentVersion.MealAllowance;
            meal.Status = MealAllowanceStatus.PendingTravelReview;
            meal.UpdatedAt = DateTimeOffset.UtcNow;
            meal.ConcurrencyToken = Guid.NewGuid();
        }
        db.ApprovalRecords.Add(new ApprovalRecord
        {
            ClaimId = claim.Id,
            ClaimVersionId = claim.CurrentVersionId!.Value,
            FromStatus = ClaimStatus.Draft,
            ToStatus = ClaimStatus.Submitted,
            ActorId = applicantId
        });
        AddAudit(applicantId, "ClaimSubmitted", "ReimbursementClaim", claim.Id, traceId, new { claim.CurrentVersionId });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    public async Task<ReimbursementClaim> CancelAsync(
        Guid applicantId,
        Guid claimId,
        ClaimActionRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureOwner(claim, applicantId);
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ConcurrencyToken);
        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Cancelled)
            throw Conflict("CLAIM_NOT_CANCELLABLE", "已批准或已作废的报销不能删除。");

        var oldStatus = claim.Status;
        claim.Status = ClaimStatus.Cancelled;
        claim.PayoutStatus = PayoutStatus.NotApplicable;
        claim.CancelledAt = claim.UpdatedAt = DateTimeOffset.UtcNow;
        claim.ConcurrencyToken = Guid.NewGuid();
        if (claim.CurrentVersion!.MealAllowance is not null)
        {
            var meal = claim.CurrentVersion.MealAllowance;
            var oldMealStatus = meal.Status;
            meal.Status = MealAllowanceStatus.Cancelled;
            meal.PayoutStatus = PayoutStatus.NotApplicable;
            meal.UpdatedAt = DateTimeOffset.UtcNow;
            meal.ConcurrencyToken = Guid.NewGuid();
            db.MealAllowanceApprovalRecords.Add(new MealAllowanceApprovalRecord
            {
                MealAllowanceId = meal.Id,
                FromStatus = oldMealStatus,
                ToStatus = MealAllowanceStatus.Cancelled,
                ActorId = applicantId,
                Comment = "差旅报销已作废，餐补同步作废。"
            });
        }
        db.ApprovalRecords.Add(new ApprovalRecord
        {
            ClaimId = claim.Id,
            ClaimVersionId = claim.CurrentVersionId!.Value,
            FromStatus = oldStatus,
            ToStatus = ClaimStatus.Cancelled,
            ActorId = applicantId,
            Comment = "申请人删除或撤回报销。"
        });
        AddAudit(applicantId, "ClaimCancelled", "ReimbursementClaim", claim.Id, traceId, new { claim.CurrentVersionId, oldStatus });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    public Task<ReimbursementClaim> ApproveAsync(Guid administratorId, Guid claimId, ReviewClaimRequest request, string? traceId, CancellationToken cancellationToken)
        => ReviewAsync(administratorId, claimId, request, true, traceId, cancellationToken);

    public Task<ReimbursementClaim> RejectAsync(Guid administratorId, Guid claimId, ReviewClaimRequest request, string? traceId, CancellationToken cancellationToken)
        => ReviewAsync(administratorId, claimId, request, false, traceId, cancellationToken);

    public async Task<ReimbursementClaim> ConfirmPayoutAsync(
        Guid administratorId,
        Guid claimId,
        ConfirmPayoutRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ConcurrencyToken);
        if (claim.Status != ClaimStatus.Approved || claim.PayoutStatus != PayoutStatus.Pending)
            throw Conflict("PAYOUT_STATUS_CONFLICT", "只有已批准且待发放的报销可以确认发放。");
        if (claim.PayoutRecord is not null)
            throw Conflict("PAYOUT_ALREADY_PAID", "该报销已经确认发放。");

        var recipient = GetRecipientSnapshot(claim.Applicant);

        claim.PayoutStatus = PayoutStatus.Paid;
        claim.PaidAt = claim.UpdatedAt = DateTimeOffset.UtcNow;
        claim.ConcurrencyToken = Guid.NewGuid();
        db.PayoutRecords.Add(new PayoutRecord
        {
            ClaimId = claim.Id,
            ApprovedVersionId = claim.CurrentVersionId!.Value,
            Amount = claim.CurrentVersion!.TotalAmount,
            RecipientName = recipient.PersonalName,
            BankCardLastFour = recipient.BankCardLastFour,
            ConfirmedById = administratorId,
            Note = request.Note?.Trim()
        });
        AddAudit(administratorId, "PayoutConfirmed", "ReimbursementClaim", claim.Id, traceId, new { claim.CurrentVersionId, amount = claim.CurrentVersion.TotalAmount });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    public Task<ReimbursementClaim> ApproveMealAllowanceAsync(Guid administratorId, Guid claimId, ReviewMealAllowanceRequest request, string? traceId, CancellationToken cancellationToken)
        => ReviewMealAllowanceAsync(administratorId, claimId, request, true, traceId, cancellationToken);

    public Task<ReimbursementClaim> RejectMealAllowanceAsync(Guid administratorId, Guid claimId, ReviewMealAllowanceRequest request, string? traceId, CancellationToken cancellationToken)
        => ReviewMealAllowanceAsync(administratorId, claimId, request, false, traceId, cancellationToken);

    public async Task<ReimbursementClaim> ConfirmMealAllowancePayoutAsync(
        Guid administratorId,
        Guid claimId,
        ConfirmMealAllowancePayoutRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ClaimConcurrencyToken);
        var meal = claim.CurrentVersion?.MealAllowance ?? throw Conflict("MEAL_ALLOWANCE_NOT_FOUND", "该报销没有餐补记录。");
        EnsureMealExpected(meal, request.MealConcurrencyToken);
        if (claim.Status != ClaimStatus.Approved || meal.Status != MealAllowanceStatus.Approved || meal.PayoutStatus != PayoutStatus.Pending || !meal.TotalAmount.HasValue)
            throw Conflict("MEAL_PAYOUT_STATUS_CONFLICT", "只有已批准且待发放的餐补可以确认发放。");
        if (meal.PayoutRecord is not null)
            throw Conflict("MEAL_PAYOUT_ALREADY_PAID", "该餐补已经确认发放。");

        var recipient = GetRecipientSnapshot(claim.Applicant);
        meal.PayoutStatus = PayoutStatus.Paid;
        meal.PaidAt = meal.UpdatedAt = DateTimeOffset.UtcNow;
        meal.ConcurrencyToken = Guid.NewGuid();
        db.MealAllowancePayoutRecords.Add(new MealAllowancePayoutRecord
        {
            MealAllowanceId = meal.Id,
            Amount = meal.TotalAmount.Value,
            RecipientName = recipient.PersonalName,
            BankCardLastFour = recipient.BankCardLastFour,
            ConfirmedById = administratorId,
            Note = request.Note?.Trim()
        });
        AddAudit(administratorId, "MealAllowancePayoutConfirmed", "MealAllowance", meal.Id, traceId, new { meal.ClaimVersionId, amount = meal.TotalAmount });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    public async Task<ReimbursementClaim?> LoadClaimAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var claim = await db.ReimbursementClaims
            .Include(x => x.Applicant)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.Project)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.TravelItinerary)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.MealAllowance)!.ThenInclude(x => x!.ApprovalRecords)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.MealAllowance)!.ThenInclude(x => x!.PayoutRecord)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.ExpenseItems).ThenInclude(x => x.AttachmentLinks).ThenInclude(x => x.AttachmentAsset)
            .Include(x => x.ApprovalRecords).ThenInclude(x => x.ClaimVersion)
            .Include(x => x.PayoutRecord)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken);

        if (claim is null) return null;

        var actorIds = claim.ApprovalRecords.Select(x => x.ActorId)
            .Append(claim.PayoutRecord?.ConfirmedById ?? Guid.Empty)
            .Concat(claim.CurrentVersion?.MealAllowance?.ApprovalRecords.Select(x => x.ActorId) ?? [])
            .Append(claim.CurrentVersion?.MealAllowance?.PayoutRecord?.ConfirmedById ?? Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (actorIds.Length == 0) return claim;

        var actorNames = await db.Users.AsNoTracking()
            .Where(x => actorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        foreach (var record in claim.ApprovalRecords)
            record.ActorDisplayName = actorNames.GetValueOrDefault(record.ActorId);
        if (claim.PayoutRecord is not null)
            claim.PayoutRecord.ConfirmedByDisplayName = actorNames.GetValueOrDefault(claim.PayoutRecord.ConfirmedById);
        if (claim.CurrentVersion?.MealAllowance is { } meal)
        {
            foreach (var record in meal.ApprovalRecords)
                record.ActorDisplayName = actorNames.GetValueOrDefault(record.ActorId);
            if (meal.PayoutRecord is not null)
                meal.PayoutRecord.ConfirmedByDisplayName = actorNames.GetValueOrDefault(meal.PayoutRecord.ConfirmedById);
        }
        return claim;
    }

    private async Task<ReimbursementClaim> ReviewMealAllowanceAsync(
        Guid administratorId,
        Guid claimId,
        ReviewMealAllowanceRequest request,
        bool approve,
        string? traceId,
        CancellationToken cancellationToken)
    {
        if (!approve && string.IsNullOrWhiteSpace(request.Comment))
            throw Validation("comment", "驳回餐补时必须填写原因。");
        if (approve && (!request.DailyAmount.HasValue || request.DailyAmount.Value <= 0))
            throw Validation("dailyAmount", "每日餐补金额必须大于零。");
        if (approve && decimal.Round(request.DailyAmount!.Value, 2, MidpointRounding.AwayFromZero) != request.DailyAmount.Value)
            throw Validation("dailyAmount", "每日餐补金额最多保留两位小数。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ClaimConcurrencyToken);
        if (claim.Status != ClaimStatus.Approved)
            throw Conflict("TRAVEL_REVIEW_REQUIRED", "必须先批准差旅报销，才能审核餐补。");
        var meal = claim.CurrentVersion?.MealAllowance ?? throw Conflict("MEAL_ALLOWANCE_NOT_FOUND", "该报销没有餐补记录。");
        EnsureMealExpected(meal, request.MealConcurrencyToken);
        if (meal.Status != MealAllowanceStatus.PendingReview)
            throw Conflict("MEAL_ALLOWANCE_STATUS_CONFLICT", "该餐补当前不在待审核状态。");

        var oldStatus = meal.Status;
        meal.Status = approve ? MealAllowanceStatus.Approved : MealAllowanceStatus.Rejected;
        meal.DailyAmount = approve ? decimal.Round(request.DailyAmount!.Value, 2, MidpointRounding.AwayFromZero) : null;
        meal.TotalAmount = approve ? meal.DailyAmount * meal.Days : null;
        meal.PayoutStatus = approve ? PayoutStatus.Pending : PayoutStatus.NotApplicable;
        meal.ReviewedById = administratorId;
        meal.ReviewedAt = meal.UpdatedAt = DateTimeOffset.UtcNow;
        meal.ReviewComment = request.Comment?.Trim();
        meal.ConcurrencyToken = Guid.NewGuid();
        db.MealAllowanceApprovalRecords.Add(new MealAllowanceApprovalRecord
        {
            MealAllowanceId = meal.Id,
            FromStatus = oldStatus,
            ToStatus = meal.Status,
            DailyAmount = meal.DailyAmount,
            TotalAmount = meal.TotalAmount,
            ActorId = administratorId,
            Comment = meal.ReviewComment
        });
        AddAudit(administratorId, approve ? "MealAllowanceApproved" : "MealAllowanceRejected", "MealAllowance", meal.Id, traceId, new { meal.ClaimVersionId, meal.Days, meal.DailyAmount, meal.TotalAmount });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    private async Task<ReimbursementClaim> ReviewAsync(
        Guid administratorId,
        Guid claimId,
        ReviewClaimRequest request,
        bool approve,
        string? traceId,
        CancellationToken cancellationToken)
    {
        if (!approve && string.IsNullOrWhiteSpace(request.Comment))
            throw Validation("comment", "驳回原因不能为空。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await LoadClaimAsync(claimId, cancellationToken) ?? throw NotFound();
        EnsureExpectedVersion(claim, request.ExpectedCurrentVersionId, request.ConcurrencyToken);
        if (claim.Status != ClaimStatus.Submitted)
            throw Conflict("CLAIM_STATUS_CONFLICT", "该报销当前不在待审批状态。");
        if (claim.CurrentVersion!.SupersededAt is not null)
            throw Conflict("CLAIM_VERSION_STALE", "该报销版本已经作废，请刷新审批列表。");

        var newStatus = approve ? ClaimStatus.Approved : ClaimStatus.Rejected;
        claim.Status = newStatus;
        claim.PayoutStatus = approve ? PayoutStatus.Pending : PayoutStatus.NotApplicable;
        claim.ReviewedAt = claim.UpdatedAt = DateTimeOffset.UtcNow;
        claim.ConcurrencyToken = Guid.NewGuid();
        if (claim.CurrentVersion.MealAllowance is { } meal)
        {
            var oldMealStatus = meal.Status;
            meal.Status = approve ? MealAllowanceStatus.PendingReview : MealAllowanceStatus.Rejected;
            meal.PayoutStatus = PayoutStatus.NotApplicable;
            meal.ReviewComment = approve ? null : request.Comment?.Trim();
            meal.UpdatedAt = DateTimeOffset.UtcNow;
            meal.ConcurrencyToken = Guid.NewGuid();
            db.MealAllowanceApprovalRecords.Add(new MealAllowanceApprovalRecord
            {
                MealAllowanceId = meal.Id,
                FromStatus = oldMealStatus,
                ToStatus = meal.Status,
                ActorId = administratorId,
                Comment = approve ? "差旅报销已批准，餐补进入待审核。" : request.Comment?.Trim()
            });
        }
        db.ApprovalRecords.Add(new ApprovalRecord
        {
            ClaimId = claim.Id,
            ClaimVersionId = claim.CurrentVersionId!.Value,
            FromStatus = ClaimStatus.Submitted,
            ToStatus = newStatus,
            ActorId = administratorId,
            Comment = request.Comment?.Trim()
        });
        AddAudit(administratorId, approve ? "ClaimApproved" : "ClaimRejected", "ReimbursementClaim", claim.Id, traceId, new { claim.CurrentVersionId });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    private async Task<ClaimVersion> BuildVersionAsync(
        ReimbursementClaim claim,
        Project project,
        int versionNumber,
        Guid actorId,
        string? description,
        TravelItineraryDraftRequest? itineraryRequest,
        List<ExpenseItemDraftRequest>? itemRequests,
        CancellationToken cancellationToken)
    {
        itemRequests ??= [];
        var explicitKeys = itemRequests.Where(x => x.ClientKey != Guid.Empty).Select(x => x.ClientKey).ToList();
        if (explicitKeys.Count != explicitKeys.Distinct().Count())
            throw Validation("expenseItems", "费用条目标识不能重复。");

        var attachmentIds = itemRequests.SelectMany(x => x.AttachmentIds ?? []).ToList();
        if (attachmentIds.Count != attachmentIds.Distinct().Count())
            throw Validation("attachments", "同一附件不能重复关联到多个费用条目。");

        List<AttachmentAsset> assets = attachmentIds.Count == 0
            ? []
            : await db.AttachmentAssets.Where(x => attachmentIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (assets.Count != attachmentIds.Count
            || assets.Any(x => x.OwnerId != actorId || x.ScanStatus != AttachmentScanStatus.Accepted || (x.BoundClaimId.HasValue && x.BoundClaimId != claim.Id)))
        {
            throw Validation("attachments", "附件不存在、无权使用或不属于当前报销。");
        }
        var assetsById = assets.ToDictionary(x => x.Id);

        var version = new ClaimVersion
        {
            ClaimId = claim.Id,
            VersionNumber = versionNumber,
            ProjectId = project.Id,
            ProjectCodeSnapshot = project.Code,
            ProjectNameSnapshot = project.Name,
            Description = description?.Trim() ?? string.Empty,
            CreatedById = actorId
        };
        if (claim.Type == ClaimType.Travel && itineraryRequest is not null)
        {
            version.TravelItinerary = new TravelItinerary
            {
                ClaimVersionId = version.Id,
                DepartureLocation = TrimOrNull(itineraryRequest.DepartureLocation),
                Destination = TrimOrNull(itineraryRequest.Destination),
                DepartureDate = itineraryRequest.DepartureDate,
                ReturnDate = itineraryRequest.ReturnDate
            };
            version.MealAllowance = new MealAllowance
            {
                ClaimVersionId = version.Id,
                DepartureDate = itineraryRequest.DepartureDate,
                ReturnDate = itineraryRequest.ReturnDate,
                Days = MealAllowanceCalculator.CalculateDays(itineraryRequest.DepartureDate, itineraryRequest.ReturnDate)
            };
        }

        foreach (var itemRequest in itemRequests)
        {
            var item = new ExpenseItem
            {
                ClaimVersionId = version.Id,
                ClientKey = itemRequest.ClientKey == Guid.Empty ? Guid.NewGuid() : itemRequest.ClientKey,
                Category = itemRequest.Category,
                Amount = itemRequest.Amount,
                ExpenseDate = itemRequest.ExpenseDate,
                Merchant = TrimOrNull(itemRequest.Merchant),
                Note = TrimOrNull(itemRequest.Note)
            };
            foreach (var attachmentId in itemRequest.AttachmentIds ?? [])
            {
                var asset = assetsById[attachmentId];
                asset.BoundClaimId = claim.Id;
                asset.BindingStatus = AttachmentBindingStatus.Bound;
                item.AttachmentLinks.Add(new ExpenseItemAttachment { ExpenseItem = item, AttachmentAsset = asset });
            }
            version.ExpenseItems.Add(item);
        }
        version.TotalAmount = version.ExpenseItems.Sum(x => x.Amount ?? 0m);
        return version;
    }

    private static void EnsureOwner(ReimbursementClaim claim, Guid applicantId)
    {
        if (claim.ApplicantId != applicantId)
            throw new ApiProblemException(StatusCodes.Status403Forbidden, "CLAIM_FORBIDDEN", "无权操作该报销。");
    }

    private static void EnsureExpectedVersion(ReimbursementClaim claim, Guid expectedVersionId, Guid concurrencyToken)
    {
        if (claim.CurrentVersionId != expectedVersionId || claim.ConcurrencyToken != concurrencyToken)
            throw Conflict("CLAIM_VERSION_STALE", "该报销已被其他操作更新，请刷新后重试。");
    }

    private static void EnsureMealExpected(MealAllowance meal, Guid concurrencyToken)
    {
        if (meal.ConcurrencyToken != concurrencyToken)
            throw Conflict("MEAL_ALLOWANCE_STALE", "餐补已被其他操作更新，请刷新后重试。");
    }

    private (string PersonalName, string BankCardLastFour) GetRecipientSnapshot(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(user.PersonalName) || string.IsNullOrWhiteSpace(user.BankCardProtected))
            throw Conflict("PROFILE_INCOMPLETE", "申请人尚未填写个人姓名和银行卡号，不能确认发放。");
        var bankCardNumber = bankCardProtector.Unprotect(user.BankCardProtected);
        if (bankCardNumber.Length < 4)
            throw Conflict("BANK_CARD_INVALID", "申请人的银行卡信息无效，请先更新个人资料。");
        return (user.PersonalName, bankCardNumber[^4..]);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict("CLAIM_VERSION_STALE", "该报销已被其他操作更新，请刷新后重试。");
        }
    }

    private void AddAudit(Guid? actorId, string action, string entityType, Guid entityId, string? traceId, object context)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            TraceId = traceId ?? string.Empty,
            Context = System.Text.Json.JsonSerializer.Serialize(context)
        });
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiProblemException Validation(string field, string message) => new(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", message, new() { [field] = [message] });
    private static ApiProblemException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    private static ApiProblemException NotFound() => new(StatusCodes.Status404NotFound, "CLAIM_NOT_FOUND", "报销不存在。");
}
