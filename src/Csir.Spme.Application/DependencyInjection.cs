using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Knowledge;
using Csir.Spme.Application.Leave;
using Csir.Spme.Application.Plan;
using Csir.Spme.Application.Projects;
using Csir.Spme.Application.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaginationOptions>(configuration.GetSection(PaginationOptions.SectionName));
        services.AddSingleton<ICursorCodec>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PaginationOptions>>().Value;
            var key = options.CursorSigningKey
                ?? configuration.GetSection("Jwt").GetValue<string>("Key")
                ?? throw new InvalidOperationException("Pagination:CursorSigningKey or Jwt:Key is required.");
            return new HmacCursorCodec(key);
        });

        services.AddScoped<ReportingPeriodService>();
        services.AddScoped<ReportService>();
        services.AddScoped<StaffQuarterlyReportService>();
        services.AddScoped<TechnologyService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<LeaveRequestService>();
        services.AddScoped<StrategicPlanService>();
        services.AddScoped<ThrustService>();
        services.AddScoped<OutputService>();
        services.AddScoped<IndicatorService>();
        services.AddScoped<IndicatorMeasurementService>();
        return services;
    }
}
