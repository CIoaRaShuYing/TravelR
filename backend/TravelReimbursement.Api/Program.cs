using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("缺少数据库连接字符串 DefaultConnection。");
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("缺少 Jwt:Key 配置。");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = Guid.TryParse(userId, out var parsedId) ? await userManager.FindByIdAsync(parsedId.ToString()) : null;
                var tokenSecurityStamp = context.Principal?.FindFirstValue(SecurityConstants.SecurityStampClaimType);
                if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(tokenSecurityStamp) || !string.Equals(tokenSecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
                    context.Fail("用户不存在、已停用或登录状态已失效。");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDataProtection().SetApplicationName("TravelReimbursement");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ClaimWorkflowService>();
builder.Services.AddScoped<MonthlyClaimExportService>();
builder.Services.AddScoped<WeeklyReportExportService>();
builder.Services.AddSingleton<IBankCardProtector, BankCardProtector>();
builder.Services.AddHostedService<StagedAttachmentCleanupService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.AddSingleton<IPrivateFileStore, LocalPrivateFileStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
_ = app.Services.GetRequiredService<IPrivateFileStore>();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (exception is ApiProblemException problem)
    {
        context.Response.StatusCode = problem.StatusCode;
        await context.Response.WriteAsJsonAsync(new { code = problem.Code, message = problem.Message, errors = problem.Errors, traceId = context.TraceIdentifier });
        return;
    }
    app.Logger.LogError(exception, "未处理的 API 异常，TraceId: {TraceId}", context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    var message = app.Environment.IsDevelopment() && exception is not null ? exception.Message : "服务器处理请求失败。";
    await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message, traceId = context.TraceIdentifier });
}));
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.Request.Path.StartsWithSegments("/api")
        && !context.Request.Path.StartsWithSegments("/api/me"))
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var profileComplete = Guid.TryParse(userId, out var parsedId)
            && await db.Users.AsNoTracking().AnyAsync(user => user.Id == parsedId
                && user.PersonalName != null && user.PersonalName != string.Empty
                && user.BankCardProtected != null && user.BankCardProtected != string.Empty);
        if (!profileComplete)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = "PROFILE_INCOMPLETE", message = "请先填写个人姓名和银行卡号，报销及餐补将使用该银行卡发放。" });
            return;
        }
    }
    await next();
});
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await SeedAsync(app.Services);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var publicApi = app.MapGroup("/api");
publicApi.MapGet("/registration-settings", async (AppDbContext db) =>
{
    var settings = await GetSettingsAsync(db);
    return Results.Ok(new { registrationMode = settings.RegistrationMode, initialAdministratorRegistration = !await HasAdministratorAsync(db) });
});

publicApi.MapPost("/auth/register", async (RegisterRequest request, AppDbContext db, UserManager<AppUser> userManager, IHttpContextAccessor accessor) =>
{
    var displayName = request.DisplayName.Trim();
    var phoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
    if (displayName.Length is < 1 or > 100 || !IsValidPhoneNumber(phoneNumber) || request.Password.Length < 8)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["registration"] = ["请填写有效姓名、11 位手机号及至少 8 位密码。"] });

    var settings = await GetSettingsAsync(db);
    if (await userManager.FindByNameAsync(phoneNumber) is not null || await db.RegistrationRequests.AnyAsync(x => x.PhoneNumber == phoneNumber && x.Status == RegistrationRequestStatus.Pending))
        return Results.Conflict(new { code = "PHONE_ALREADY_EXISTS", message = "该手机号已注册或已有待审核申请。" });

    await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
    {
        if (!await HasAdministratorAsync(db))
        {
            var initialAdmin = new AppUser { UserName = phoneNumber, PhoneNumber = phoneNumber, DisplayName = displayName, IsActive = true };
            var created = await userManager.CreateAsync(initialAdmin, request.Password);
            if (!created.Succeeded) return Results.ValidationProblem(ToErrors(created.Errors));
            var rolesAdded = await userManager.AddToRolesAsync(initialAdmin, ["Applicant", "Administrator"]);
            if (!rolesAdded.Succeeded) return Results.ValidationProblem(ToErrors(rolesAdded.Errors));
            await AuditAsync(db, null, "InitialAdministratorRegistered", "User", initialAdmin.Id.ToString(), accessor.HttpContext?.TraceIdentifier);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Ok(new { message = "首位管理员账户创建成功，请登录。", registrationMode = settings.RegistrationMode, registrationCompleted = true, initialAdministrator = true });
        }
    }

    if (settings.RegistrationMode == RegistrationMode.Closed)
        return Results.BadRequest(new { code = "REGISTRATION_CLOSED", message = "当前不开放注册，请联系管理员创建账号。" });
    if (settings.RegistrationMode == RegistrationMode.Open)
    {
        var user = new AppUser { UserName = phoneNumber, PhoneNumber = phoneNumber, DisplayName = displayName, IsActive = true };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) return Results.ValidationProblem(ToErrors(created.Errors));
        await userManager.AddToRoleAsync(user, "Applicant");
        await AuditAsync(db, null, "UserRegistered", "User", user.Id.ToString(), accessor.HttpContext?.TraceIdentifier);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "注册成功，请登录。", registrationMode = settings.RegistrationMode, registrationCompleted = true });
    }

    var pendingUser = new AppUser { UserName = phoneNumber, PhoneNumber = phoneNumber, DisplayName = displayName };
    var requestEntity = new RegistrationRequest
    {
        DisplayName = displayName,
        PhoneNumber = phoneNumber,
        PasswordHash = userManager.PasswordHasher.HashPassword(pendingUser, request.Password)
    };
    db.RegistrationRequests.Add(requestEntity);
    await AuditAsync(db, null, "RegistrationRequested", "RegistrationRequest", requestEntity.Id.ToString(), accessor.HttpContext?.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Accepted($"/api/admin/registration-requests/{requestEntity.Id}", new { message = "注册申请已提交，等待管理员审核。", registrationMode = settings.RegistrationMode, registrationCompleted = false });
});

publicApi.MapPost("/auth/login", async (LoginRequest request, UserManager<AppUser> userManager, IConfiguration configuration) =>
{
    var phoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
    var user = IsValidPhoneNumber(phoneNumber) ? await userManager.FindByNameAsync(phoneNumber) : null;
    if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        return Results.Unauthorized();
    var roles = await userManager.GetRolesAsync(user);
    var token = CreateToken(user, roles, configuration);
    var profileIncomplete = string.IsNullOrWhiteSpace(user.PersonalName) || string.IsNullOrWhiteSpace(user.BankCardProtected);
    return Results.Ok(new { token, user = new { user.Id, user.DisplayName, user.PhoneNumber, profileIncomplete }, roles, profileIncomplete });
});

var secured = publicApi.MapGroup(string.Empty).RequireAuthorization();
secured.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> userManager, IBankCardProtector bankCardProtector) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var bankCardNumber = string.IsNullOrWhiteSpace(user.BankCardProtected) ? null : bankCardProtector.Unprotect(user.BankCardProtected);
    return Results.Ok(new { user.Id, user.DisplayName, user.PhoneNumber, user.PersonalName, bankCardNumber, profileIncomplete = string.IsNullOrWhiteSpace(user.PersonalName) || bankCardNumber is null, roles = await userManager.GetRolesAsync(user) });
});

