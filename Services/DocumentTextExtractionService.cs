using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace hrms_api.Services;

public class PdfTextExtractionService : IPdfTextExtractionService
{
    private readonly ILogger<PdfTextExtractionService> _logger;

    public PdfTextExtractionService(ILogger<PdfTextExtractionService> logger)
    {
        _logger = logger;
    }

    public Task<DocumentExtractionResult> ExtractTextLayerAsync(
        string fullFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(fullFilePath))
            return Task.FromResult(DocumentExtractionResult.Fail($"PDF file missing: {Path.GetFileName(fullFilePath)}"));

        try
        {
            using var pdf = PdfDocument.Open(fullFilePath);
            var pageCount = pdf.NumberOfPages;
            var sb = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var words = page.GetWords();
                var line = string.Join(" ", words.Select(w => w.Text));
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine(line);
            }

            var cleaned = TextCleanup.Clean(sb.ToString());
            if (cleaned.Length < DocumentExtractionConstants.MinMeaningfulTextLength)
            {
                return Task.FromResult(DocumentExtractionResult.Fail(
                    "PDF text layer is empty or below minimum readable length. Treating document as ImageBasedPdf.",
                    DocumentExtractionConstants.MethodTextLayer,
                    isImageBasedPdf: true,
                    normalTextLength: cleaned.Length,
                    pdfPageCount: pageCount,
                    failureStep: DocumentExtractionConstants.FailureStepPdfTextExtraction));
            }

            _logger.LogInformation("PDF text-layer extraction succeeded for {FileName}. Length={Length}",
                Path.GetFileName(fullFilePath), cleaned.Length);

            return Task.FromResult(DocumentExtractionResult.Ok(
                cleaned,
                DocumentExtractionConstants.MethodTextLayer,
                isSelectableTextPdf: true,
                normalTextLength: cleaned.Length,
                pdfPageCount: pageCount));
        }
        catch (Exception ex) when (IsProtectedOrUnreadable(ex))
        {
            _logger.LogWarning(ex, "PDF text-layer extraction failed for {FileName}", Path.GetFileName(fullFilePath));
            return Task.FromResult(DocumentExtractionResult.Fail(
                "PDF could not be read. It may be password protected, corrupted, or unreadable.",
                DocumentExtractionConstants.MethodTextLayer,
                failureStep: DocumentExtractionConstants.FailureStepPdfTextExtraction));
        }
    }

    private static bool IsProtectedOrUnreadable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException ||
        ex.GetType().Name.Contains("Pdf", StringComparison.OrdinalIgnoreCase);
}

public class PdfToImageService : IPdfToImageService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfToImageService> _logger;

    public PdfToImageService(
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<PdfToImageService> logger)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    public async Task<PdfImageRenderResult> ConvertToImagesAsync(
        string fullFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("OcrSettings:Enabled", true))
            throw new InvalidOperationException("OCR is disabled in configuration.");

        if (!File.Exists(fullFilePath))
            throw new FileNotFoundException("PDF file missing.", fullFilePath);

        var maxFileSizeMb = Math.Clamp(_configuration.GetValue("OcrSettings:MaxFileSizeMb", 25), 1, 250);
        var fileSizeMb = new FileInfo(fullFilePath).Length / 1024m / 1024m;
        if (fileSizeMb > maxFileSizeMb)
            throw new InvalidOperationException($"PDF is too large for OCR ({fileSizeMb:F2} MB). Limit is {maxFileSizeMb} MB.");

        var pdfToPpmPath = _configuration["OcrSettings:PdfToPpmPath"] ?? "pdftoppm";
        var dpi = Math.Clamp(_configuration.GetValue("OcrSettings:Dpi", 300), 300, 600);
        var maxPages = Math.Clamp(_configuration.GetValue("OcrSettings:MaxPages", 100), 1, 500);
        var timeoutSeconds = Math.Clamp(_configuration.GetValue("OcrSettings:RenderTimeoutSeconds", 120), 10, 900);
        var tempRoot = ResolveTempRoot();
        Directory.CreateDirectory(tempRoot);
        var tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        var renderedDir = Path.Combine(tempDir, "rendered");
        Directory.CreateDirectory(renderedDir);

        var imagePrefix = Path.Combine(renderedDir, "page");
        var result = await ProcessRunner.RunAsync(
            pdfToPpmPath,
            ["-f", "1", "-l", maxPages.ToString(), "-r", dpi.ToString(), "-png", "-aa", "yes", "-aaVector", "yes", fullFilePath, imagePrefix],
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("PDF-to-image conversion failed for {FileName}: {Error}",
                Path.GetFileName(fullFilePath), result.Error);
            throw new InvalidOperationException(
                "PDF-to-image conversion failed. Install Poppler pdftoppm and configure OcrSettings:PdfToPpmPath.");
        }

        var images = Directory.GetFiles(renderedDir, "page-*.png")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (images.Count == 0)
        {
            throw new InvalidOperationException("PDF-to-image conversion produced no page images.");
        }

        return new PdfImageRenderResult(images, images.Count, images.Count > 0, images.FirstOrDefault());
    }

    private string ResolveTempRoot()
    {
        var configured = _configuration["OcrSettings:TempFolder"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var wwwRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        return Path.Combine(wwwRoot, "ocr-temp");
    }
}

