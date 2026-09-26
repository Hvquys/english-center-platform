using System.Security.Cryptography;
using System.Text;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Modules.Courses;
using Microsoft.Extensions.Options;

namespace EnglishCenter.Api.Modules.Caching;

public interface ICourseCache
{
    Task<CacheReadResult<CourseResponse>> GetByIdAsync(long courseId, CancellationToken cancellationToken);
    Task SetByIdAsync(CourseResponse course, CancellationToken cancellationToken);
    Task<CacheReadResult<PagedResponse<CourseResponse>>> GetListAsync(CourseListQuery query, CancellationToken cancellationToken);
    Task SetListAsync(CourseListQuery query, PagedResponse<CourseResponse> response, CancellationToken cancellationToken);
    Task InvalidateAsync(long courseId, CancellationToken cancellationToken);
    Task InvalidateListsAsync(CancellationToken cancellationToken);
}

internal sealed class CourseCache(
    IApplicationCache cache,
    IOptions<RedisCacheOptions> options) : ICourseCache
{
    private const string ListVersionKey = "v1:courses:list:version";
    private static readonly TimeSpan ListVersionTtl = TimeSpan.FromDays(30);
    private readonly TimeSpan detailTtl = TimeSpan.FromSeconds(options.Value.DefaultTtlSeconds);
    private readonly TimeSpan listTtl = TimeSpan.FromSeconds(options.Value.CourseListTtlSeconds);

    public Task<CacheReadResult<CourseResponse>> GetByIdAsync(long courseId, CancellationToken cancellationToken) =>
        cache.GetAsync<CourseResponse>(DetailKey(courseId), cancellationToken);

    public Task SetByIdAsync(CourseResponse course, CancellationToken cancellationToken) =>
        cache.SetAsync(DetailKey(course.CourseId), course, detailTtl, cancellationToken);

    public async Task<CacheReadResult<PagedResponse<CourseResponse>>> GetListAsync(
        CourseListQuery query,
        CancellationToken cancellationToken)
    {
        var version = await GetListVersionAsync(cancellationToken);
        return await cache.GetAsync<PagedResponse<CourseResponse>>(ListKey(version, query), cancellationToken);
    }

    public async Task SetListAsync(
        CourseListQuery query,
        PagedResponse<CourseResponse> response,
        CancellationToken cancellationToken)
    {
        var version = await GetListVersionAsync(cancellationToken);
        await cache.SetAsync(ListKey(version, query), response, listTtl, cancellationToken);
    }

    public async Task InvalidateAsync(long courseId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(DetailKey(courseId), cancellationToken);
        await InvalidateListsAsync(cancellationToken);
    }

    public async Task InvalidateListsAsync(CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(ListVersionKey, cancellationToken);
        await cache.SetAsync(ListVersionKey, NewVersion(), ListVersionTtl, cancellationToken);
    }

    private async Task<string> GetListVersionAsync(CancellationToken cancellationToken)
    {
        var result = await cache.GetAsync<string>(ListVersionKey, cancellationToken);
        if (result.Found && !string.IsNullOrWhiteSpace(result.Value)) return result.Value;

        var version = NewVersion();
        await cache.SetAsync(ListVersionKey, version, ListVersionTtl, cancellationToken);
        return version;
    }

    private static string DetailKey(long courseId) => $"v1:courses:detail:{courseId}";

    private static string ListKey(string version, CourseListQuery query)
    {
        var normalized = $"{query.Page}|{query.PageSize}|{query.Search?.Trim().ToUpperInvariant()}|{query.Status}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"v1:courses:list:{version}:{hash}";
    }

    private static string NewVersion() => Guid.NewGuid().ToString("N");
}