secured.MapGet("/me/profile", async (ClaimsPrincipal principal, UserManager<AppUser> userManager, IBankCardProtector bankCardProtector) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var bankCardNumber = string.IsNullOrWhiteSpace(user.BankCardProtected) ? null : bankCardProtector.Unprotect(user.BankCardProtected);
    return Results.Ok(new { user.PersonalName, bankCardNumber, profileIncomplete = string.IsNullOrWhiteSpace(user.PersonalName) || bankCardNumber is null });
});

secured.MapPut("/me/profile", async (UpdateProfileRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager, IBankCardProtector bankCardProtector, AppDbContext db, HttpContext context) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var personalName = request.PersonalName.Trim();
    var bankCardNumber = request.BankCardNumber.Trim();
    if (personalName.Length is < 1 or > 100 || bankCardNumber.Length is < 16 or > 19 || !bankCardNumber.All(char.IsDigit))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["profile"] = ["请填写有效的个人姓名和 16-19 位银行卡号。"] });

    user.PersonalName = personalName;
    user.BankCardProtected = bankCardProtector.Protect(bankCardNumber);
    await AuditAsync(db, user.Id, "UserProfileUpdated", "User", user.Id.ToString(), context.TraceIdentifier, System.Text.Json.JsonSerializer.Serialize(new { bankCardLastFour = bankCardNumber[^4..] }));
    await db.SaveChangesAsync();
    return Results.Ok(new { user.PersonalName, bankCardNumber, profileIncomplete = false });
});

secured.MapPut("/me/password", async (ChangePasswordRequest request, UserManager<AppUser> userManager, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["原密码不正确。"] }, statusCode: StatusCodes.Status400BadRequest, title: "PASSWORD_INCORRECT");
    if (await userManager.CheckPasswordAsync(user, request.NewPassword))
        return Results.Conflict(new { code = "PASSWORD_UNCHANGED", message = "新密码不能与原密码相同。" });

    await using var transaction = await db.Database.BeginTransactionAsync();
    var changed = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!changed.Succeeded) return Results.ValidationProblem(ToErrors(changed.Errors));
    await AuditAsync(db, user.Id, "PasswordChanged", "User", user.Id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new { message = "密码修改成功，请重新登录。" });
});

secured.MapGet("/projects/available", async (AppDbContext db) => Results.Ok(await db.Projects.AsNoTracking()
    .Where(x => x.IsActive).OrderBy(x => x.Name)
    .Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync()));

secured.MapGet("/projects/mine", async (AppDbContext db, ClaimsPrincipal principal) =>
{
    var userId = GetUserId(principal);
    return Results.Ok(await db.Projects.AsNoTracking()
        .Where(project => project.IsActive || db.ReimbursementClaims.Any(claim => claim.ApplicantId == userId && claim.CurrentVersion!.ProjectId == project.Id))
        .OrderBy(project => project.Name)
        .Select(project => new { project.Id, project.Code, project.Name, project.IsActive })
        .ToListAsync());
});

secured.MapGet("/claims", async (Guid? projectId, ClaimStatus? status, int? page, int? pageSize, AppDbContext db, ClaimsPrincipal principal) =>
{
    var paging = NormalizePaging(page, pageSize);
    var userId = GetUserId(principal);
    var query = db.ReimbursementClaims.AsNoTracking().Where(x => x.ApplicantId == userId);
    if (projectId.HasValue) query = query.Where(x => x.CurrentVersion!.ProjectId == projectId.Value);
    if (status.HasValue) query = query.Where(x => x.Status == status.Value);
    else query = query.Where(x => x.Status != ClaimStatus.Cancelled);
    var total = await query.CountAsync();
    var items = await ProjectClaimList(query.OrderByDescending(x => x.UpdatedAt)).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync();
    return Results.Ok(new PagedResult<object>(items.Cast<object>().ToList(), paging.Page, paging.PageSize, total));
});

secured.MapPost("/claims", async (CreateClaimRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    var claim = await workflow.CreateAsync(GetUserId(principal), request, context.TraceIdentifier, cancellationToken);
    return Results.Created($"/api/claims/{claim.Id}", ToClaimResponse(claim));
});

secured.MapGet("/claims/{id:guid}", async (Guid id, ClaimWorkflowService workflow, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var claim = await workflow.LoadClaimAsync(id, cancellationToken);
    if (claim is null) return Results.NotFound();
    if (!CanAccessClaim(claim, principal)) return Results.Forbid();
    return Results.Ok(ToClaimResponse(claim));
});

secured.MapPost("/claims/{id:guid}/versions", async (Guid id, CreateClaimVersionRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.SaveNewVersionAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));

secured.MapPost("/claims/{id:guid}/submit", async (Guid id, ClaimActionRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.SubmitAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));

secured.MapPost("/claims/{id:guid}/cancel", async (Guid id, ClaimActionRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.CancelAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));

secured.MapGet("/claims/{id:guid}/versions", async (Guid id, AppDbContext db, ClaimsPrincipal principal) =>
{
    var claim = await db.ReimbursementClaims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    if (claim is null) return Results.NotFound();
    if (!CanAccessClaim(claim, principal)) return Results.Forbid();
    var versions = await db.ClaimVersions.AsNoTracking().Where(x => x.ClaimId == id).OrderByDescending(x => x.VersionNumber)
        .Select(x => new { x.Id, x.VersionNumber, x.ProjectId, projectCode = x.ProjectCodeSnapshot, projectName = x.ProjectNameSnapshot, x.Description, x.TotalAmount, x.CreatedAt, x.SupersededAt, isCurrent = x.Id == claim.CurrentVersionId }).ToListAsync();
    return Results.Ok(versions);
});

secured.MapGet("/claims/{claimId:guid}/versions/{versionId:guid}", async (Guid claimId, Guid versionId, AppDbContext db, ClaimsPrincipal principal) =>
{
    var claim = await db.ReimbursementClaims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claimId);
    if (claim is null) return Results.NotFound();
    if (!CanAccessClaim(claim, principal)) return Results.Forbid();
    var version = await db.ClaimVersions.AsNoTracking().Include(x => x.TravelItinerary).Include(x => x.ExpenseItems)
        .ThenInclude(x => x.AttachmentLinks).ThenInclude(x => x.AttachmentAsset)
        .SingleOrDefaultAsync(x => x.Id == versionId && x.ClaimId == claimId);
    return version is null ? Results.NotFound() : Results.Ok(ToVersionResponse(version));
});

secured.MapPost("/attachments/staged", async (IFormFile file, AppDbContext db, IPrivateFileStore fileStore, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    var validation = await AttachmentFileValidator.ValidateAsync(file, cancellationToken);
    if (!validation.IsValid) return Results.BadRequest(new { code = "ATTACHMENT_INVALID", message = validation.ErrorMessage });
    var stored = await fileStore.SaveAsync(file, cancellationToken);
    try
    {
        var asset = new AttachmentAsset
        {
            OwnerId = GetUserId(principal),
            ObjectKey = stored.ObjectKey,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = validation.ContentType!,
            Size = stored.Size,
            Sha256 = stored.Sha256
        };
        db.AttachmentAssets.Add(asset);
        await AuditAsync(db, asset.OwnerId, "AttachmentStaged", "AttachmentAsset", asset.Id.ToString(), context.TraceIdentifier);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/attachments/{asset.Id}/download", new { asset.Id, asset.OriginalFileName, asset.ContentType, asset.Size, asset.ScanStatus, asset.BindingStatus });
    }
    catch
    {
        await fileStore.DeleteAsync(stored.ObjectKey, cancellationToken);
        throw;
    }
}).DisableAntiforgery();