public class ImagePreprocessingService : IImagePreprocessingService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagePreprocessingService> _logger;

    public ImagePreprocessingService(
        IConfiguration configuration,
        ILogger<ImagePreprocessingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OcrPageInput>> PreprocessAsync(
        IReadOnlyList<string> renderedImagePaths,
        CancellationToken cancellationToken = default)
    {
        if (renderedImagePaths.Count == 0)
            throw new InvalidOperationException("PDF rendered image was not created.");

        var imageMagickPath = _configuration["OcrSettings:ImageMagickPath"] ?? "magick";
        var scale = Math.Clamp(_configuration.GetValue("OcrSettings:PreprocessScalePercent", 200), 150, 300);
        var output = new List<OcrPageInput>();
        var preprocessDir = Path.Combine(Directory.GetParent(Directory.GetParent(renderedImagePaths[0])!.FullName)!.FullName, "preprocessed");
        Directory.CreateDirectory(preprocessDir);

        foreach (var imagePath in renderedImagePaths)
        {
            var preprocessedPath = Path.Combine(
                preprocessDir,
                Path.GetFileNameWithoutExtension(imagePath) + "-ocr.png");

            try
            {
                var result = await ProcessRunner.RunAsync(
                    imageMagickPath,
                    [
                        imagePath,
                        "-colorspace", "Gray",
                        "-auto-level",
                        "-contrast-stretch", "0x10%",
                        "-sharpen", "0x1",
                        "-despeckle",
                        "-resize", $"{scale}%",
                        "-threshold", "60%",
                        "-type", "Bilevel",
                        preprocessedPath
                    ],
                    TimeSpan.FromSeconds(Math.Clamp(_configuration.GetValue("OcrSettings:PreprocessTimeoutSeconds", 60), 10, 300)),
                    cancellationToken);

                if (result.ExitCode != 0 || !File.Exists(preprocessedPath))
                {
                    _logger.LogWarning(
                        "Image preprocessing failed for {Image}. Falling back to rendered image. Error={Error}",
                        imagePath,
                        result.Error);
                    output.Add(new OcrPageInput(imagePath, imagePath));
                    continue;
                }

                output.Add(new OcrPageInput(imagePath, preprocessedPath));
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Image preprocessing tool is unavailable for {Image}. Falling back to rendered image.",
                    imagePath);
                output.Add(new OcrPageInput(imagePath, imagePath));
            }
        }

        return output;
    }
}

