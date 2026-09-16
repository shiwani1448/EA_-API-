using FluentValidation;
using FluentValidation.AspNetCore;
using Jarvis5.Mapping;
using Jarvis5.Middleware;
using Jarvis5.Repositories;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using Jarvis5.Data.EaFms;

using HrmsDbContext = hrms_api.Data.AppDbContext;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONTROLLERS & JSON
// ============================================================

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new Jarvis5.Common.DateTimeUtcConverter());

        options.JsonSerializerOptions.Converters.Add(
            new Jarvis5.Common.NullableDateTimeUtcConverter());
    });

// ============================================================
// FLUENT VALIDATION
// ============================================================

builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddValidatorsFromAssemblyContaining<
    CreateRequestDtoValidator>();

// ============================================================
// SWAGGER
// ============================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SchemaFilter<Jarvis5.Filters.EaCreateRequestSchemaFilter>();
    options.OperationFilter<Jarvis5.Filters.WorkflowOperationFilter>();
    options.SchemaFilter<Jarvis5.Filters.WorkflowRequestSchemaFilter>();
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Studio5 Jarvis Master API",
        Version = "v1"
    });
    // Ensure EA controllers are tagged in the single v1 document so they remain visible
    options.TagActionsBy(apiDesc =>
    {
        // Prefer determining EA membership from the operation route (api/ea/...).
        var relativePath = apiDesc.RelativePath?.Trim('/') ?? string.Empty; // e.g. "api/ea/followups/{followupId}/cycles"

        if (!string.IsNullOrEmpty(relativePath) && relativePath.StartsWith("api/ea", StringComparison.OrdinalIgnoreCase))
        {
            // split path segments
            var segments = relativePath.Split('/');

            // segments[0] == "api", segments[1] == "ea", segments[2] == module
            string module = segments.Length > 2 ? segments[2] : string.Empty;

            // Meetings MUST keep exact names established in CHANGE 1
            if (string.Equals(module, "meetings", StringComparison.OrdinalIgnoreCase))
            {
                // If this is the KPI path, keep the exact KPI tag
                if (relativePath.StartsWith("api/ea/meetings/kpis", StringComparison.OrdinalIgnoreCase))
                    return new[] { "EA Meetings KPIs" };

                return new[] { "EA Meetings" };
            }

            // Follow-up cycles: routes like api/ea/followups/{followupId}/cycles
            if (module.Equals("followups", StringComparison.OrdinalIgnoreCase) &&
                relativePath.IndexOf("/cycles", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[] { "EA - Follow-up Cycles" };
            }

            // Common explicit mappings (preserve existing readable EA-prefixed groups)
            return module.ToLowerInvariant() switch
            {
                "intake" => new[] { "EA - Intake" },
                "workflows" => new[] { "EA - Workflow" },
                "followups" => new[] { "EA - Follow-ups" },
                "escalations" => new[] { "EA - Escalations" },
                "audit" => new[] { "EA - Audit" },
                "notifications" => new[] { "EA - Notifications" },
                "tat-rules" => new[] { "EA - TAT Rules" },
                _ => new[] { "EA - " + ToTitleCase(module) }
            };
        }

        // Non-EA: fall back to controller-based grouping for existing behavior
        var controllerFallback = apiDesc.ActionDescriptor?.RouteValues?[("controller")] ?? string.Empty;
        return controllerFallback switch
        {
            "Intake" => new[] { "EA - Intake" },
            "Workflow" => new[] { "EA - Workflow" },
            "Followups" => new[] { "EA - Follow-ups" },
            "Escalations" => new[] { "EA - Escalations" },
            "Audit" => new[] { "EA - Audit" },
            _ => new[] { controllerFallback ?? "default" }
        };

        static string ToTitleCase(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // replace hyphens with spaces, split camel case-ish tokens
            s = s.Replace('-', ' ');
            // Insert spaces between camelcase boundaries
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(c);
            }
            // Title-case the result
            var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + (parts[i].Length > 1 ? parts[i].Substring(1) : string.Empty);
            return string.Join(' ', parts);
        }
    });
});