secured.MapGet("/attachments/{id:guid}/download", async (Guid id, AppDbContext db, IPrivateFileStore fileStore, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
{
    var asset = await db.AttachmentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (asset is null) return Results.NotFound();
    if (asset.OwnerId != GetUserId(principal) && !principal.IsInRole("Administrator")) return Results.Forbid();
    var stream = await fileStore.OpenReadAsync(asset.ObjectKey, cancellationToken);
    return Results.File(stream, asset.ContentType, asset.OriginalFileName, enableRangeProcessing: false);
});

secured.MapGet("/weekly-reports", async (Guid? projectId, DateOnly? weekFrom, DateOnly? weekTo, int? page, int? pageSize, AppDbContext db, ClaimsPrincipal principal) =>
{
    var paging = NormalizePaging(page, pageSize);
    var userId = GetUserId(principal);
    var query = db.WeeklyReports.AsNoTracking().Where(x => x.AuthorId == userId);
    if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
    if (weekFrom.HasValue) query = query.Where(x => x.WeekStart >= weekFrom.Value);
    if (weekTo.HasValue) query = query.Where(x => x.WeekStart <= weekTo.Value);
    var total = await query.CountAsync();
    var items = await ProjectWeeklyReports(query.OrderByDescending(x => x.WeekStart).ThenBy(x => x.Project.Name))
        .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync();
    return Results.Ok(new PagedResult<WeeklyReportRow>(items, paging.Page, paging.PageSize, total));
});

secured.MapGet("/weekly-reports/export.xlsx", async (Guid? projectId, DateOnly? weekFrom, DateOnly? weekTo, WeeklyReportExportService exportService, AppDbContext db, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    var userId = GetUserId(principal);
    var result = await exportService.CreateAsync(userId, projectId, weekFrom, weekTo, cancellationToken);
    await AuditAsync(db, userId, "WeeklyReportsExported", "WeeklyReport", userId.ToString(), context.TraceIdentifier,
        System.Text.Json.JsonSerializer.Serialize(new { projectId, weekFrom, weekTo, result.ReportCount }));
    await db.SaveChangesAsync(cancellationToken);
    return Results.File(result.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
});

secured.MapPost("/weekly-reports", async (CreateWeeklyReportRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var userId = GetUserId(principal);
    var validation = ValidateWeeklyReport(request.WeekStart, request.CompletedWork, request.NextWeekPlan);
    if (validation is not null) return validation;
    var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId && x.IsActive);
    if (project is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["projectId"] = ["请选择有效的启用项目。"] });
    if (await db.WeeklyReports.AnyAsync(x => x.AuthorId == userId && x.ProjectId == request.ProjectId && x.WeekStart == request.WeekStart))
        return Results.Conflict(new { code = "WEEKLY_REPORT_DUPLICATE", message = "该项目本周周报已存在，请直接编辑已有记录。" });

    var report = new WeeklyReport
    {
        AuthorId = userId,
        ProjectId = request.ProjectId,
        WeekStart = request.WeekStart,
        CompletedWork = request.CompletedWork.Trim(),
        NextWeekPlan = request.NextWeekPlan.Trim(),
        Issues = TrimOrNull(request.Issues),
        LastEditedById = userId
    };
    db.WeeklyReports.Add(report);
    await AuditAsync(db, userId, "WeeklyReportCreated", "WeeklyReport", report.Id.ToString(), context.TraceIdentifier, System.Text.Json.JsonSerializer.Serialize(new { report.ProjectId, report.WeekStart }));
    await db.SaveChangesAsync();
    return Results.Created($"/api/weekly-reports/{report.Id}", await ProjectWeeklyReports(db.WeeklyReports.AsNoTracking().Where(x => x.Id == report.Id)).SingleAsync());
});

secured.MapPut("/weekly-reports/{id:guid}", async (Guid id, UpdateWeeklyReportRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var userId = GetUserId(principal);
    var report = await db.WeeklyReports.SingleOrDefaultAsync(x => x.Id == id);
    if (report is null) return Results.NotFound();
    if (report.AuthorId != userId && !principal.IsInRole("Administrator")) return Results.Forbid();
    if (report.ConcurrencyToken != request.ConcurrencyToken)
        return Results.Conflict(new { code = "WEEKLY_REPORT_STALE", message = "周报已被其他操作更新，请刷新后重试。" });
    var validation = ValidateWeeklyReport(request.WeekStart, request.CompletedWork, request.NextWeekPlan);
    if (validation is not null) return validation;
    var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId);
    if (project is null || (!project.IsActive && project.Id != report.ProjectId))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["projectId"] = ["请选择有效的启用项目。"] });
    if (await db.WeeklyReports.AnyAsync(x => x.Id != id && x.AuthorId == report.AuthorId && x.ProjectId == request.ProjectId && x.WeekStart == request.WeekStart))
        return Results.Conflict(new { code = "WEEKLY_REPORT_DUPLICATE", message = "该用户在所选项目和周已有周报。" });

    report.ProjectId = request.ProjectId;
    report.WeekStart = request.WeekStart;
    report.CompletedWork = request.CompletedWork.Trim();
    report.NextWeekPlan = request.NextWeekPlan.Trim();
    report.Issues = TrimOrNull(request.Issues);
    report.LastEditedById = userId;
    report.UpdatedAt = DateTimeOffset.UtcNow;
    report.ConcurrencyToken = Guid.NewGuid();
    await AuditAsync(db, userId, "WeeklyReportUpdated", "WeeklyReport", report.Id.ToString(), context.TraceIdentifier, System.Text.Json.JsonSerializer.Serialize(new { report.AuthorId, report.ProjectId, report.WeekStart }));
    await db.SaveChangesAsync();
    return Results.Ok(await ProjectWeeklyReports(db.WeeklyReports.AsNoTracking().Where(x => x.Id == report.Id)).SingleAsync());
});