public class OcrService : IOcrService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrService> _logger;

    public OcrService(IConfiguration configuration, ILogger<OcrService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractTextAsync(
        IReadOnlyList<OcrPageInput> pageImages,
        CancellationToken cancellationToken = default)
    {
        if (pageImages.Count == 0)
            return DocumentExtractionResult.Fail(
                "PDF rendered image was not created",
                DocumentExtractionConstants.MethodOcr,
                isImageBasedPdf: true,
                failureStep: DocumentExtractionConstants.FailureStepPdfToImage);

        var tesseractPath = _configuration["OcrSettings:TesseractPath"] ?? "tesseract";
        var tesseractDataPath = _configuration["OcrSettings:TesseractDataPath"];
        var language = _configuration["OcrSettings:Language"] ?? "eng";
        var pageSegmentationMode = _configuration["OcrSettings:PageSegmentationMode"] ?? "4";
        var engineMode = _configuration["OcrSettings:EngineMode"] ?? "1";
        var timeoutSeconds = Math.Clamp(_configuration.GetValue("OcrSettings:TimeoutSeconds", 120), 10, 1800);
        var perPageTimeout = TimeSpan.FromSeconds(Math.Max(10, timeoutSeconds / Math.Max(1, pageImages.Count)));
        var sb = new StringBuilder();

        foreach (var page in pageImages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var args = new List<string> { page.OcrImagePath, "stdout" };
            if (!string.IsNullOrWhiteSpace(tesseractDataPath))
            {
                args.Add("--tessdata-dir");
                args.Add(tesseractDataPath);
            }

            args.AddRange(["-l", language, "--oem", engineMode, "--psm", pageSegmentationMode]);

            var result = await ProcessRunner.RunAsync(
                tesseractPath,
                args,
                perPageTimeout,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("OCR failed for image {Image}: {Error}", page.OcrImagePath, result.Error);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(result.Output))
                sb.AppendLine(result.Output);
        }

        var cleaned = TextCleanup.Clean(sb.ToString());
        if (cleaned.Length == 0)
        {
            return DocumentExtractionResult.Fail(
                "OCR engine returned empty text",
                DocumentExtractionConstants.MethodOcr,
                isImageBasedPdf: true,
                renderedImageCreated: true,
                renderedImagePath: pageImages.FirstOrDefault()?.RenderedImagePath,
                ocrTextLength: 0,
                failureStep: DocumentExtractionConstants.FailureStepOcr);
        }

        if (cleaned.Length < DocumentExtractionConstants.MinMeaningfulTextLength)
        {
            return DocumentExtractionResult.Fail(
                "OCR text was below minimum readable length",
                DocumentExtractionConstants.MethodOcr,
                isImageBasedPdf: true,
                renderedImageCreated: true,
                renderedImagePath: pageImages.FirstOrDefault()?.RenderedImagePath,
                ocrTextLength: cleaned.Length,
                failureStep: DocumentExtractionConstants.FailureStepTextTooShort);
        }

        return DocumentExtractionResult.Ok(
            cleaned,
            DocumentExtractionConstants.MethodOcr,
            isImageBasedPdf: true,
            renderedImageCreated: true,
            renderedImagePath: pageImages.FirstOrDefault()?.RenderedImagePath,
            ocrTextLength: cleaned.Length);
    }
}

public class DocumentExtractionService : IDocumentExtractionService
{
    private readonly IPdfTextExtractionService _pdfTextExtractionService;
    private readonly IPdfToImageService _pdfToImageService;
    private readonly IImagePreprocessingService _imagePreprocessingService;
    private readonly IOcrService _ocrService;
    private readonly ILogger<DocumentExtractionService> _logger;

