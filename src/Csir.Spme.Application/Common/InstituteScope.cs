using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Application.Common;

/// <summary>
/// Effective institute scope for the caller. Institute users are always scoped to their claim;
/// CSIR-wide callers (no claim) may supply an institute filter.
/// </summary>
public sealed class InstituteScope
{
    private InstituteScope(Guid? scopedInstituteId, Guid? effectiveFilter)
    {
        ScopedInstituteId = scopedInstituteId;
        EffectiveFilter = effectiveFilter;
    }

    /// <summary>The caller's institute claim; null for CSIR-wide callers.</summary>
    public Guid? ScopedInstituteId { get; }

    /// <summary>The institute filter to apply to list queries.</summary>
    public Guid? EffectiveFilter { get; }

    public static Result<InstituteScope> Resolve(Guid? scopedInstituteId, Guid? requestedFilter)
    {
        if (scopedInstituteId.HasValue && requestedFilter.HasValue && scopedInstituteId.Value != requestedFilter.Value)
        {
            return Result<InstituteScope>.Failure(Error.CrossInstitute(
                "You are not authorized to access resources for that institute."));
        }

        return Result<InstituteScope>.Success(
            new InstituteScope(scopedInstituteId, scopedInstituteId ?? requestedFilter));
    }

    /// <summary>True when the caller may access a resource owned by <paramref name="instituteId"/>.</summary>
    public bool CanAccess(Guid instituteId) =>
        !ScopedInstituteId.HasValue || ScopedInstituteId.Value == instituteId;
}
