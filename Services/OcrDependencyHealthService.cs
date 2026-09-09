using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IOcrDependencyHealthService
{
    Task<DocumentExtractionHealthDto> CheckAsync(CancellationToken cancellationToken = default);
}

public class OcrDependencyHealthService : IOcrDependencyHealthService
{
    private readonly IConfiguration _configuration;

    public OcrDependencyHealthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<DocumentExtractionHealthDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var pdfToPpmPath = _configuration["OcrSettings:PdfToPpmPath"] ?? "pdftoppm";
        var tesseractPath = _configuration["OcrSettings:TesseractPath"] ?? "tesseract";
        var tesseractDataPath = _configuration["OcrSettings:TesseractDataPath"] ?? string.Empty;
        var tempFolder = ResolveTempFolder();

        var tempFolderWritable = EnsureWritable(tempFolder);

        return new DocumentExtractionHealthDto
        {
            PopplerAvailable = await IsToolAvailableAsync(pdfToPpmPath, ["-v"], cancellationToken),
            PdfToPpmPath = pdfToPpmPath,
            TesseractAvailable = await IsTesseractAvailableAsync(tesseractPath, tesseractDataPath, cancellationToken),
            TesseractDataPath = tesseractDataPath,
            TempFolderWritable = tempFolderWritable
        };
    }

    private async Task<bool> IsTesseractAvailableAsync(
        string tesseractPath,
        string tesseractDataPath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tesseractDataPath) && !Directory.Exists(tesseractDataPath))
            return false;

        return await IsToolAvailableAsync(tesseractPath, ["--version"], cancellationToken);
    }

    private static async Task<bool> IsToolAvailableAsync(
        string toolPath,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        if (LooksLikePath(toolPath) && !File.Exists(toolPath))
            return false;

        try
        {
            var result = await ProcessRunner.RunAsync(
                toolPath,
                args,
                TimeSpan.FromSeconds(10),
                cancellationToken);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string ResolveTempFolder()
    {
        var configured = _configuration["OcrSettings:TempFolder"];
        return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ocr-temp")
            : configured);
    }

    private static bool EnsureWritable(string tempFolder)
    {
        try
        {
            Directory.CreateDirectory(tempFolder);
            var testFile = Path.Combine(tempFolder, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikePath(string value) =>
        value.Contains(Path.DirectorySeparatorChar) ||
        value.Contains(Path.AltDirectorySeparatorChar) ||
        Path.IsPathRooted(value);
}

public class OcrDependencyStartupValidator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcrDependencyStartupValidator> _logger;
    private readonly IHostEnvironment _environment;

    public OcrDependencyStartupValidator(
        IServiceScopeFactory scopeFactory,
        ILogger<OcrDependencyStartupValidator> logger,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IOcrDependencyHealthService>();
        var health = await healthService.CheckAsync(cancellationToken);

        var missing = new List<string>();
        if (!health.PopplerAvailable)
            missing.Add($"Poppler is missing at '{health.PdfToPpmPath}'.");
        if (!health.TesseractAvailable)
            missing.Add($"Tesseract or tessdata is unavailable at '{health.TesseractDataPath}'.");
        if (!health.TempFolderWritable)
            missing.Add("The OCR temp folder is not writable.");

        if (missing.Count == 0)
            return;

        var message = "OCR dependency validation failed: " + string.Join(" ", missing);
        if (_environment.IsDevelopment())
        {
            _logger.LogWarning("{Message} OCR endpoints may fail until dependencies are configured; application startup will continue in Development.", message);
            return;
        }

        _logger.LogError("{Message}", message);
        throw new InvalidOperationException(message);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