// ============================================================
// DATABASE
// ============================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<Jarvis5.Data.AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<HrmsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<EaFmsDbContext>(options =>
    options.UseNpgsql(connectionString));

// EA services
builder.Services.AddScoped<Jarvis5.Services.EaFms.IApprovalDocumentService, Jarvis5.Services.EaFms.ApprovalDocumentService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IApprovalAuthorizationService, Jarvis5.Services.EaFms.ApprovalAuthorizationService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IApprovalLifecycleService, Jarvis5.Services.EaFms.ApprovalLifecycleService>();
// Ensure DI registrations for approval services exist (no-op change).

// ============================================================
// JWT AUTHENTICATION
// ============================================================

var jwt = builder.Configuration.GetSection("JwtSettings");

var jwtSecretKey = jwt["SecretKey"];

if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is missing from configuration.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecretKey)),

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ============================================================
// CORS
// ============================================================

const string frontendCorsPolicy = "FrontendCorsPolicy";

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? new[]
    {
        "http://localhost:4200",
        "http://localhost:8085",
        "https://studio5jarvis.studio5design.in"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        frontendCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// ============================================================
// AUTOMAPPER
// ============================================================

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(MappingProfile).Assembly);

// ============================================================
// HTTP CONTEXT
// ============================================================

builder.Services.AddHttpContextAccessor();

// ============================================================
// JARVIS REPOSITORIES
// ============================================================

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRequestHistoryRepository, RequestHistoryRepository>();
builder.Services.AddScoped<IAnalysisRepository, AnalysisRepository>();
builder.Services.AddScoped<ISolutionDesignRepository, SolutionDesignRepository>();
builder.Services.AddScoped<IApprovalRepository, ApprovalRepository>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ============================================================
// JARVIS SERVICES
// ============================================================

builder.Services.AddSingleton<
    IAttachmentFileService,
    AttachmentFileService>();

builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<ISimilarRequestFinder, SimilarRequestFinder>();

builder.Services.AddScoped<
    IAnalysisPromptBuilder,
    AnalysisPromptBuilder>();

builder.Services.AddScoped<
    IAnalysisReworkPromptBuilder,
    AnalysisReworkPromptBuilder>();

builder.Services.AddScoped<IAnalysisService, AnalysisService>();

builder.Services.AddScoped<
    ISolutionKnowledgeFinder,
    SolutionKnowledgeFinder>();

builder.Services.AddScoped<
    ISolutionDesignPromptBuilder,
    SolutionDesignPromptBuilder>();

builder.Services.AddScoped<
    ISolutionDesignService,
    SolutionDesignService>();

builder.Services.AddScoped<IApprovalService, ApprovalService>();

// ============================================================
// TASK SERVICES
// ============================================================

builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IStageMasterRepository, StageMasterRepository>();
builder.Services.AddScoped<ITaskHistoryRepository, TaskHistoryRepository>();

builder.Services.AddScoped<
    IDevelopmentPlanService,
    DevelopmentPlanService>();

builder.Services.AddScoped<
    IStageMasterService,
    StageMasterService>();

// ============================================================
// SNAG LIST
// ============================================================

builder.Services.AddScoped<ISnagListRepository, SnagListRepository>();
builder.Services.AddScoped<ISnagListService, SnagListService>();

// ============================================================
// ATTACHMENTS
// ============================================================

builder.Services.AddScoped<
    IAttachmentRepository,
    AttachmentRepository>();

builder.Services.AddSingleton<
    IAttachmentStorageService,
    AttachmentStorageService>();

builder.Services.AddScoped<
    IAttachmentService,
    AttachmentService>();