var admin = secured.MapGroup("/admin").RequireAuthorization(new AuthorizeAttribute { Roles = "Administrator" });
admin.MapGet("/weekly-reports", async (Guid? projectId, Guid? authorId, DateOnly? weekFrom, DateOnly? weekTo, int? page, int? pageSize, AppDbContext db) =>
{
    var paging = NormalizePaging(page, pageSize);
    var query = db.WeeklyReports.AsNoTracking().AsQueryable();
    if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
    if (authorId.HasValue) query = query.Where(x => x.AuthorId == authorId.Value);
    if (weekFrom.HasValue) query = query.Where(x => x.WeekStart >= weekFrom.Value);
    if (weekTo.HasValue) query = query.Where(x => x.WeekStart <= weekTo.Value);
    var total = await query.CountAsync();
    var items = await ProjectWeeklyReports(query.OrderByDescending(x => x.WeekStart).ThenBy(x => x.Project.Name).ThenBy(x => x.Author.DisplayName))
        .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync();
    return Results.Ok(new PagedResult<WeeklyReportRow>(items, paging.Page, paging.PageSize, total));
});
admin.MapGet("/weekly-reports/export.xlsx", async (Guid? projectId, Guid? authorId, DateOnly? weekFrom, DateOnly? weekTo, WeeklyReportExportService exportService, AppDbContext db, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    var userId = GetUserId(principal);
    var result = await exportService.CreateAsync(authorId, projectId, weekFrom, weekTo, cancellationToken);
    await AuditAsync(db, userId, "AdminWeeklyReportsExported", "WeeklyReport", authorId?.ToString() ?? "all", context.TraceIdentifier,
        System.Text.Json.JsonSerializer.Serialize(new { projectId, authorId, weekFrom, weekTo, result.ReportCount }));
    await db.SaveChangesAsync(cancellationToken);
    return Results.File(result.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
});
admin.MapGet("/registration-settings", async (AppDbContext db) =>
{
    var settings = await GetSettingsAsync(db);
    return Results.Ok(new { settings.RegistrationMode, settings.UpdatedAt });
});
admin.MapPut("/registration-settings", async (UpdateRegistrationModeRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var settings = await GetSettingsAsync(db);
    settings.RegistrationMode = request.RegistrationMode;
    settings.UpdatedAt = DateTimeOffset.UtcNow;
    settings.UpdatedById = GetUserId(principal);
    await AuditAsync(db, settings.UpdatedById, "RegistrationModeChanged", "SystemSettings", "1", context.TraceIdentifier, request.RegistrationMode.ToString());
    await db.SaveChangesAsync();
    return Results.Ok(new { settings.RegistrationMode, settings.UpdatedAt });
});

admin.MapGet("/registration-requests", async (RegistrationRequestStatus? status, int? page, int? pageSize, AppDbContext db) =>
{
    var paging = NormalizePaging(page, pageSize);
    var query = db.RegistrationRequests.AsNoTracking().AsQueryable();
    if (status.HasValue) query = query.Where(x => x.Status == status.Value);
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.CreatedAt).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
        .Select(x => new { x.Id, x.DisplayName, x.PhoneNumber, x.Status, x.CreatedAt, x.ReviewedAt, x.ReviewedById, x.ConcurrencyToken }).ToListAsync();
    return Results.Ok(new PagedResult<object>(items.Cast<object>().ToList(), paging.Page, paging.PageSize, total));
});

admin.MapPost("/registration-requests/{id:guid}/approve", async (Guid id, ReviewRegistrationRequest request, AppDbContext db, UserManager<AppUser> userManager, ClaimsPrincipal principal, HttpContext context) =>
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    var pending = await db.RegistrationRequests.SingleOrDefaultAsync(x => x.Id == id);
    if (pending is null) return Results.NotFound();
    if (request.ConcurrencyToken == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发令牌不能为空。"] });
    if (pending.Status != RegistrationRequestStatus.Pending || request.ConcurrencyToken != pending.ConcurrencyToken)
        return Results.Conflict(new { code = "REGISTRATION_REQUEST_STALE", message = "该注册申请已被处理，请刷新。" });
    if (await userManager.FindByNameAsync(pending.PhoneNumber) is not null)
        return Results.Conflict(new { code = "PHONE_ALREADY_EXISTS", message = "该手机号已经是系统用户。" });
    var user = new AppUser { UserName = pending.PhoneNumber, PhoneNumber = pending.PhoneNumber, DisplayName = pending.DisplayName, IsActive = true, PasswordHash = pending.PasswordHash };
    var created = await userManager.CreateAsync(user);
    if (!created.Succeeded) return Results.ValidationProblem(ToErrors(created.Errors));
    await userManager.AddToRoleAsync(user, "Applicant");
    pending.Status = RegistrationRequestStatus.Approved;
    pending.ReviewedById = GetUserId(principal);
    pending.ReviewedAt = DateTimeOffset.UtcNow;
    pending.ConcurrencyToken = Guid.NewGuid();
    await AuditAsync(db, pending.ReviewedById, "RegistrationApproved", "RegistrationRequest", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new { message = "已批准注册申请。" });
});

admin.MapPost("/registration-requests/{id:guid}/reject", async (Guid id, ReviewRegistrationRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var pending = await db.RegistrationRequests.SingleOrDefaultAsync(x => x.Id == id);
    if (pending is null) return Results.NotFound();
    if (request.ConcurrencyToken == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发令牌不能为空。"] });
    if (pending.Status != RegistrationRequestStatus.Pending || request.ConcurrencyToken != pending.ConcurrencyToken)
        return Results.Conflict(new { code = "REGISTRATION_REQUEST_STALE", message = "该注册申请已被处理，请刷新。" });
    pending.Status = RegistrationRequestStatus.Rejected;
    pending.ReviewedById = GetUserId(principal);
    pending.ReviewedAt = DateTimeOffset.UtcNow;
    pending.ConcurrencyToken = Guid.NewGuid();
    await AuditAsync(db, pending.ReviewedById, "RegistrationRejected", "RegistrationRequest", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "已拒绝注册申请。" });
});

admin.MapPost("/users/{id:guid}/administrator/{action:regex(^grant|revoke$)}", async (Guid id, string action, AppDbContext db, UserManager<AppUser> userManager, ClaimsPrincipal principal, HttpContext context) =>
{
    var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
    if (user is null) return Results.NotFound();
    var roles = await userManager.GetRolesAsync(user);
    if (!roles.Any(role => role is "Applicant" or "Administrator")) return Results.NotFound();

    var grant = action == "grant";
    if (grant && !user.IsActive)
        return Results.Conflict(new { code = "USER_INACTIVE_ROLE_CHANGE", message = "停用用户不能设为管理员。" });
    if (!grant && id == GetUserId(principal))
        return Results.Conflict(new { code = "USER_SELF_ADMIN_REVOKE", message = "不能取消当前登录账户的管理员角色。" });
    if (!grant && user.PhoneNumber == SecurityConstants.SuperAdministratorPhoneNumber)
        return Results.Conflict(new { code = "SUPER_ADMIN_ROLE_REQUIRED", message = "超级管理员账号不能取消管理员角色。" });

    if (grant && roles.Contains("Administrator") || !grant && !roles.Contains("Administrator"))
        return Results.Ok(new { user.Id, roles });
    await using var transaction = await db.Database.BeginTransactionAsync();
    var result = grant
        ? await userManager.AddToRoleAsync(user, "Administrator")
        : await userManager.RemoveFromRoleAsync(user, "Administrator");
    if (!result.Succeeded) return Results.ValidationProblem(ToErrors(result.Errors));

    var stampUpdate = await userManager.UpdateSecurityStampAsync(user);
    if (!stampUpdate.Succeeded) return Results.ValidationProblem(ToErrors(stampUpdate.Errors));
    await AuditAsync(db, GetUserId(principal), grant ? "AdministratorGranted" : "AdministratorRevoked", "User", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new { user.Id, roles = await userManager.GetRolesAsync(user) });
});

