using Csir.Spme.Domain.Hr;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class StaffProfileCompletion
{
    /// <summary>
    /// Bounded completion score for the authenticated staff profile summary.
    /// Counts core HR identity, contact, employment, and photo fields only.
    /// </summary>
    public static int Calculate(Employee employee, bool hasCurrentEmployment)
    {
        var profileFields = new[]
        {
            !string.IsNullOrWhiteSpace(employee.StaffId),
            !string.IsNullOrWhiteSpace(employee.Surname),
            !string.IsNullOrWhiteSpace(employee.OtherNames),
            employee.DateOfBirth.HasValue,
            !string.IsNullOrWhiteSpace(employee.Nationality),
            !string.IsNullOrWhiteSpace(employee.MaritalStatus),
            !string.IsNullOrWhiteSpace(employee.PrimaryEmail),
            !string.IsNullOrWhiteSpace(employee.Phone),
            hasCurrentEmployment,
            employee.ProfileImageFileId.HasValue
        };
        return (int)Math.Round(profileFields.Count(field => field) * 100m / profileFields.Length);
    }
}