    public DocumentExtractionService(
        IPdfTextExtractionService pdfTextExtractionService,
        IPdfToImageService pdfToImageService,
        IImagePreprocessingService imagePreprocessingService,
        IOcrService ocrService,
        ILogger<DocumentExtractionService> logger)
    {
        _pdfTextExtractionService = pdfTextExtractionService;
        _pdfToImageService = pdfToImageService;
        _imagePreprocessingService = imagePreprocessingService;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractAsync(
        string fullFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(fullFilePath))
            return DocumentExtractionResult.Fail($"PDF file missing: {Path.GetFileName(fullFilePath)}");

        if (!string.Equals(Path.GetExtension(fullFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            return await ExtractNonPdfAsync(fullFilePath, cancellationToken);

        var textLayer = await _pdfTextExtractionService.ExtractTextLayerAsync(fullFilePath, cancellationToken);
        if (textLayer.Success)
            return textLayer;

        PdfImageRenderResult? renderResult = null;
        try
        {
            renderResult = await _pdfToImageService.ConvertToImagesAsync(fullFilePath, cancellationToken);
            if (!renderResult.RenderedImageCreated)
            {
                return DocumentExtractionResult.Fail(
                    "PDF rendered image was not created",
                    DocumentExtractionConstants.MethodFailed,
                    isImageBasedPdf: true,
                    normalTextLength: textLayer.NormalTextLength,
                    pdfPageCount: renderResult.PageCount,
                    failureStep: DocumentExtractionConstants.FailureStepPdfToImage);
            }
        }
        catch (OperationCanceledException)
        {
            return DocumentExtractionResult.Fail(
                "PDF-to-image conversion timed out or was cancelled",
                DocumentExtractionConstants.MethodFailed,
                isImageBasedPdf: true,
                normalTextLength: textLayer.NormalTextLength,
                pdfPageCount: textLayer.PdfPageCount,
                failureStep: DocumentExtractionConstants.FailureStepPdfToImage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF-to-image conversion failed for {FileName}", Path.GetFileName(fullFilePath));
            return DocumentExtractionResult.Fail(
                BuildPdfRenderFailureReason(ex),
                DocumentExtractionConstants.MethodFailed,
                isImageBasedPdf: true,
                normalTextLength: textLayer.NormalTextLength,
                pdfPageCount: textLayer.PdfPageCount,
                failureStep: DocumentExtractionConstants.FailureStepPdfToImage);
        }

        IReadOnlyList<OcrPageInput> ocrInputs;
        try
        {
            ocrInputs = await _imagePreprocessingService.PreprocessAsync(
                renderResult.RenderedImagePaths,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image preprocessing failed for {FileName}", Path.GetFileName(fullFilePath));
            return DocumentExtractionResult.Fail(
                "Image preprocessing failed",
                DocumentExtractionConstants.MethodFailed,
                isImageBasedPdf: true,
                normalTextLength: textLayer.NormalTextLength,
                pdfPageCount: renderResult.PageCount,
                renderedImageCreated: renderResult.RenderedImageCreated,
                renderedImagePath: renderResult.FirstRenderedImagePath,
                failureStep: DocumentExtractionConstants.FailureStepImagePreprocessing);
        }

        try
        {
            var ocr = await _ocrService.ExtractTextAsync(ocrInputs, cancellationToken);
            if (ocr.Success)
            {
                return ocr with
                {
                    NormalTextLength = textLayer.NormalTextLength,
                    PdfPageCount = renderResult.PageCount,
                    RenderedImageCreated = renderResult.RenderedImageCreated,
                    RenderedImagePath = renderResult.FirstRenderedImagePath,
                    IsImageBasedPdf = true,
                    ExtractionMethodUsed = DocumentExtractionConstants.MethodOcr
                };
            }

            return ocr with
            {
                ExtractionMethod = DocumentExtractionConstants.MethodFailed,
                ExtractionMethodUsed = DocumentExtractionConstants.MethodFailed,
                NormalTextLength = textLayer.NormalTextLength,
                PdfPageCount = renderResult.PageCount,
                RenderedImageCreated = renderResult.RenderedImageCreated,
                RenderedImagePath = renderResult.FirstRenderedImagePath,
                IsImageBasedPdf = true
            };
        }
        catch (OperationCanceledException)
        {
            return DocumentExtractionResult.Fail(
                "OCR timed out or was cancelled",
                DocumentExtractionConstants.MethodFailed,
                isImageBasedPdf: true,
                normalTextLength: textLayer.NormalTextLength,
                pdfPageCount: renderResult.PageCount,
                renderedImageCreated: renderResult.RenderedImageCreated,
                renderedImagePath: renderResult.FirstRenderedImagePath,
                failureStep: DocumentExtractionConstants.FailureStepOcr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed for {FileName}", Path.GetFileName(fullFilePath));
            return DocumentExtractionResult.Fail(
                "OCR engine returned empty text",
                DocumentExtractionConstants.MethodFailed,
                isImageBasedPdf: true,
                normalTextLength: textLayer.NormalTextLength,
                pdfPageCount: renderResult.PageCount,
                renderedImageCreated: renderResult.RenderedImageCreated,
                renderedImagePath: renderResult.FirstRenderedImagePath,
                failureStep: DocumentExtractionConstants.FailureStepOcr);
        }
        finally
        {
            var extractionFolder = renderResult.RenderedImagePaths.FirstOrDefault() is { } first
                ? Directory.GetParent(Directory.GetParent(first)!.FullName)?.FullName
                : null;

            if (!string.IsNullOrWhiteSpace(extractionFolder))
            {
                try { Directory.Delete(extractionFolder, true); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete OCR temp folder {TempDir}", extractionFolder); }
            }
        }
    }

    private static async Task<DocumentExtractionResult> ExtractNonPdfAsync(
        string fullFilePath,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(fullFilePath).ToLowerInvariant();
        var text = ext switch
        {
            ".txt" => await File.ReadAllTextAsync(fullFilePath, cancellationToken),
            ".docx" => ExtractDocx(fullFilePath),
            ".doc" => string.Empty,
            _ => string.Empty
        };

        var cleaned = TextCleanup.Clean(text);
        if (cleaned.Length < DocumentExtractionConstants.MinMeaningfulTextLength)
            return DocumentExtractionResult.Fail($"Document text is empty or too short for file type '{ext}'.");

        return DocumentExtractionResult.Ok(cleaned, DocumentExtractionConstants.MethodTextLayer);
    }

    private static string ExtractDocx(string filePath)
    {
        using var wordDoc = WordprocessingDocument.Open(filePath, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            if (!string.IsNullOrWhiteSpace(para.InnerText))
                sb.AppendLine(para.InnerText);
        }

        return sb.ToString();
    }

    private static string BuildPdfRenderFailureReason(Exception ex)
    {
        if (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
            return "PDF renderer missing. Please install Poppler and configure OcrSettings:PdfToPpmPath.";

        return "PDF rendered image was not created";
    }

}

public class DocumentTextExtractionService : IDocumentTextExtractionService
{
    private readonly IDocumentExtractionService _documentExtractionService;

    public DocumentTextExtractionService(IDocumentExtractionService documentExtractionService)
    {
        _documentExtractionService = documentExtractionService;
    }

    public Task<DocumentExtractionResult> ExtractTextAsync(string fullFilePath) =>
        _documentExtractionService.ExtractAsync(fullFilePath);
}

internal static class TextCleanup
{
    private static readonly Regex ControlChars = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);
    private static readonly Regex SameLineSpaces = new(@"[^\S\r\n]{2,}", RegexOptions.Compiled);
    private static readonly Regex LineEndings = new(@"\r\n|\r", RegexOptions.Compiled);
    private static readonly Regex BlankLines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex JunkRuns = new(@"[^\w\s.,;:()/%+\-@#&]{4,}", RegexOptions.Compiled);
    private static readonly Regex PageNumber = new(@"^(page\s*)?\d+\s*(of\s*\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CommonFooter = new(@"^(confidential|private and confidential|resume|curriculum vitae|cv|job description|jd)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = LineEndings.Replace(text, "\n");
        text = ControlChars.Replace(text, " ");
        text = JunkRuns.Replace(text, " ");

        var lines = text.Split('\n')
            .Select(line => SameLineSpaces.Replace(line.Replace('\t', ' ').Replace('\u00a0', ' '), " ").Trim())
            .Where(line => line.Length > 1 && !PageNumber.IsMatch(line) && !CommonFooter.IsMatch(line))
            .ToList();

        lines = RemoveRepeatedHeaderFooterLines(lines);
        return BlankLines.Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    private static List<string> RemoveRepeatedHeaderFooterLines(List<string> lines)
    {
        if (lines.Count < 8)
            return lines;

        var counts = lines
            .GroupBy(NormalizeSignature, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 2)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return lines
            .Where(line => !counts.TryGetValue(NormalizeSignature(line), out var count) || count < 3)
            .ToList();
    }

    private static string NormalizeSignature(string value)
    {
        var chars = value.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();
        return SameLineSpaces.Replace(new string(chars), " ").Trim();
    }
}

internal static class ProcessRunner
{
    public static async Task<ProcessOutput> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));

        if (completedTask != waitTask)
        {
            try { process.Kill(true); }
            catch { }

            return new ProcessOutput(-1, string.Empty, $"Process timed out after {timeout.TotalSeconds:N0} seconds.");
        }

        return new ProcessOutput(process.ExitCode, await outputTask, await errorTask);
    }
}

internal sealed record ProcessOutput(int ExitCode, string Output, string Error);