admin.MapPut("/users/{id:guid}/password", async (Guid id, ResetPasswordRequest request, AppDbContext db, UserManager<AppUser> userManager, ClaimsPrincipal principal, HttpContext context) =>
{
    if (id == GetUserId(principal))
        return Results.Conflict(new { code = "USER_SELF_PASSWORD_RESET", message = "请使用账号安全页面修改自己的密码。" });
    var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
    if (user is null) return Results.NotFound();
    var roles = await userManager.GetRolesAsync(user);
    if (!roles.Any(role => role is "Applicant" or "Administrator")) return Results.NotFound();

    await using var transaction = await db.Database.BeginTransactionAsync();
    var token = await userManager.GeneratePasswordResetTokenAsync(user);
    var reset = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
    if (!reset.Succeeded) return Results.ValidationProblem(ToErrors(reset.Errors));
    await AuditAsync(db, GetUserId(principal), "PasswordResetByAdministrator", "User", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new { user.Id, message = "密码重置成功，目标用户需要使用新密码重新登录。" });
});

admin.MapGet("/users", async (bool? isActive, string? keyword, int? page, int? pageSize, AppDbContext db, IBankCardProtector bankCardProtector, ClaimsPrincipal principal, HttpContext context) =>
{
    var paging = NormalizePaging(page, pageSize);
    var formalUserIds = db.UserRoles
        .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
        .Where(x => x.Name == "Applicant" || x.Name == "Administrator")
        .Select(x => x.UserId)
        .Distinct();
    var query = db.Users.AsNoTracking().Where(user => formalUserIds.Contains(user.Id));
    if (isActive.HasValue) query = query.Where(user => user.IsActive == isActive.Value);
    if (!string.IsNullOrWhiteSpace(keyword))
    {
        var term = keyword.Trim();
        query = query.Where(user => user.DisplayName.Contains(term) || (user.PhoneNumber != null && user.PhoneNumber.Contains(term)));
    }

    var total = await query.CountAsync();
    var users = await query.OrderBy(user => user.DisplayName).ThenBy(user => user.PhoneNumber)
        .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
        .Select(user => new { user.Id, user.DisplayName, user.PersonalName, user.BankCardProtected, PhoneNumber = user.PhoneNumber ?? string.Empty, user.IsActive })
        .ToListAsync();
    var userIds = users.Select(user => user.Id).ToList();
    var roleRows = await db.UserRoles.AsNoTracking()
        .Where(userRole => userIds.Contains(userRole.UserId))
        .Join(db.Roles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
        .Where(x => x.Name != null)
        .ToListAsync();
    var rolesByUserId = roleRows.GroupBy(x => x.UserId)
        .ToDictionary(group => group.Key, group => group.Select(x => x.Name!).OrderBy(x => x).ToArray());
    var items = users.Select(user => new
    {
        user.Id,
        user.DisplayName,
        user.PersonalName,
        bankCardNumber = string.IsNullOrWhiteSpace(user.BankCardProtected) ? null : bankCardProtector.Unprotect(user.BankCardProtected),
        user.PhoneNumber,
        user.IsActive,
        roles = rolesByUserId.GetValueOrDefault(user.Id, [])
    }).Cast<object>().ToList();
    await AuditAsync(db, GetUserId(principal), "UserBankCardsViewed", "UserDirectory", "paged", context.TraceIdentifier, System.Text.Json.JsonSerializer.Serialize(new { page = paging.Page, count = items.Count }));
    await db.SaveChangesAsync();
    return Results.Ok(new PagedResult<object>(items, paging.Page, paging.PageSize, total));
});

admin.MapGet("/applicants", async (string? keyword, int? page, int? pageSize, AppDbContext db) =>
{
    var paging = NormalizePaging(page, pageSize);
    var applicantRoleIds = db.Roles.Where(role => role.Name == "Applicant").Select(role => role.Id);
    var query = db.Users.AsNoTracking().Where(user => user.IsActive && user.PhoneNumber != null
        && db.UserRoles.Any(userRole => userRole.UserId == user.Id && applicantRoleIds.Contains(userRole.RoleId)));
    if (!string.IsNullOrWhiteSpace(keyword))
    {
        var term = keyword.Trim();
        query = query.Where(user => user.DisplayName.Contains(term) || (user.PhoneNumber != null && user.PhoneNumber.Contains(term)));
    }

    var total = await query.CountAsync();
    var items = await query.OrderBy(user => user.DisplayName).ThenBy(user => user.PhoneNumber)
        .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
        .Select(user => new { user.Id, user.DisplayName, PhoneNumber = user.PhoneNumber! })
        .ToListAsync();
    return Results.Ok(new PagedResult<object>(items.Cast<object>().ToList(), paging.Page, paging.PageSize, total));
});

admin.MapPost("/users/{id:guid}/bank-card/copied", async (Guid id, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == id && x.BankCardProtected != null)) return Results.NotFound();
    await AuditAsync(db, GetUserId(principal), "UserBankCardCopied", "User", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

admin.MapGet("/users/{id:guid}/payment-profile", async (Guid id, AppDbContext db, IBankCardProtector bankCardProtector, ClaimsPrincipal principal, HttpContext context) =>
{
    var user = await db.Users.AsNoTracking()
        .Where(x => x.Id == id)
        .Select(x => new { x.Id, x.DisplayName, x.PersonalName, x.BankCardProtected })
        .SingleOrDefaultAsync();
    if (user is null) return Results.NotFound();
    var bankCardNumber = string.IsNullOrWhiteSpace(user.BankCardProtected) ? null : bankCardProtector.Unprotect(user.BankCardProtected);
    await AuditAsync(db, GetUserId(principal), "UserPaymentProfileViewed", "User", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Ok(new { user.Id, user.DisplayName, user.PersonalName, bankCardNumber });
});

admin.MapPost("/users/{id:guid}/{action:regex(^enable|disable$)}", async (Guid id, string action, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
    if (user is null) return Results.NotFound();
    var roleNames = await db.UserRoles.Where(userRole => userRole.UserId == id)
        .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name)
        .Where(roleName => roleName != null)
        .Select(roleName => roleName!)
        .ToListAsync();
    if (!roleNames.Any(roleName => roleName is "Applicant" or "Administrator")) return Results.NotFound();

    var enable = action == "enable";
    if (!enable)
    {
        if (id == GetUserId(principal))
            return Results.Conflict(new { code = "USER_SELF_DISABLE", message = "不能停用当前登录账户。" });

        var isAdministrator = roleNames.Contains("Administrator");
        if (isAdministrator)
        {
            var activeAdministratorCount = await db.Users.Where(item => item.IsActive)
                .Join(db.UserRoles, item => item.Id, userRole => userRole.UserId, (item, userRole) => userRole.RoleId)
                .Join(db.Roles, roleId => roleId, role => role.Id, (roleId, role) => role.Name)
                .CountAsync(roleName => roleName == "Administrator");
            if (activeAdministratorCount <= 1)
                return Results.Conflict(new { code = "LAST_ADMIN_DISABLE", message = "不能停用最后一个启用的管理员账户。" });
        }
    }

    if (user.IsActive == enable) return Results.Ok(new { user.Id, user.IsActive });
    user.IsActive = enable;
    var actorId = GetUserId(principal);
    await AuditAsync(db, actorId, enable ? "UserEnabled" : "UserDisabled", "User", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Ok(new { user.Id, user.IsActive });
});

admin.MapGet("/projects", async (bool? isActive, string? keyword, int? page, int? pageSize, AppDbContext db) =>
{
    var paging = NormalizePaging(page, pageSize);
    var query = db.Projects.AsNoTracking().AsQueryable();
    if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
    if (!string.IsNullOrWhiteSpace(keyword))
    {
        var term = keyword.Trim();
        query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
    }
    var total = await query.CountAsync();
    var items = await query.OrderBy(x => x.Name).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
        .Select(x => new { x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt, x.ConcurrencyToken }).ToListAsync();
    return Results.Ok(new PagedResult<object>(items.Cast<object>().ToList(), paging.Page, paging.PageSize, total));
});

admin.MapPost("/projects", async (CreateProjectRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var code = request.Code.Trim();
    var name = request.Name.Trim();
    if (code.Length == 0 || name.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["project"] = ["项目编码和名称不能为空。"] });
    var normalized = code.ToUpperInvariant();
    if (await db.Projects.AnyAsync(x => x.NormalizedCode == normalized || x.Name == name))
        return Results.Conflict(new { code = "PROJECT_DUPLICATE", message = "项目编码或名称已存在。" });
    var userId = GetUserId(principal);
    var project = new Project { Code = code, NormalizedCode = normalized, Name = name, Description = request.Description?.Trim(), CreatedById = userId, UpdatedById = userId };
    db.Projects.Add(project);
    await AuditAsync(db, userId, "ProjectCreated", "Project", project.Id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Created($"/api/admin/projects/{project.Id}", new { project.Id, project.Code, project.Name, project.Description, project.IsActive, project.ConcurrencyToken });
});

admin.MapPut("/projects/{id:guid}", async (Guid id, UpdateProjectRequest request, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == id);
    if (project is null) return Results.NotFound();
    if (project.ConcurrencyToken != request.ConcurrencyToken) return Results.Conflict(new { code = "PROJECT_STALE", message = "项目已被修改，请刷新。" });
    var name = request.Name.Trim();
    if (name.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["项目名称不能为空。"] });
    if (await db.Projects.AnyAsync(x => x.Id != id && x.Name == name)) return Results.Conflict(new { code = "PROJECT_DUPLICATE", message = "项目名称已存在。" });
    project.Name = name;
    project.Description = request.Description?.Trim();
    project.UpdatedById = GetUserId(principal);
    project.UpdatedAt = DateTimeOffset.UtcNow;
    project.ConcurrencyToken = Guid.NewGuid();
    await AuditAsync(db, project.UpdatedById, "ProjectUpdated", "Project", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Ok(new { project.Id, project.Code, project.Name, project.Description, project.IsActive, project.ConcurrencyToken });
});

admin.MapPost("/projects/{id:guid}/{action:regex(^enable|disable$)}", async (Guid id, string action, AppDbContext db, ClaimsPrincipal principal, HttpContext context) =>
{
    var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == id);
    if (project is null) return Results.NotFound();
    project.IsActive = action == "enable";
    project.UpdatedById = GetUserId(principal);
    project.UpdatedAt = DateTimeOffset.UtcNow;
    project.ConcurrencyToken = Guid.NewGuid();
    await AuditAsync(db, project.UpdatedById, project.IsActive ? "ProjectEnabled" : "ProjectDisabled", "Project", id.ToString(), context.TraceIdentifier);
    await db.SaveChangesAsync();
    return Results.Ok(new { project.Id, project.IsActive, project.ConcurrencyToken });
});

admin.MapGet("/claims", async (Guid? projectId, Guid? applicantId, ClaimStatus? status, PayoutStatus? payoutStatus, string? workQueue, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int? page, int? pageSize, AppDbContext db) =>
{
    var paging = NormalizePaging(page, pageSize);
    var query = db.ReimbursementClaims.AsNoTracking().AsQueryable();
    if (projectId.HasValue) query = query.Where(x => x.CurrentVersion!.ProjectId == projectId.Value);
    if (applicantId.HasValue) query = query.Where(x => x.ApplicantId == applicantId.Value);
    if (string.Equals(workQueue, "approval", StringComparison.OrdinalIgnoreCase))
        query = query.Where(x => x.Status == ClaimStatus.Submitted || x.CurrentVersion!.MealAllowance!.Status == MealAllowanceStatus.PendingReview);
    else if (string.Equals(workQueue, "payout", StringComparison.OrdinalIgnoreCase))
        query = query.Where(x => (x.Status == ClaimStatus.Approved && x.PayoutStatus == PayoutStatus.Pending)
            || (x.CurrentVersion!.MealAllowance!.Status == MealAllowanceStatus.Approved && x.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Pending));
    else
    {
        if (status.HasValue) query = query.Where(x => x.Status == status.Value); else query = query.Where(x => x.Status != ClaimStatus.Cancelled);
        if (payoutStatus.HasValue) query = query.Where(x => x.PayoutStatus == payoutStatus.Value);
    }
    if (createdFrom.HasValue) query = query.Where(x => x.CreatedAt >= createdFrom.Value);
    if (createdTo.HasValue) query = query.Where(x => x.CreatedAt < createdTo.Value.AddDays(1));
    var total = await query.CountAsync();
    var totalAmount = await query.SumAsync(x => (decimal?)x.CurrentVersion!.TotalAmount) ?? 0m;
    var items = await ProjectClaimList(query.OrderByDescending(x => x.UpdatedAt)).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync();
    return Results.Ok(new { items, page = paging.Page, pageSize = paging.PageSize, total, summary = new { claimCount = total, totalAmount } });
});

admin.MapGet("/claims/group-summary", async (string groupBy, Guid? projectId, Guid? applicantId, ClaimStatus? status, PayoutStatus? payoutStatus, string? workQueue, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, AppDbContext db) =>
{
    var query = db.ReimbursementClaims.AsNoTracking().AsQueryable();
    if (projectId.HasValue) query = query.Where(x => x.CurrentVersion!.ProjectId == projectId.Value);
    if (applicantId.HasValue) query = query.Where(x => x.ApplicantId == applicantId.Value);
    if (string.Equals(workQueue, "approval", StringComparison.OrdinalIgnoreCase))
        query = query.Where(x => x.Status == ClaimStatus.Submitted || x.CurrentVersion!.MealAllowance!.Status == MealAllowanceStatus.PendingReview);
    else if (string.Equals(workQueue, "payout", StringComparison.OrdinalIgnoreCase))
        query = query.Where(x => (x.Status == ClaimStatus.Approved && x.PayoutStatus == PayoutStatus.Pending)
            || (x.CurrentVersion!.MealAllowance!.Status == MealAllowanceStatus.Approved && x.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Pending));
    else
    {
        if (status.HasValue) query = query.Where(x => x.Status == status.Value); else query = query.Where(x => x.Status != ClaimStatus.Cancelled);
        if (payoutStatus.HasValue) query = query.Where(x => x.PayoutStatus == payoutStatus.Value);
    }
    if (createdFrom.HasValue) query = query.Where(x => x.CreatedAt >= createdFrom.Value);
    if (createdTo.HasValue) query = query.Where(x => x.CreatedAt < createdTo.Value.AddDays(1));
    if (string.Equals(groupBy, "applicant", StringComparison.OrdinalIgnoreCase))
    {
        var groups = await query.GroupBy(x => new { x.ApplicantId, x.Applicant.DisplayName })
            .Select(g => new { key = g.Key.ApplicantId, label = g.Key.DisplayName, claimCount = g.Count(), totalAmount = g.Sum(x => x.CurrentVersion!.TotalAmount) })
            .OrderBy(x => x.label).ToListAsync();
        return Results.Ok(groups);
    }
    var projectGroups = await query.GroupBy(x => new { x.CurrentVersion!.ProjectId, x.CurrentVersion.Project.Name })
        .Select(g => new { key = g.Key.ProjectId, label = g.Key.Name, claimCount = g.Count(), totalAmount = g.Sum(x => x.CurrentVersion!.TotalAmount) })
        .OrderBy(x => x.label).ToListAsync();
    return Results.Ok(projectGroups);
});

admin.MapPost("/claims/{id:guid}/versions/{versionId:guid}/approve", async (Guid id, Guid versionId, ReviewClaimRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    if (request.ExpectedCurrentVersionId != versionId) throw new ApiProblemException(409, "CLAIM_VERSION_STALE", "审批版本与当前版本不一致。");
    return Results.Ok(ToClaimResponse(await workflow.ApproveAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken)));
});
admin.MapPost("/claims/{id:guid}/versions/{versionId:guid}/reject", async (Guid id, Guid versionId, ReviewClaimRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
{
    if (request.ExpectedCurrentVersionId != versionId) throw new ApiProblemException(409, "CLAIM_VERSION_STALE", "审批版本与当前版本不一致。");
    return Results.Ok(ToClaimResponse(await workflow.RejectAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken)));
});
admin.MapPost("/claims/{id:guid}/payout/confirm", async (Guid id, ConfirmPayoutRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.ConfirmPayoutAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));

