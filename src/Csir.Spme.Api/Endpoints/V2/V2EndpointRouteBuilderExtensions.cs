namespace Csir.Spme.Api.Endpoints.V2;

public static class V2EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapV2Endpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthEndpoints();
        endpoints.MapIamEndpoints();
        endpoints.MapDashboardEndpoints();
        endpoints.MapHrDashboardEndpoints();
        endpoints.MapHumanResourcesEndpoints();
        endpoints.MapInstituteEndpoints();
        endpoints.MapPlanningEndpoints();
        endpoints.MapProjectEndpoints();
        endpoints.MapReportingPeriodEndpoints();
        endpoints.MapReportingEndpoints();
        endpoints.MapStaffQuarterlyReportEndpoints();
        endpoints.MapLeaveEndpoints();
        endpoints.MapHolidayEndpoints();
        endpoints.MapHolidayPeriodEndpoints();
        endpoints.MapSkeletalStaffEndpoints();
        endpoints.MapKnowledgeEndpoints();
        endpoints.MapCommunicationEndpoints();
        endpoints.MapPromotionEndpoints();
        endpoints.MapPromotionSubmissionEndpoints();
        endpoints.MapPromotionReportEndpoints();
        return endpoints;
    }
}
