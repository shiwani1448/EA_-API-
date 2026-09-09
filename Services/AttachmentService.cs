using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Dtos.Attachment;
using Jarvis5.Entities;
using Jarvis5.Repositories;
using Microsoft.Extensions.Logging;

namespace Jarvis5.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IAttachmentRepository attachmentRepository,
        IAttachmentStorageService storageService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<AttachmentService> logger)
    {
        _attachmentRepository = attachmentRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<AttachmentResponseDto>> UploadAsync(UploadAttachmentRequestDto dto, CancellationToken ct = default)
    {
        var entityType = dto.EntityType.Trim().ToUpperInvariant();
        var now = Clock.UtcNow;
        var saved = new List<SCIHAttachment>();

        foreach (var file in dto.Files)
        {
            var (fileName, fileUrl, fileSize) = _storageService.Save(entityType, dto.EntityId, file.OriginalName, file.Base64Content);

            var attachment = new SCIHAttachment
            {
                EntityType = entityType,
                EntityId = dto.EntityId,
                FileName = fileName,
                OriginalName = file.OriginalName,
                FileUrl = fileUrl,
                ContentType = file.ContentType,
                FileSize = fileSize,
                UploadedBy = dto.UploadedBy,
                UploadedAt = now,
                IsDeleted = false,
                CreatedDate = now
            };

            await _attachmentRepository.AddAsync(attachment, ct);
            saved.Add(attachment);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Saved {Count} attachment(s) for {EntityType} {EntityId}.",
            saved.Count, entityType, dto.EntityId);

        return _mapper.Map<List<AttachmentResponseDto>>(saved);
    }

    public async Task<List<AttachmentResponseDto>> GetByEntityAsync(string entityType, long entityId, CancellationToken ct = default)
    {
        var attachments = await _attachmentRepository.GetByEntityAsync(entityType.Trim().ToUpperInvariant(), entityId, ct);
        return _mapper.Map<List<AttachmentResponseDto>>(attachments);
    }

    public async Task DeleteAsync(long attachmentId, CancellationToken ct = default)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, ct)
            ?? throw new NotFoundException($"Attachment {attachmentId} was not found.");

        attachment.IsDeleted = true;
        attachment.UpdatedDate = Clock.UtcNow;

        _attachmentRepository.Update(attachment);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Soft deleted attachment {AttachmentId}.", attachmentId);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long attachmentId, CancellationToken ct = default)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, ct)
            ?? throw new NotFoundException($"Attachment {attachmentId} was not found.");

        var physicalPath = _storageService.ResolvePhysicalPath(attachment.FileUrl);
        if (!File.Exists(physicalPath))
            throw new NotFoundException($"The file for attachment {attachmentId} could not be found on disk.");

        var content = await File.ReadAllBytesAsync(physicalPath, ct);
        var contentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType;

        return (content, contentType, attachment.OriginalName);
    }
}