admin.MapPost("/claims/{id:guid}/meal-allowance/approve", async (Guid id, ReviewMealAllowanceRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.ApproveMealAllowanceAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));
admin.MapPost("/claims/{id:guid}/meal-allowance/reject", async (Guid id, ReviewMealAllowanceRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.RejectMealAllowanceAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));
admin.MapPost("/claims/{id:guid}/meal-allowance/payout/confirm", async (Guid id, ConfirmMealAllowancePayoutRequest request, ClaimWorkflowService workflow, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken) =>
    Results.Ok(ToClaimResponse(await workflow.ConfirmMealAllowancePayoutAsync(GetUserId(principal), id, request, context.TraceIdentifier, cancellationToken))));

admin.MapGet("/claims/export.zip", ExportClaimsArchiveAsync);
admin.MapGet("/claims/export.xlsx", ExportClaimsArchiveAsync);

app.Run();

static async Task<IResult> ExportClaimsArchiveAsync(Guid projectId, DateOnly? submittedFrom, DateOnly? submittedTo, MonthlyClaimExportService exportService, AppDbContext db, ClaimsPrincipal principal, HttpContext context, CancellationToken cancellationToken)
{
    var result = await exportService.CreateArchiveAsync(projectId, submittedFrom, submittedTo, cancellationToken);
    try
    {
        await AuditAsync(db, GetUserId(principal), "MonthlyClaimsArchiveExported", "Project", projectId.ToString(), context.TraceIdentifier, System.Text.Json.JsonSerializer.Serialize(new { result.From, result.To, result.ClaimCount, result.AttachmentCount }));
        await db.SaveChangesAsync(cancellationToken);
    }
    catch
    {
        await result.Content.DisposeAsync();
        throw;
    }
    return Results.File(result.Content, "application/zip", result.FileName);
}

