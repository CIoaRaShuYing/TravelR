using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed class MeetingRecordService(AppDbContext db)
{
    public async Task<PagedResult<MeetingRecordListRow>> ListAsync(
        Guid? projectId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateTo.Value < dateFrom.Value)
            throw Validation("dateRange", "结束日期不能早于开始日期。");

        var query = db.MeetingRecords.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(record => record.ProjectId == projectId.Value);
        if (dateFrom.HasValue) query = query.Where(record => record.MeetingDate >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(record => record.MeetingDate <= dateTo.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(record => record.MeetingDate)
            .ThenByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(record => new MeetingRecordListRow(
                record.Id,
                record.ProjectId,
                record.Project.Code,
                record.Project.Name,
                record.MeetingDate,
                record.Location,
                record.Participants.Count,
                record.Items.Count(item => item.Kind == MeetingRecordItemKind.Requirement),
                record.Items.Count(item => item.Kind == MeetingRecordItemKind.WorkFocus),
                record.Items.OrderBy(item => item.Kind).ThenBy(item => item.SortOrder).Select(item => item.Content).FirstOrDefault(),
                record.CreatedById,
                record.CreatedBy.DisplayName,
                record.LastEditedById,
                record.LastEditedBy.DisplayName,
                record.CreatedAt,
                record.UpdatedAt,
                record.ConcurrencyToken))
            .ToListAsync(cancellationToken);

        return new PagedResult<MeetingRecordListRow>(items, page, pageSize, total);
    }

    public async Task<MeetingRecordDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await LoadAsync(id, asTracking: false, cancellationToken);
        return record is null ? null : ToDetail(record);
    }

    public async Task<MeetingRecordDetail> CreateAsync(
        Guid actorId,
        CreateMeetingRecordRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var input = Normalize(request.ProjectId, request.MeetingDate, request.Location, request.Participants, request.Requirements, request.WorkFocuses);
        var projectExists = await db.Projects.AnyAsync(project => project.Id == input.ProjectId && project.IsActive, cancellationToken);
        if (!projectExists) throw Validation("projectId", "请选择有效的启用项目。");

        var record = new MeetingRecord
        {
            ProjectId = input.ProjectId,
            MeetingDate = input.MeetingDate,
            Location = input.Location,
            CreatedById = actorId,
            LastEditedById = actorId,
            Participants = CreateParticipants(input.Participants),
            Items = CreateItems(input.Requirements, input.WorkFocuses)
        };
        db.MeetingRecords.Add(record);
        AddAudit(actorId, "MeetingRecordCreated", record.Id, traceId, record, input);
        await SaveChangesAsync(cancellationToken);
        return (await GetAsync(record.Id, cancellationToken))!;
    }

    public async Task<MeetingRecordDetail> UpdateAsync(
        Guid actorId,
        Guid id,
        UpdateMeetingRecordRequest request,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var record = await LoadAsync(id, asTracking: true, cancellationToken)
            ?? throw new ApiProblemException(StatusCodes.Status404NotFound, "MEETING_RECORD_NOT_FOUND", "会议记录不存在或已删除。");
        EnsureCurrent(record, request.ConcurrencyToken);
        var input = Normalize(request.ProjectId, request.MeetingDate, request.Location, request.Participants, request.Requirements, request.WorkFocuses);

        var project = await db.Projects.SingleOrDefaultAsync(candidate => candidate.Id == input.ProjectId, cancellationToken);
        if (project is null || (!project.IsActive && project.Id != record.ProjectId))
            throw Validation("projectId", "请选择有效的启用项目。");

        record.ProjectId = input.ProjectId;
        record.MeetingDate = input.MeetingDate;
        record.Location = input.Location;
        record.LastEditedById = actorId;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.ConcurrencyToken = Guid.NewGuid();
        SyncParticipants(record, input.Participants);
        SyncItems(record, MeetingRecordItemKind.Requirement, input.Requirements);
        SyncItems(record, MeetingRecordItemKind.WorkFocus, input.WorkFocuses);
        AddAudit(actorId, "MeetingRecordUpdated", record.Id, traceId, record, input);
        await SaveChangesAsync(cancellationToken);
        return (await GetAsync(record.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(
        Guid actorId,
        Guid id,
        Guid concurrencyToken,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var record = await db.MeetingRecords.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new ApiProblemException(StatusCodes.Status404NotFound, "MEETING_RECORD_NOT_FOUND", "会议记录不存在或已删除。");
        EnsureCurrent(record, concurrencyToken);
        var now = DateTimeOffset.UtcNow;
        record.DeletedById = actorId;
        record.DeletedAt = now;
        record.LastEditedById = actorId;
        record.UpdatedAt = now;
        record.ConcurrencyToken = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = "MeetingRecordDeleted",
            EntityType = nameof(MeetingRecord),
            EntityId = record.Id.ToString(),
            TraceId = traceId ?? string.Empty,
            Context = JsonSerializer.Serialize(new { record.ProjectId, record.MeetingDate })
        });
        await SaveChangesAsync(cancellationToken);
    }

    internal static NormalizedMeetingRecordInput Normalize(
        Guid projectId,
        DateOnly meetingDate,
        string? location,
        IReadOnlyList<MeetingParticipantRequest>? participants,
        IReadOnlyList<MeetingRecordItemRequest>? requirements,
        IReadOnlyList<MeetingRecordItemRequest>? workFocuses)
    {
        var errors = new Dictionary<string, string[]>();
        if (projectId == Guid.Empty) errors["projectId"] = ["请选择项目。"];
        if (meetingDate == default) errors["meetingDate"] = ["请选择会议日期。"];
        var normalizedLocation = NormalizeRequired(location, 200, "会议地点", errors, "location");
        participants ??= [];
        requirements ??= [];
        workFocuses ??= [];
        if (participants.Count is < 1 or > 100) errors["participants"] = ["参会人员数量必须在 1 到 100 之间。"];
        if (requirements.Count > 100) errors["requirements"] = ["需求内容不能超过 100 条。"];
        if (workFocuses.Count > 100) errors["workFocuses"] = ["工作重点不能超过 100 条。"];
        if (requirements.Count + workFocuses.Count < 1) errors["items"] = ["请至少填写一条需求内容或工作重点。"];

        var normalizedParticipants = participants.Select((participant, index) => new NormalizedMeetingParticipant(
            NormalizeRequired(participant?.Name, 100, $"第 {index + 1} 位参会人员姓名", errors, $"participants[{index}].name"),
            NormalizeOptional(participant?.Organization, 200, $"第 {index + 1} 位参会人员单位", errors, $"participants[{index}].organization"),
            NormalizeOptional(participant?.Title, 100, $"第 {index + 1} 位参会人员职务", errors, $"participants[{index}].title"),
            NormalizeOptional(participant?.Phone, 50, $"第 {index + 1} 位参会人员电话", errors, $"participants[{index}].phone")))
            .ToArray();
        var normalizedRequirements = NormalizeItems(requirements, "requirements", "需求内容", errors);
        var normalizedWorkFocuses = NormalizeItems(workFocuses, "workFocuses", "工作重点", errors);
        if (errors.Count > 0)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "MEETING_RECORD_INVALID", "会议记录内容不完整。", errors);

        return new NormalizedMeetingRecordInput(projectId, meetingDate, normalizedLocation, normalizedParticipants, normalizedRequirements, normalizedWorkFocuses);
    }

    private async Task<MeetingRecord?> LoadAsync(Guid id, bool asTracking, CancellationToken cancellationToken)
    {
        IQueryable<MeetingRecord> query = db.MeetingRecords
            .Include(record => record.Project)
            .Include(record => record.CreatedBy)
            .Include(record => record.LastEditedBy)
            .Include(record => record.Participants)
            .Include(record => record.Items);
        if (!asTracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(record => record.Id == id, cancellationToken);
    }

    private static MeetingRecordDetail ToDetail(MeetingRecord record) => new(
        record.Id,
        record.ProjectId,
        record.Project.Code,
        record.Project.Name,
        record.Project.IsActive,
        record.MeetingDate,
        record.Location,
        record.Participants.OrderBy(participant => participant.SortOrder).Select(participant => new MeetingParticipantRow(
            participant.Id, participant.Name, participant.Organization, participant.Title, participant.Phone, participant.SortOrder)).ToArray(),
        record.Items.Where(item => item.Kind == MeetingRecordItemKind.Requirement).OrderBy(item => item.SortOrder).Select(ToItemRow).ToArray(),
        record.Items.Where(item => item.Kind == MeetingRecordItemKind.WorkFocus).OrderBy(item => item.SortOrder).Select(ToItemRow).ToArray(),
        record.CreatedById,
        record.CreatedBy.DisplayName,
        record.LastEditedById,
        record.LastEditedBy.DisplayName,
        record.CreatedAt,
        record.UpdatedAt,
        record.ConcurrencyToken);

    private static MeetingRecordItemRow ToItemRow(MeetingRecordItem item) =>
        new(item.Id, item.Content, item.Status, item.DueDate, item.Owner, item.SortOrder);

    private static List<MeetingParticipant> CreateParticipants(IReadOnlyList<NormalizedMeetingParticipant> participants) =>
        participants.Select((participant, index) => new MeetingParticipant
        {
            SortOrder = index,
            Name = participant.Name,
            Organization = participant.Organization,
            Title = participant.Title,
            Phone = participant.Phone
        }).ToList();

    private static List<MeetingRecordItem> CreateItems(
        IReadOnlyList<NormalizedMeetingRecordItem> requirements,
        IReadOnlyList<NormalizedMeetingRecordItem> workFocuses) =>
        requirements.Select((item, index) => CreateItem(MeetingRecordItemKind.Requirement, index, item))
            .Concat(workFocuses.Select((item, index) => CreateItem(MeetingRecordItemKind.WorkFocus, index, item)))
            .ToList();

    private static MeetingRecordItem CreateItem(MeetingRecordItemKind kind, int sortOrder, NormalizedMeetingRecordItem item) => new()
    {
        Kind = kind,
        SortOrder = sortOrder,
        Content = item.Content,
        Status = item.Status,
        DueDate = item.DueDate,
        Owner = item.Owner
    };

    private void SyncParticipants(MeetingRecord record, IReadOnlyList<NormalizedMeetingParticipant> input)
    {
        var existing = record.Participants.OrderBy(participant => participant.SortOrder).ToList();
        for (var index = 0; index < input.Count; index++)
        {
            var participant = index < existing.Count ? existing[index] : new MeetingParticipant { MeetingRecordId = record.Id };
            if (index >= existing.Count) record.Participants.Add(participant);
            participant.SortOrder = index;
            participant.Name = input[index].Name;
            participant.Organization = input[index].Organization;
            participant.Title = input[index].Title;
            participant.Phone = input[index].Phone;
        }
        if (existing.Count > input.Count) db.MeetingParticipants.RemoveRange(existing.Skip(input.Count));
    }

    private void SyncItems(MeetingRecord record, MeetingRecordItemKind kind, IReadOnlyList<NormalizedMeetingRecordItem> input)
    {
        var existing = record.Items.Where(item => item.Kind == kind).OrderBy(item => item.SortOrder).ToList();
        for (var index = 0; index < input.Count; index++)
        {
            var item = index < existing.Count ? existing[index] : new MeetingRecordItem { MeetingRecordId = record.Id, Kind = kind };
            if (index >= existing.Count) record.Items.Add(item);
            item.SortOrder = index;
            item.Content = input[index].Content;
            item.Status = input[index].Status;
            item.DueDate = input[index].DueDate;
            item.Owner = input[index].Owner;
        }
        if (existing.Count > input.Count) db.MeetingRecordItems.RemoveRange(existing.Skip(input.Count));
    }

    private void AddAudit(Guid actorId, string action, Guid entityId, string? traceId, MeetingRecord record, NormalizedMeetingRecordInput input) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = nameof(MeetingRecord),
            EntityId = entityId.ToString(),
            TraceId = traceId ?? string.Empty,
            Context = JsonSerializer.Serialize(new
            {
                record.ProjectId,
                record.MeetingDate,
                participantCount = input.Participants.Count,
                requirementCount = input.Requirements.Count,
                workFocusCount = input.WorkFocuses.Count
            })
        });

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Stale();
        }
    }

    private static void EnsureCurrent(MeetingRecord record, Guid concurrencyToken)
    {
        if (concurrencyToken == Guid.Empty || record.ConcurrencyToken != concurrencyToken) throw Stale();
    }

    private static NormalizedMeetingRecordItem[] NormalizeItems(
        IReadOnlyList<MeetingRecordItemRequest> items,
        string key,
        string label,
        Dictionary<string, string[]> errors) =>
        items.Select((item, index) => new NormalizedMeetingRecordItem(
            NormalizeRequired(item?.Content, 4000, $"第 {index + 1} 条{label}", errors, $"{key}[{index}].content"),
            NormalizeOptional(item?.Status, 100, $"第 {index + 1} 条{label}状态", errors, $"{key}[{index}].status"),
            item?.DueDate,
            NormalizeOptional(item?.Owner, 100, $"第 {index + 1} 条{label}负责人", errors, $"{key}[{index}].owner")))
            .ToArray();

    private static string NormalizeRequired(string? value, int maxLength, string label, Dictionary<string, string[]> errors, string key)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) errors[key] = [$"{label}不能为空。"];
        else if (normalized.Length > maxLength) errors[key] = [$"{label}不能超过 {maxLength} 个字符。"];
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label, Dictionary<string, string[]> errors, string key)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength) errors[key] = [$"{label}不能超过 {maxLength} 个字符。"];
        return normalized;
    }

    private static ApiProblemException Validation(string key, string message) =>
        new(StatusCodes.Status400BadRequest, "MEETING_RECORD_INVALID", message, new Dictionary<string, string[]> { [key] = [message] });

    private static ApiProblemException Stale() =>
        new(StatusCodes.Status409Conflict, "MEETING_RECORD_STALE", "会议记录已被其他用户修改或删除，请刷新后重试。");
}

