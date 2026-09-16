using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public sealed class BusinessModuleService(EaFmsDbContext db, IBusinessModuleRepository repository,
    ICurrentUserService user, IAuditService audit) : IBusinessModuleService
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase) { "Meeting", "EA Approval" };

    public async Task<IReadOnlyList<BusinessModuleDto>> GetAllAsync(bool activeOnly, CancellationToken ct) =>
        (await repository.Query(activeOnly).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<BusinessModuleDto> GetByIdAsync(long id, CancellationToken ct) =>
        ToDto(await repository.Query().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Business module {id} not found."));

    public async Task<BusinessModuleDto> CreateAsync(SaveBusinessModuleDto dto, CancellationToken ct)
    {
        var (name, description) = ValidateAndNormalize(dto);
        if (await repository.ExistsNormalizedNameAsync(name.ToLowerInvariant(), null, ct))
            throw new BusinessRuleException("A business module with this name already exists.");

        var actor = Actor();
        var module = new BusinessModule { Name = name, Description = description, IsActive = dto.IsActive, IsDeleted = false, CreatedBy = actor, CreatedDate = Clock.UtcNowTz };
        await repository.AddAsync(module, ct);
        await db.SaveChangesAsync(ct);
        audit.AddAudit("BUSINESS_MODULE_CREATE", "BusinessModule", nameof(BusinessModule), module.Id.ToString(), null, new { module.Name, module.Description, module.IsActive });
        await db.SaveChangesAsync(ct);
        return ToDto(module);
    }

    public async Task<BusinessModuleDto> UpdateAsync(long id, SaveBusinessModuleDto dto, CancellationToken ct)
    {
        var module = await repository.GetForUpdateAsync(id, ct) ?? throw new NotFoundException($"Business module {id} not found.");
        var (name, description) = ValidateAndNormalize(dto);
        if (ProtectedNames.Contains(module.Name.Trim()) && !string.Equals(module.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("This protected system module cannot be renamed.");
        if (await repository.ExistsNormalizedNameAsync(name.ToLowerInvariant(), id, ct))
            throw new BusinessRuleException("A business module with this name already exists.");

        var old = new { module.Name, module.Description, module.IsActive };
        var nameChanged = !string.Equals(module.Name, name, StringComparison.Ordinal);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        module.Name = name; module.Description = description; module.IsActive = dto.IsActive;
        module.ModifiedBy = Actor(); module.ModifiedDate = Clock.UtcNowTz;
        if (nameChanged)
        {
            var rules = await db.TatRules.Where(x => x.BusinessModuleId == module.Id && !x.IsDeleted).ToListAsync(ct);
            foreach (var rule in rules) rule.ModuleName = name;
        }
        await db.SaveChangesAsync(ct);
        audit.AddAudit("BUSINESS_MODULE_UPDATE", "BusinessModule", nameof(BusinessModule), module.Id.ToString(), old, new { module.Name, module.Description, module.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(module);
    }

    private static (string Name, string? Description) ValidateAndNormalize(SaveBusinessModuleDto dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BadRequestException("Name is required.");
        if (name.Length > 200) throw new BadRequestException("Name must not exceed 200 characters.");
        var description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (description?.Length > 2000) throw new BadRequestException("Description must not exceed 2000 characters.");
        return (name, description);
    }

    private string Actor() => user.UserName ?? (user.UserId == 0 ? "system" : user.UserId.ToString());
    private static BusinessModuleDto ToDto(BusinessModule module) => new() { Id = module.Id, Name = module.Name, Description = module.Description, IsActive = module.IsActive };
}