static async Task SeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (!await db.SystemSettings.AnyAsync())
    {
        db.SystemSettings.Add(new SystemSettings());
        await db.SaveChangesAsync();
    }
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in new[] { "Applicant", "Administrator" })
        if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole<Guid>(role));

    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    if ((await userManager.GetUsersInRoleAsync("Administrator")).Count > 0) return;
    var phoneNumber = configuration["BootstrapAdmin:PhoneNumber"];
    var password = configuration["BootstrapAdmin:Password"];
    var displayName = configuration["BootstrapAdmin:DisplayName"] ?? "系统管理员";
    if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(password)) return;
    if (!IsValidPhoneNumber(phoneNumber)) throw new InvalidOperationException("BootstrapAdmin:PhoneNumber 必须是有效的 11 位手机号。");
    if (await userManager.FindByNameAsync(phoneNumber) is not null) return;
    var admin = new AppUser { UserName = phoneNumber, PhoneNumber = phoneNumber, DisplayName = displayName, IsActive = true };
    var result = await userManager.CreateAsync(admin, password);
    if (!result.Succeeded) throw new InvalidOperationException("初始化管理员账号失败：" + string.Join("; ", result.Errors.Select(x => x.Description)));
    await userManager.AddToRolesAsync(admin, ["Applicant", "Administrator"]);
}

static IQueryable<ClaimListRow> ProjectClaimList(IQueryable<ReimbursementClaim> query) => query.Select(x => new ClaimListRow(
    x.Id,
    x.ClaimNumber,
    x.CurrentVersionId!.Value,
    x.CurrentVersion!.VersionNumber,
    x.Type,
    x.CurrentVersion.ProjectId,
    x.CurrentVersion.Project.Code,
    x.CurrentVersion.Project.Name,
    x.ApplicantId,
    x.Applicant.DisplayName,
    x.CurrentVersion.Description,
    x.CurrentVersion.TotalAmount,
    x.Status,
    x.PayoutStatus,
    x.CurrentVersion.MealAllowance != null ? x.CurrentVersion.MealAllowance.Status : null,
    x.CurrentVersion.MealAllowance != null ? x.CurrentVersion.MealAllowance.PayoutStatus : null,
    x.CurrentVersion.MealAllowance != null ? x.CurrentVersion.MealAllowance.Days : null,
    x.CurrentVersion.MealAllowance != null ? x.CurrentVersion.MealAllowance.TotalAmount : null,
    x.CurrentVersion.MealAllowance != null ? x.CurrentVersion.MealAllowance.ConcurrencyToken : null,
    x.ConcurrencyToken,
    x.CreatedAt,
    x.UpdatedAt));

static IQueryable<WeeklyReportRow> ProjectWeeklyReports(IQueryable<WeeklyReport> query) => query.Select(x => new WeeklyReportRow(
    x.Id,
    x.AuthorId,
    x.Author.DisplayName,
    x.Author.PersonalName,
    x.ProjectId,
    x.Project.Code,
    x.Project.Name,
    x.WeekStart,
    x.CompletedWork,
    x.NextWeekPlan,
    x.Issues,
    x.LastEditedById,
    x.LastEditedBy.DisplayName,
    x.CreatedAt,
    x.UpdatedAt,
    x.ConcurrencyToken));

