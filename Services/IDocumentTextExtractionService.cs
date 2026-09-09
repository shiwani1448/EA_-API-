namespace hrms_api.Services;

public static class DocumentExtractionConstants
{
    public const int MinMeaningfulTextLength = 100;
    public const string StatusSuccess = "Success";
    public const string StatusFailed = "Failed";
    public const string MethodTextLayer = "TextLayer";
    public const string MethodOcr = "OCR";
    public const string MethodFailed = "Failed";
    public const string FailureStepPdfTextExtraction = "PdfTextExtraction";
    public const string FailureStepPdfToImage = "PdfToImage";
    public const string FailureStepImagePreprocessing = "ImagePreprocessing";
    public const string FailureStepOcr = "OCR";
    public const string FailureStepTextTooShort = "TextTooShort";
}

public record DocumentExtractionResult(
    bool Success,
    string? Text,
    string ExtractionStatus,
    string ExtractionMethod,
    string? FailureReason,
    bool TextAvailable,
    bool IsSelectableTextPdf = false,
    bool IsImageBasedPdf = false,
    int NormalTextLength = 0,
    int PdfPageCount = 0,
    bool RenderedImageCreated = false,
    string? RenderedImagePath = null,
    int OCRTextLength = 0,
    string? ExtractionMethodUsed = null,
    string? FailureStep = null)
{
    public string? ErrorMessage => FailureReason;
    public bool RequiresOcr => ExtractionMethod == DocumentExtractionConstants.MethodOcr && !Success;

    public static DocumentExtractionResult Ok(
        string text,
        string method,
        bool isSelectableTextPdf = false,
        bool isImageBasedPdf = false,
        int normalTextLength = 0,
        int pdfPageCount = 0,
        bool renderedImageCreated = false,
        string? renderedImagePath = null,
        int ocrTextLength = 0) =>
        new(
            true,
            text,
            DocumentExtractionConstants.StatusSuccess,
            method,
            null,
            true,
            isSelectableTextPdf,
            isImageBasedPdf,
            normalTextLength,
            pdfPageCount,
            renderedImageCreated,
            renderedImagePath,
            ocrTextLength,
            method);

    public static DocumentExtractionResult Fail(
        string reason,
        string method = DocumentExtractionConstants.MethodFailed,
        bool isImageBasedPdf = false,
        int normalTextLength = 0,
        int pdfPageCount = 0,
        bool renderedImageCreated = false,
        string? renderedImagePath = null,
        int ocrTextLength = 0,
        string? failureStep = null) =>
        new(
            false,
            null,
            DocumentExtractionConstants.StatusFailed,
            method,
            reason,
            false,
            false,
            isImageBasedPdf,
            normalTextLength,
            pdfPageCount,
            renderedImageCreated,
            renderedImagePath,
            ocrTextLength,
            method,
            failureStep);
}

public sealed record PdfImageRenderResult(
    IReadOnlyList<string> RenderedImagePaths,
    int PageCount,
    bool RenderedImageCreated,
    string? FirstRenderedImagePath);

public sealed record OcrPageInput(string RenderedImagePath, string OcrImagePath);

public interface IPdfTextExtractionService
{
    Task<DocumentExtractionResult> ExtractTextLayerAsync(string fullFilePath, CancellationToken cancellationToken = default);
}

public interface IPdfToImageService
{
    Task<PdfImageRenderResult> ConvertToImagesAsync(string fullFilePath, CancellationToken cancellationToken = default);
}

public interface IImagePreprocessingService
{
    Task<IReadOnlyList<OcrPageInput>> PreprocessAsync(
        IReadOnlyList<string> renderedImagePaths,
        CancellationToken cancellationToken = default);
}

public interface IOcrService
{
    Task<DocumentExtractionResult> ExtractTextAsync(
        IReadOnlyList<OcrPageInput> pageImages,
        CancellationToken cancellationToken = default);
}

public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtractAsync(string fullFilePath, CancellationToken cancellationToken = default);
}

public interface IDocumentTextExtractionService
{
    Task<DocumentExtractionResult> ExtractTextAsync(string fullFilePath);
}