// EA FMS Intake services/repositories
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.ITatRuleRepository, Jarvis5.Repositories.EaFms.TatRuleRepository>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IBusinessModuleRepository, Jarvis5.Repositories.EaFms.BusinessModuleRepository>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IEaTaskRepository, Jarvis5.Repositories.EaFms.EaTaskRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.ITatRuleService, Jarvis5.Services.EaFms.TatRuleService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IBusinessModuleService, Jarvis5.Services.EaFms.BusinessModuleService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IEaTaskService, Jarvis5.Services.EaFms.EaTaskService>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IIntakeRepository, Jarvis5.Repositories.EaFms.IntakeRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IIntakeService, Jarvis5.Services.EaFms.IntakeService>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IWorkflowRepository, Jarvis5.Repositories.EaFms.WorkflowRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IWorkflowService, Jarvis5.Services.EaFms.WorkflowService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IWorkflowExecutionService, Jarvis5.Services.EaFms.WorkflowExecutionService>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IFollowupRepository, Jarvis5.Repositories.EaFms.FollowupRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IFollowupService, Jarvis5.Services.EaFms.FollowupService>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IFollowupCycleRepository, Jarvis5.Repositories.EaFms.FollowupCycleRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IFollowupCycleService, Jarvis5.Services.EaFms.FollowupCycleService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IFollowupSourceResolver, Jarvis5.Services.EaFms.FollowupSourceResolver>();
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IEscalationRepository, Jarvis5.Repositories.EaFms.EscalationRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IEscalationService, Jarvis5.Services.EaFms.EscalationService>();
// EA audit service
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IMeetingRepository, Jarvis5.Repositories.EaFms.MeetingRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IMeetingService, Jarvis5.Services.EaFms.MeetingService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IMeetingCompletionFileStore, Jarvis5.Services.EaFms.MeetingCompletionFileStore>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IMeetingLifecycleService, Jarvis5.Services.EaFms.MeetingLifecycleService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.IAuditService, Jarvis5.Services.EaFms.AuditService>();
// EA notifications
builder.Services.AddScoped<Jarvis5.Services.EaFms.INotificationService, Jarvis5.Services.EaFms.NotificationService>();
// Approval services
builder.Services.AddScoped<Jarvis5.Repositories.EaFms.IApprovalNumberRepository, Jarvis5.Repositories.EaFms.ApprovalRepository>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.ApprovalService>();
builder.Services.AddScoped<Jarvis5.Services.EaFms.ApprovalQueryService>();
// Ensure ITatRuleService is available for ApprovalService TAT resolution (already registered above)

// Travel services
builder.Services.AddScoped<Jarvis5.Services.EaFms.ITravelRequestService, Jarvis5.Services.EaFms.TravelRequestService>();

// ============================================================
// ANTHROPIC / CLAUDE
// ============================================================

builder.Services.Configure<ClaudeOptions>(
    builder.Configuration.GetSection("AnthropicSettings"));

builder.Services.AddSingleton<
    IClaudeClient,
    ClaudeClient>();

// ============================================================
// HRMS SERVICES
// ============================================================

builder.Services.AddScoped<
    hrms_api.Services.IEmailService,
    hrms_api.Services.EmailService>();

builder.Services.AddHttpClient<
    hrms_api.Services.IClaudeService,
    hrms_api.Services.ClaudeService>(
    client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
    });

builder.Services.AddScoped<
    hrms_api.Services.IPdfTextExtractionService,
    hrms_api.Services.PdfTextExtractionService>();

builder.Services.AddScoped<
    hrms_api.Services.IPdfToImageService,
    hrms_api.Services.PdfToImageService>();

builder.Services.AddScoped<
    hrms_api.Services.IImagePreprocessingService,
    hrms_api.Services.ImagePreprocessingService>();

builder.Services.AddScoped<
    hrms_api.Services.IOcrService,
    hrms_api.Services.OcrService>();

builder.Services.AddScoped<
    hrms_api.Services.IOcrDependencyHealthService,
    hrms_api.Services.OcrDependencyHealthService>();

builder.Services.AddHostedService<
    hrms_api.Services.OcrDependencyStartupValidator>();

builder.Services.AddScoped<
    hrms_api.Services.IDocumentExtractionService,
    hrms_api.Services.DocumentExtractionService>();

builder.Services.AddScoped<
    hrms_api.Services.IDocumentTextExtractionService,
    hrms_api.Services.DocumentTextExtractionService>();

builder.Services.AddScoped<
    hrms_api.Services.IAiScreeningPromptBuilder,
    hrms_api.Services.AiScreeningPromptBuilder>();