static object ToClaimResponse(ReimbursementClaim claim) => new
{
    claim.Id,
    claim.ClaimNumber,
    claim.Type,
    claim.Status,
    claim.PayoutStatus,
    claim.ConcurrencyToken,
    claim.CurrentVersionId,
    currentVersion = claim.CurrentVersion is null ? null : ToVersionResponse(claim.CurrentVersion),
    applicant = new { claim.ApplicantId, claim.Applicant.DisplayName, claim.Applicant.PhoneNumber },
    claim.CreatedAt,
    claim.UpdatedAt,
    claim.SubmittedAt,
    claim.ReviewedAt,
    claim.CancelledAt,
    claim.PaidAt,
    approvalRecords = claim.ApprovalRecords.OrderBy(x => x.CreatedAt).Select(x => new { x.ClaimVersionId, versionNumber = x.ClaimVersion.VersionNumber, x.FromStatus, x.ToStatus, x.ActorId, actorDisplayName = x.ActorDisplayName, x.Comment, x.CreatedAt }),
    payoutRecord = claim.PayoutRecord is null ? null : new { claim.PayoutRecord.ApprovedVersionId, claim.PayoutRecord.Amount, claim.PayoutRecord.RecipientName, claim.PayoutRecord.BankCardLastFour, claim.PayoutRecord.ConfirmedById, confirmedByDisplayName = claim.PayoutRecord.ConfirmedByDisplayName, claim.PayoutRecord.Note, claim.PayoutRecord.ConfirmedAt }
};

static object ToVersionResponse(ClaimVersion version) => new
{
    version.Id,
    version.VersionNumber,
    version.ProjectId,
    project = new { code = version.ProjectCodeSnapshot, name = version.ProjectNameSnapshot },
    version.Description,
    version.TotalAmount,
    version.CreatedAt,
    version.SupersededAt,
    travelItinerary = version.TravelItinerary is null ? null : new { version.TravelItinerary.DepartureLocation, version.TravelItinerary.Destination, version.TravelItinerary.DepartureDate, version.TravelItinerary.ReturnDate },
    mealAllowance = version.MealAllowance is null ? null : new
    {
        version.MealAllowance.Id,
        version.MealAllowance.DepartureDate,
        version.MealAllowance.ReturnDate,
        version.MealAllowance.Days,
        version.MealAllowance.DailyAmount,
        version.MealAllowance.TotalAmount,
        version.MealAllowance.Status,
        version.MealAllowance.PayoutStatus,
        version.MealAllowance.ConcurrencyToken,
        version.MealAllowance.ReviewedAt,
        version.MealAllowance.ReviewComment,
        approvalRecords = version.MealAllowance.ApprovalRecords.OrderBy(x => x.CreatedAt).Select(x => new { x.FromStatus, x.ToStatus, x.DailyAmount, x.TotalAmount, x.ActorId, actorDisplayName = x.ActorDisplayName, x.Comment, x.CreatedAt }),
        payoutRecord = version.MealAllowance.PayoutRecord is null ? null : new { version.MealAllowance.PayoutRecord.Amount, version.MealAllowance.PayoutRecord.RecipientName, version.MealAllowance.PayoutRecord.BankCardLastFour, version.MealAllowance.PayoutRecord.ConfirmedById, confirmedByDisplayName = version.MealAllowance.PayoutRecord.ConfirmedByDisplayName, version.MealAllowance.PayoutRecord.Note, version.MealAllowance.PayoutRecord.ConfirmedAt }
    },
    expenseItems = version.ExpenseItems.Select(item => new
    {
        item.Id,
        item.ClientKey,
        item.Category,
        item.Amount,
        item.Currency,
        item.ExpenseDate,
        item.Merchant,
        item.Note,
        attachments = item.AttachmentLinks.Select(link => new { link.AttachmentAsset.Id, link.AttachmentAsset.OriginalFileName, link.AttachmentAsset.ContentType, link.AttachmentAsset.Size, link.AttachmentAsset.ScanStatus })
    })
};

static async Task<SystemSettings> GetSettingsAsync(AppDbContext db)
{
    var settings = await db.SystemSettings.SingleOrDefaultAsync(x => x.Id == 1);
    if (settings is not null) return settings;
    settings = new SystemSettings();
    db.SystemSettings.Add(settings);
    await db.SaveChangesAsync();
    return settings;
}

static Task<bool> HasAdministratorAsync(AppDbContext db) => db.UserRoles
    .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Name)
    .AnyAsync(roleName => roleName == "Administrator");

static string CreateToken(AppUser user, IList<string> roles, IConfiguration configuration)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.DisplayName),
        new(ClaimTypes.MobilePhone, user.PhoneNumber!),
        new(SecurityConstants.SecurityStampClaimType, user.SecurityStamp ?? string.Empty)
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
    var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}

static Guid GetUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
static bool CanAccessClaim(ReimbursementClaim claim, ClaimsPrincipal principal) => principal.IsInRole("Administrator") || claim.ApplicantId == GetUserId(principal);
static bool IsValidPhoneNumber(string phoneNumber) => phoneNumber.Length == 11 && phoneNumber[0] == '1' && phoneNumber[1] is >= '3' and <= '9' && phoneNumber.All(char.IsDigit);
static Dictionary<string, string[]> ToErrors(IEnumerable<IdentityError> errors) => errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray());
static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize) => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 20, 1, 100));
static IResult? ValidateWeeklyReport(DateOnly weekStart, string completedWork, string nextWeekPlan)
{
    if (weekStart.DayOfWeek != DayOfWeek.Monday)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["weekStart"] = ["周报开始日期必须是周一。"] });
    if (string.IsNullOrWhiteSpace(completedWork) || string.IsNullOrWhiteSpace(nextWeekPlan))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["本周完成情况和下周计划不能为空。"] });
    if (completedWork.Trim().Length > 4000 || nextWeekPlan.Trim().Length > 4000)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["content"] = ["周报单项内容不能超过 4000 个字符。"] });
    return null;
}
static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static async Task AuditAsync(AppDbContext db, Guid? actorId, string action, string entityType, string entityId, string? traceId, string? context = null)
{
    db.AuditLogs.Add(new AuditLog { ActorId = actorId, Action = action, EntityType = entityType, EntityId = entityId, TraceId = traceId ?? string.Empty, Context = context });
    await Task.CompletedTask;
}

public sealed record ClaimListRow(
    Guid Id,
    string ClaimNumber,
    Guid CurrentVersionId,
    int VersionNumber,
    ClaimType Type,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    Guid ApplicantId,
    string ApplicantName,
    string Description,
    decimal TotalAmount,
    ClaimStatus Status,
    PayoutStatus PayoutStatus,
    MealAllowanceStatus? MealAllowanceStatus,
    PayoutStatus? MealAllowancePayoutStatus,
    int? MealAllowanceDays,
    decimal? MealAllowanceTotalAmount,
    Guid? MealAllowanceConcurrencyToken,
    Guid ConcurrencyToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WeeklyReportRow(
    Guid Id,
    Guid AuthorId,
    string AuthorDisplayName,
    string? AuthorPersonalName,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly WeekStart,
    string CompletedWork,
    string NextWeekPlan,
    string? Issues,
    Guid LastEditedById,
    string LastEditedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyToken);

public partial class Program;


static class SecurityConstants
{
    public const string SuperAdministratorPhoneNumber = "13730614340";
    public const string SecurityStampClaimType = "security_stamp";
}
