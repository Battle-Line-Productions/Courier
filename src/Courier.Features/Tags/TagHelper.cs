using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Tags;

public static class TagHelper
{
    public static async Task<List<TagSummaryDto>> GetTagsForEntityAsync(
        CourierDbContext db, string entityType, Guid entityId, CancellationToken ct)
    {
        return await db.EntityTags
            .Where(et => et.EntityType == entityType && et.EntityId == entityId && !et.Tag.IsDeleted)
            .Select(et => new TagSummaryDto { Name = et.Tag.Name, Color = et.Tag.Color })
            .ToListAsync(ct);
    }

    public static async Task<Dictionary<Guid, List<TagSummaryDto>>> GetTagsForEntitiesAsync(
        CourierDbContext db, string entityType, IEnumerable<Guid> entityIds, CancellationToken ct)
    {
        var idList = entityIds.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, List<TagSummaryDto>>();

        var tags = await db.EntityTags
            .Where(et => et.EntityType == entityType && idList.Contains(et.EntityId) && !et.Tag.IsDeleted)
            .Select(et => new { et.EntityId, Tag = new TagSummaryDto { Name = et.Tag.Name, Color = et.Tag.Color } })
            .ToListAsync(ct);

        return tags
            .GroupBy(t => t.EntityId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Tag).ToList());
    }
}
