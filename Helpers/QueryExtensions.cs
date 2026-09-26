using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Helpers;

/// <summary>Limits queries to the sports a coach or evaluator is assigned to (null = no limit, e.g. Admin).</summary>
public static class QueryExtensions
{
    public static IQueryable<Applicant> ScopeTo(this IQueryable<Applicant> query, List<int>? sportIds) =>
        sportIds == null ? query : query.Where(a => sportIds.Contains(a.SportId));

    public static IQueryable<TryoutSchedule> ScopeTo(this IQueryable<TryoutSchedule> query, List<int>? sportIds) =>
        sportIds == null ? query : query.Where(t => sportIds.Contains(t.SportId));

    public static IQueryable<Sport> ScopeTo(this IQueryable<Sport> query, List<int>? sportIds) =>
        sportIds == null ? query : query.Where(s => sportIds.Contains(s.Id));
}