public sealed record MeetingRecordListRow(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly MeetingDate,
    string Location,
    int ParticipantCount,
    int RequirementCount,
    int WorkFocusCount,
    string? FirstItemContent,
    Guid CreatedById,
    string CreatedByDisplayName,
    Guid LastEditedById,
    string LastEditedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyToken);

public sealed record MeetingRecordDetail(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    bool ProjectIsActive,
    DateOnly MeetingDate,
    string Location,
    IReadOnlyList<MeetingParticipantRow> Participants,
    IReadOnlyList<MeetingRecordItemRow> Requirements,
    IReadOnlyList<MeetingRecordItemRow> WorkFocuses,
    Guid CreatedById,
    string CreatedByDisplayName,
    Guid LastEditedById,
    string LastEditedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyToken);

public sealed record MeetingParticipantRow(Guid Id, string Name, string? Organization, string? Title, string? Phone, int SortOrder);
public sealed record MeetingRecordItemRow(Guid Id, string Content, string? Status, DateOnly? DueDate, string? Owner, int SortOrder);

internal sealed record NormalizedMeetingRecordInput(
    Guid ProjectId,
    DateOnly MeetingDate,
    string Location,
    IReadOnlyList<NormalizedMeetingParticipant> Participants,
    IReadOnlyList<NormalizedMeetingRecordItem> Requirements,
    IReadOnlyList<NormalizedMeetingRecordItem> WorkFocuses);

internal sealed record NormalizedMeetingParticipant(string Name, string? Organization, string? Title, string? Phone);
internal sealed record NormalizedMeetingRecordItem(string Content, string? Status, DateOnly? DueDate, string? Owner);
