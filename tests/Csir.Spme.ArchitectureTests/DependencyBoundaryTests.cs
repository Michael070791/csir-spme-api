using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace Csir.Spme.ArchitectureTests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Domain_Does_Not_Reference_EntityFramework_Or_Infrastructure()
    {
        var references = typeof(User).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        references.Should().NotContain(name =>
            name != null && (name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                name.StartsWith("Csir.Spme.Infrastructure", StringComparison.Ordinal)));
    }

    [Fact]
    public void Application_Does_Not_Reference_Infrastructure()
    {
        var references = typeof(ICommunicationOutbox).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        references.Should().NotContain("Csir.Spme.Infrastructure");
    }

    [Fact]
    public void Communication_Infrastructure_Has_No_MailKit_Dependency()
    {
        var references = typeof(ZeptoMailTransport).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        references.Should().NotContain("MailKit");
        references.Should().NotContain("MimeKit");
    }

    [Fact]
    public void Report_Endpoint_Handlers_Depend_On_Application_Service_Not_DbContext()
    {
        var endpointType = typeof(Csir.Spme.Api.Auth.AuthorizationPolicies).Assembly
            .GetType("Csir.Spme.Api.Endpoints.V2.ReportingEndpoints", throwOnError: true)!;
        var handlers = endpointType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.EndsWith("ReportAsync", StringComparison.Ordinal) ||
                method.Name == "GetReportsAsync")
            .ToList();

        handlers.Should().NotBeEmpty();
        handlers.Should().OnlyContain(method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ReportService)));
        handlers.SelectMany(method => method.GetParameters())
            .Should().NotContain(parameter => parameter.ParameterType == typeof(SpmeDbContext));
    }
}