builder.Services.AddScoped<
    hrms_api.Services.ICandidateStatusService,
    hrms_api.Services.CandidateStatusService>();

builder.Services.AddScoped<
    hrms_api.Services.IActivityService,
    hrms_api.Services.ActivityService>();

builder.Services.AddScoped<
    hrms_api.Services.IAiScreeningService,
    hrms_api.Services.AiScreeningService>();

// ============================================================
// SCREENING BATCH
// ============================================================

builder.Services.AddSingleton<
    hrms_api.Services.IScreeningBatchQueue,
    hrms_api.Services.ScreeningBatchQueue>();

builder.Services.AddHostedService<
    hrms_api.Services.ScreeningBatchWorker>();

builder.Services.AddScoped<
    hrms_api.Services.IScreeningResultRepository,
    hrms_api.Services.ScreeningResultRepository>();

builder.Services.AddScoped<
    hrms_api.Services.IScreeningBatchService,
    hrms_api.Services.ScreeningBatchService>();

// ============================================================
// HR AI INTERVIEW
// ============================================================

builder.Services.AddScoped<
    hrms_api.Services.IHrAiInterviewQuestionService,
    hrms_api.Services.HrAiInterviewQuestionService>();

builder.Services.AddScoped<
    hrms_api.Services.IDirectorRoundInsightService,
    hrms_api.Services.DirectorRoundInsightService>();

builder.Services.AddScoped<
    hrms_api.Services.IDirectorAiInterviewQuestionService,
    hrms_api.Services.DirectorAiInterviewQuestionService>();

builder.Services.AddScoped<
    hrms_api.Services.IAssessmentEvaluationService,
    hrms_api.Services.AssessmentEvaluationService>();

// ============================================================
// ONBOARDING AI
// ============================================================

builder.Services.AddScoped<
    hrms_api.Services.IOnboardingAiService,
    hrms_api.Services.OnboardingAiService>();

builder.Services.AddScoped<
    hrms_api.Services.IOnboardingAssessmentService,
    hrms_api.Services.OnboardingAssessmentService>();

builder.Services.AddScoped<
    hrms_api.Services.IOnboardingFileService,
    hrms_api.Services.OnboardingFileService>();

builder.Services.AddScoped<
    hrms_api.Services.IOnboardingEvidenceProcessor,
    hrms_api.Services.OnboardingEvidenceProcessor>();

// Add registration for ApprovalLifecycleService
builder.Services.AddScoped<Jarvis5.Services.EaFms.IApprovalLifecycleService, Jarvis5.Services.EaFms.ApprovalLifecycleService>();

// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();

// ============================================================
// SWAGGER
// ============================================================

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Studio5 Jarvis Master API v1");
});

// ============================================================
// EXCEPTION MIDDLEWARE
//
// Keep the legacy HRMS exception middleware global and outermost so existing
// non-EA endpoints retain their ApiResponse-based error contracts. Scope the
// EA ProblemDetails exception middleware only to requests whose path begins
// with /api/ea using UseWhen. This ensures EA controllers see EA mappings
// first while preserving legacy behavior elsewhere.
// ============================================================

app.UseMiddleware<hrms_api.Middleware.ExceptionMiddleware>();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/ea"),
    branch =>
    {
        branch.UseMiddleware<ExceptionHandlingMiddleware>();
    });

// ============================================================
// HTTPS
// ============================================================

app.UseHttpsRedirection();

// ============================================================
// STATIC FILES
// ============================================================

app.UseStaticFiles();

// ============================================================
// CORS
// ============================================================

app.UseCors(frontendCorsPolicy);

// ============================================================
// AUTHENTICATION & AUTHORIZATION
// ============================================================

app.UseAuthentication();

app.UseAuthorization();

// ============================================================
// CONTROLLERS
// ============================================================

app.MapControllers();

// ============================================================
// CONTENT FOLDER
// ============================================================

var contentFolder = Path.Combine(
    app.Environment.ContentRootPath,
    "Content");

Directory.CreateDirectory(contentFolder);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(contentFolder),
        RequestPath = "/Content"
    });

// ============================================================
// RUN
// ============================================================

app.Run();
