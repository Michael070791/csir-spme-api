using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public class DocumentationEndpointTests : IClassFixture<SpmeApiFactory>
{
    private static readonly HashSet<string> OperationMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "get",
            "put",
            "post",
            "delete",
            "options",
            "head",
            "patch",
            "trace"
        };

    private readonly HttpClient _client;

    public DocumentationEndpointTests(SpmeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_V2_Document_Lists_Employee_Dependant_Endpoints()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/spouse", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/employees/{employeeId}/spouse/{spouseId}", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/employees/{employeeId}/children", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/employees/{employeeId}/children/{childId}", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_V2_Document_Lists_System_User_Primary_And_Compatibility_Routes()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v2/system-users", out var systemUsersPath).Should().BeTrue();
        systemUsersPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("System Users");
        paths.TryGetProperty("/api/v2/system-users/{id}", out var systemUserDetailPath).Should().BeTrue();
        systemUserDetailPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("System Users");

        paths.TryGetProperty("/api/v2/users", out var usersPath).Should().BeTrue();
        usersPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Identity and Access");
        paths.TryGetProperty("/api/v2/users/{id}", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_V2_Document_Lists_Strategic_Planning_Resources()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v2/strategic-plans", out var strategicPlans).Should().BeTrue();
        strategicPlans.TryGetProperty("get", out _).Should().BeTrue();
        strategicPlans.TryGetProperty("post", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/strategic-plans/{id}", out var strategicPlan).Should().BeTrue();
        strategicPlan.TryGetProperty("get", out _).Should().BeTrue();
        strategicPlan.TryGetProperty("patch", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/strategic-plans/{id}/activate", out _).Should().BeTrue();

        paths.TryGetProperty("/api/v2/thrusts", out var thrustsListPath).Should().BeTrue();
        thrustsListPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Thrusts");
        thrustsListPath.GetProperty("get").GetProperty("parameters")
            .EnumerateArray()
            .Should().Contain(parameter =>
                parameter.GetProperty("name").GetString() == "strategicPlanId" &&
                parameter.GetProperty("in").GetString() == "query");
        thrustsListPath.GetProperty("get").GetProperty("responses")
            .TryGetProperty("404", out _).Should().BeFalse();
        paths.TryGetProperty("/api/v2/strategic-plans/{planId}/thrusts", out var thrustsCreatePath).Should().BeTrue();
        thrustsCreatePath.TryGetProperty("get", out _).Should().BeTrue();
        thrustsCreatePath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Thrusts");

        paths.TryGetProperty("/api/v2/thrusts/{thrustId}/outputs", out var outputsPath).Should().BeTrue();
        outputsPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Outputs");
        outputsPath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Outputs");

        paths.TryGetProperty("/api/v2/outputs/{outputId}/indicators", out var indicatorsPath).Should().BeTrue();
        indicatorsPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Indicators");
        indicatorsPath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Indicators");
        paths.TryGetProperty("/api/v2/thrusts/{thrustId}/indicators", out var thrustIndicatorsPath)
            .Should().BeTrue();
        thrustIndicatorsPath.GetProperty("get").GetProperty("operationId").GetString()
            .Should().Be("Indicators_ListByThrust");
        thrustIndicatorsPath.TryGetProperty("post", out _).Should().BeFalse();
        thrustIndicatorsPath.TryGetProperty("patch", out _).Should().BeFalse();
        thrustIndicatorsPath.TryGetProperty("delete", out _).Should().BeFalse();

        paths.TryGetProperty("/api/v2/indicators/{indicatorId}/measurements", out var indicatorDataPath).Should().BeTrue();
        indicatorDataPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Indicator Measurements");
        indicatorDataPath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Indicator Measurements");
        indicatorDataPath.GetProperty("get").GetProperty("operationId").GetString()
            .Should().Be("IndicatorMeasurements_List");
        indicatorDataPath.GetProperty("post").GetProperty("operationId").GetString()
            .Should().Be("IndicatorMeasurements_Create");

        paths.TryGetProperty("/api/v2/indicator-measurements/{id}", out var indicatorMeasurementPath).Should().BeTrue();
        indicatorMeasurementPath.GetProperty("get").GetProperty("operationId").GetString()
            .Should().Be("IndicatorMeasurements_Get");
        indicatorMeasurementPath.GetProperty("patch").GetProperty("operationId").GetString()
            .Should().Be("IndicatorMeasurements_Update");
        indicatorMeasurementPath.GetProperty("delete").GetProperty("tags")[0].GetString().Should().Be("Indicator Measurements");
        indicatorMeasurementPath.GetProperty("delete").GetProperty("operationId").GetString()
            .Should().Be("IndicatorMeasurements_Delete");
    }

    [Fact]
    public async Task OpenApi_V2_Document_Groups_Organization_Structure_Under_Institutes()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        AssertDocumentedOperation(paths.GetProperty("/api/v2/institutes").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/institutes/{id}").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/institutes/{id}/divisions").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/divisions").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/divisions").GetProperty("post"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/divisions/{id}").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/divisions/{id}").GetProperty("patch"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/divisions/{id}/sections").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/sections").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/sections").GetProperty("post"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/sections/{id}").GetProperty("get"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/sections/{id}").GetProperty("patch"), "Institutes");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/grades").GetProperty("get"), "Human Resources");
    }

    [Fact]
    public async Task OpenApi_V2_Document_Describes_Every_V2_Operation()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject().Where(path => path.Name.StartsWith("/api/v2", StringComparison.Ordinal)))
        {
            foreach (var operationProperty in path.Value.EnumerateObject()
                         .Where(property => OperationMethods.Contains(property.Name)))
            {
                var operation = operationProperty.Value;
                operation.TryGetProperty("summary", out var summaryElement)
                    .Should().BeTrue($"because {operationProperty.Name.ToUpperInvariant()} {path.Name} must have a summary");
                summaryElement.GetString().Should().NotBeNullOrWhiteSpace(
                    $"because {operationProperty.Name.ToUpperInvariant()} {path.Name} must have a summary");

                operation.TryGetProperty("description", out var descriptionElement)
                    .Should().BeTrue($"because {operationProperty.Name.ToUpperInvariant()} {path.Name} must have a description");
                var description = descriptionElement.GetString();
                description.Should().NotBeNullOrWhiteSpace(
                    $"because {operationProperty.Name.ToUpperInvariant()} {path.Name} must have a description");
                description!.Length.Should().BeGreaterThan(
                    80,
                    $"because {operationProperty.Name.ToUpperInvariant()} {path.Name} must explain its behavior");
                description.Should().NotContain(
                    "/api/v2/",
                    $"because {operationProperty.Name.ToUpperInvariant()} {path.Name} descriptions must remain base-URL agnostic");
            }
        }
    }

    [Fact]
    public async Task OpenApi_V2_Document_Uses_Requirement_Response_Envelopes()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        AssertResponseSchema(paths, "/api/v2/reports", "get", "200", "ListResponseOfReportResponse");
        AssertResponseSchema(paths, "/api/v2/reports/{id}", "get", "200", "DataResponseOfReportResponse");
        AssertResponseSchema(paths, "/api/v2/reporting-periods", "get", "200", "ListResponseOfReportingPeriodResponse");
        AssertResponseSchema(paths, "/api/v2/reporting-periods/{id}", "get", "200", "DataResponseOfReportingPeriodResponse");
        AssertResponseSchema(paths, "/api/v2/reporting-periods/{id}/open", "post", "200", "DataResponseOfReportingPeriodResponse");
        AssertResponseSchema(paths, "/api/v2/reporting-periods/{id}/close", "post", "200", "DataResponseOfReportingPeriodResponse");
        AssertResponseSchema(paths, "/api/v2/reporting-periods/{id}/finalize", "post", "200", "DataResponseOfReportingPeriodResponse");
        AssertResponseSchema(paths, "/api/v2/technologies", "get", "200", "ListResponseOfTechnologyResponse");
        AssertResponseSchema(paths, "/api/v2/technologies/{id}", "get", "200", "DataResponseOfTechnologyResponse");
        AssertResponseSchema(paths, "/api/v2/projects", "get", "200", "ListResponseOfProjectResponse");
        AssertResponseSchema(paths, "/api/v2/projects/{id}", "get", "200", "DataResponseOfProjectResponse");
        AssertResponseSchema(paths, "/api/v2/leave-types", "get", "200", "ListResponseOfLeaveTypeMetadataResponse");
        AssertResponseSchema(paths, "/api/v2/leave-types/{code}", "get", "200", "DataResponseOfLeaveTypeMetadataResponse");
        AssertResponseSchema(paths, "/api/v2/leave-requests", "get", "200", "ListResponseOfLeaveRequestDto");
        AssertResponseSchema(paths, "/api/v2/leave-requests/{id}", "get", "200", "DataResponseOfLeaveRequestDto");
        AssertResponseSchema(paths, "/api/v2/thrusts", "get", "200", "ListResponseOfThrustResponse");
        AssertResponseSchema(paths, "/api/v2/thrusts/{id}", "get", "200", "DataResponseOfThrustResponse");
        AssertResponseSchema(paths, "/api/v2/outputs", "get", "200", "ListResponseOfOutputResponse");
        AssertResponseSchema(paths, "/api/v2/outputs/{id}", "get", "200", "DataResponseOfOutputResponse");
        AssertResponseSchema(paths, "/api/v2/outputs/{outputId}/indicators", "get", "200", "ListResponseOfIndicatorResponse");
        AssertResponseSchema(paths, "/api/v2/indicators/{id}", "get", "200", "DataResponseOfIndicatorResponse");
        AssertResponseSchema(paths, "/api/v2/indicators/{indicatorId}/measurements", "get", "200", "ListResponseOfIndicatorDataResponse");
        AssertResponseSchema(paths, "/api/v2/indicator-measurements/{id}", "get", "200", "DataResponseOfIndicatorDataResponse");
        AssertResponseSchema(paths, "/api/v2/strategic-plans", "get", "200", "ListResponseOfStrategicPlanResponse");
        AssertResponseSchema(paths, "/api/v2/strategic-plans/{id}", "get", "200", "DataResponseOfStrategicPlanResponse");
        AssertResponseSchema(paths, "/api/v2/strategic-plans/{id}/activate", "post", "200", "DataResponseOfStrategicPlanResponse");
    }

    [Fact]
    public async Task OpenApi_V2_Document_Lists_Resource_Crud_With_Professional_Descriptions()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        AssertDocumentedCrud(paths, "/api/v2/reports", "/api/v2/reports/{id}", "Reports");
        paths.TryGetProperty("/api/v2/reporting-periods", out var reportingPeriodsPath).Should().BeTrue();
        AssertDocumentedOperation(reportingPeriodsPath.GetProperty("get"), "Reporting Periods");
        AssertDocumentedOperation(reportingPeriodsPath.GetProperty("post"), "Reporting Periods");
        paths.TryGetProperty("/api/v2/reporting-periods/{id}", out var reportingPeriodPath).Should().BeTrue();
        AssertDocumentedOperation(reportingPeriodPath.GetProperty("get"), "Reporting Periods");
        AssertReportingPeriodCommand(
            paths, "/api/v2/reporting-periods/{id}/open", "ReportingPeriods_Open");
        AssertReportingPeriodCommand(
            paths, "/api/v2/reporting-periods/{id}/close", "ReportingPeriods_Close");
        AssertReportingPeriodCommand(
            paths, "/api/v2/reporting-periods/{id}/finalize", "ReportingPeriods_Finalize");
        AssertDocumentedCrud(paths, "/api/v2/technologies", "/api/v2/technologies/{id}", "Technologies");
        AssertDocumentedCrud(paths, "/api/v2/projects", "/api/v2/projects/{id}", "Projects");
        AssertDocumentedCrud(paths, "/api/v2/memos", "/api/v2/memos/{id}", "Memos");
        AssertDocumentedEmployeeOperations(paths);

        AssertDocumentedOperation(paths.GetProperty("/api/v2/memos/{id}/publish").GetProperty("post"), "Memos");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/memos/{id}/withdraw").GetProperty("post"), "Memos");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/memos/{id}/acknowledgements").GetProperty("post"), "Memos");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/memos/preview").GetProperty("post"), "Memos");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notifications").GetProperty("get"), "Notifications");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notifications/{id}").GetProperty("get"), "Notifications");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notifications/{id}/read").GetProperty("post"), "Notifications");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notifications/read-all").GetProperty("post"), "Notifications");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/me").GetProperty("get"), "Settings");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/me/portal-profile").GetProperty("get"), "Settings");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/me").GetProperty("patch"), "Settings");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/me/password").GetProperty("post"), "Settings");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notification-preferences/me").GetProperty("get"), "Settings");
        AssertDocumentedOperation(paths.GetProperty("/api/v2/notification-preferences/me").GetProperty("patch"), "Settings");

        paths.TryGetProperty(
            "/api/v2/promotion-submissions/{promotionSubmissionId}/reports/{reportType}",
            out var promotionReportPath).Should().BeTrue();
        AssertDocumentedOperation(
            promotionReportPath.GetProperty("get"),
            "Promotions");
        AssertDocumentedOperation(
            promotionReportPath.GetProperty("put"),
            "Promotions");
        promotionReportPath.TryGetProperty("post", out _).Should().BeFalse();
        promotionReportPath.TryGetProperty("delete", out _).Should().BeFalse();

        paths.GetProperty("/api/v2/holidays").GetProperty("get")
            .GetProperty("tags")[0].GetString().Should().Be("Leave");
        paths.GetProperty("/api/v2/holidays").GetProperty("post")
            .GetProperty("tags")[0].GetString().Should().Be("Leave");
        paths.TryGetProperty("/api/v2/leave-types", out var leaveTypesPath).Should().BeTrue();
        AssertDocumentedOperation(leaveTypesPath.GetProperty("get"), "Leave");
        paths.TryGetProperty("/api/v2/leave-types/{code}", out var leaveTypePath).Should().BeTrue();
        AssertDocumentedOperation(leaveTypePath.GetProperty("get"), "Leave");
        paths.TryGetProperty("/api/v2/holiday-periods", out var holidayPeriodsPath).Should().BeTrue();
        holidayPeriodsPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Leave");
        holidayPeriodsPath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Leave");
        paths.TryGetProperty("/api/v2/skeletal-staff-requests", out var skeletalStaffPath).Should().BeTrue();
        skeletalStaffPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Leave");
        skeletalStaffPath.GetProperty("post").GetProperty("tags")[0].GetString().Should().Be("Leave");
        paths.TryGetProperty("/api/v2/skeletal-staff-requests/{id}/allowance-report", out var allowanceReportPath).Should().BeTrue();
        allowanceReportPath.GetProperty("get").GetProperty("tags")[0].GetString().Should().Be("Leave");

        paths.TryGetProperty("/api/v2/employees/{id}/profile-image", out var profileImagePath).Should().BeTrue();
        AssertDocumentedOperation(profileImagePath.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(profileImagePath.GetProperty("post"), "Human Resources");
        profileImagePath.GetProperty("get").GetProperty("responses").TryGetProperty("307", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/employees/{id}/profile-image/access", out var profileImageAccessPath).Should().BeTrue();
        AssertDocumentedOperation(profileImageAccessPath.GetProperty("get"), "Human Resources");

        paths.GetProperty("/api/v2/reports/{id}/submit").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("submitted state");
        paths.GetProperty("/api/v2/reports/{id}/approve").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("approving user");
        paths.GetProperty("/api/v2/reports/{id}/return").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("correction reason");
        paths.GetProperty("/api/v2/projects/{id}/submit").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("active lifecycle state");
        paths.GetProperty("/api/v2/projects/{id}/archive").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("no longer appear as active work");

        var tags = document.RootElement.GetProperty("tags");
        AssertDocumentedTag(tags, "Reports");
        AssertDocumentedTag(tags, "Reporting Periods");
        AssertDocumentedTag(tags, "Human Resources");
        AssertDocumentedTag(tags, "Promotions");
        AssertDocumentedTag(tags, "Leave");
        AssertDocumentedTag(tags, "Memos");
        AssertDocumentedTag(tags, "Notifications");
        AssertDocumentedTag(tags, "Settings");
        AssertDocumentedTag(tags, "Technologies");
        AssertDocumentedTag(tags, "Projects");
        AssertDocumentedTag(tags, "Staff Portal");
        AssertDocumentedTag(tags, "Files");
        AssertDocumentedTag(tags, "Identity and Access");
        AssertDocumentedTag(tags, "System Users");
        AssertDocumentedTag(tags, "Institutes");
        AssertDocumentedTag(tags, "Strategic Plans");
        AssertDocumentedTag(tags, "Thrusts");
        AssertDocumentedTag(tags, "Outputs");
        AssertDocumentedTag(tags, "Indicators");
        AssertDocumentedTag(tags, "Indicator Measurements");
        AssertDocumentedTag(tags, "Staff quarterly reports");
        AssertDocumentedTag(tags, "Promotion submissions");
        var tagNames = tags.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToList();
        tagNames.Should().NotContain(["Organization", "Strategic Plan Reports", "Promotion Reports", "Leave Holidays"]);

        AssertDocumentedOperation(
            paths.GetProperty("/api/v2/auth/sessions").GetProperty("post"),
            "Identity and Access");
        AssertDocumentedOperation(
            paths.GetProperty("/api/v2/auth/sessions/refresh").GetProperty("post"),
            "Identity and Access");
        AssertDocumentedOperation(
            paths.GetProperty("/api/v2/auth/login").GetProperty("post"),
            "Identity and Access");
    }

    [Fact]
    public async Task Scalar_V2_Page_Loads_V2_OpenApi_Document()
    {
        var response = await _client.GetAsync("/scalar/v2/");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("CSIR SPME API V2");
        html.Should().Contain(@"""title"":""CSIR SPME API V2""");
        html.Should().Contain(@"""url"":""../../openapi/v2.json""");
        html.Should().NotContain("openapi/v1.json");

        var resolvedDocumentUri = new Uri(new Uri("http://localhost/scalar/v2/"), "../../openapi/v2.json");
        resolvedDocumentUri.AbsolutePath.Should().Be("/openapi/v2.json");
    }

    [Fact]
    public async Task OpenApi_V2_Document_Describes_Bearer_Authentication_Only_For_Protected_Operations()
    {
        var response = await _client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        root.GetProperty("info").GetProperty("title").GetString().Should().Be("CSIR SPME API V2");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("2.0.0");
        root.GetProperty("info").GetProperty("description").GetString().Should().Contain("/api/v2");
        root.GetProperty("info").GetProperty("contact").GetProperty("name").GetString()
            .Should().Be("CSIR SPME Support");
        root.GetProperty("paths").TryGetProperty("/metrics", out _).Should().BeFalse();
        root.GetProperty("paths").TryGetProperty("/healthz", out _).Should().BeFalse();
        root.GetProperty("paths").TryGetProperty("/readyz", out _).Should().BeFalse();

        root.GetProperty("components").GetProperty("securitySchemes").GetProperty("BearerAuth")
            .GetProperty("scheme").GetString().Should().Be("bearer");

        AssertBearerProtected(root, "/api/v2/reporting-periods", "get");
        AssertBearerProtected(root, "/api/v2/auth/me", "get");
        AssertBearerProtected(root, "/api/v2/me/portal-profile", "get");
        AssertAnonymous(root, "/api/v2/auth/sessions", "post");
        AssertAnonymous(root, "/api/v2/auth/password-resets", "post");
        AssertAnonymous(root, "/api/v2/auth/password-resets/confirm", "post");
        var confirm = root.GetProperty("paths")
            .GetProperty("/api/v2/auth/password-resets/confirm")
            .GetProperty("post");
        confirm.GetProperty("responses").TryGetProperty("422", out _).Should().BeTrue();
        var schemaReference = confirm.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        schemaReference.Should().Be("#/components/schemas/ConfirmPasswordResetRequest");
        var confirmSchema = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("ConfirmPasswordResetRequest").GetProperty("properties");
        confirmSchema.TryGetProperty("requestId", out _).Should().BeTrue();
        confirmSchema.TryGetProperty("token", out _).Should().BeTrue();
        confirmSchema.TryGetProperty("email", out _).Should().BeFalse();
    }

    private static void AssertDocumentedCrud(JsonElement paths, string collectionPath, string itemPath, string tag)
    {
        paths.TryGetProperty(collectionPath, out var collection).Should().BeTrue();
        AssertDocumentedOperation(collection.GetProperty("get"), tag);
        AssertDocumentedOperation(collection.GetProperty("post"), tag);

        paths.TryGetProperty(itemPath, out var item).Should().BeTrue();
        AssertDocumentedOperation(item.GetProperty("get"), tag);
        AssertDocumentedOperation(item.GetProperty("patch"), tag);
        AssertDocumentedOperation(item.GetProperty("delete"), tag);
    }

    private static void AssertReportingPeriodCommand(
        JsonElement paths,
        string path,
        string operationId)
    {
        paths.TryGetProperty(path, out var commandPath).Should().BeTrue();
        var operation = commandPath.GetProperty("post");
        operation.GetProperty("operationId").GetString().Should().Be(operationId);
        AssertDocumentedOperation(operation, "Reporting Periods");
    }

    private static void AssertBearerProtected(JsonElement root, string path, string method)
    {
        var operation = root.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.TryGetProperty("security", out var security).Should().BeTrue();
        security.GetArrayLength().Should().Be(1);
        security[0].TryGetProperty("BearerAuth", out var scopes).Should().BeTrue();
        scopes.GetArrayLength().Should().Be(0);
    }

    private static void AssertResponseSchema(
        JsonElement paths,
        string path,
        string method,
        string statusCode,
        string expectedSchema)
    {
        var schema = paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        schema.GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{expectedSchema}");
    }

    private static void AssertAnonymous(JsonElement root, string path, string method)
    {
        var operation = root.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.TryGetProperty("security", out _).Should().BeFalse();
    }

    private static void AssertDocumentedEmployeeOperations(JsonElement paths)
    {
        paths.TryGetProperty("/api/v2/employees", out var employees).Should().BeTrue();
        AssertDocumentedOperation(employees.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(employees.GetProperty("post"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{id}", out var employee).Should().BeTrue();
        AssertDocumentedOperation(employee.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(employee.GetProperty("patch"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{id}/profile-image", out var profileImage).Should().BeTrue();
        AssertDocumentedOperation(profileImage.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(profileImage.GetProperty("post"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/employment-records", out var employmentRecords).Should().BeTrue();
        AssertDocumentedOperation(employmentRecords.GetProperty("get"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/spouse", out var spouse).Should().BeTrue();
        AssertDocumentedOperation(spouse.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(spouse.GetProperty("post"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/spouse/{spouseId}", out var spouseItem).Should().BeTrue();
        AssertDocumentedOperation(spouseItem.GetProperty("put"), "Human Resources");
        AssertDocumentedOperation(spouseItem.GetProperty("delete"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/children", out var children).Should().BeTrue();
        AssertDocumentedOperation(children.GetProperty("get"), "Human Resources");
        AssertDocumentedOperation(children.GetProperty("post"), "Human Resources");

        paths.TryGetProperty("/api/v2/employees/{employeeId}/children/{childId}", out var childItem).Should().BeTrue();
        AssertDocumentedOperation(childItem.GetProperty("put"), "Human Resources");
        AssertDocumentedOperation(childItem.GetProperty("delete"), "Human Resources");

        paths.TryGetProperty("/api/v2/education-certificate-types", out var certificateTypes).Should().BeTrue();
        AssertDocumentedOperation(certificateTypes.GetProperty("get"), "Human Resources");

        employees.GetProperty("get").GetProperty("description").GetString()
            .Should().Contain("paged employee directory");
        children.GetProperty("post").GetProperty("description").GetString()
            .Should().Contain("maximum of two child records");
    }

    private static void AssertDocumentedOperation(JsonElement operation, string tag)
    {
        operation.GetProperty("tags")[0].GetString().Should().Be(tag);
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        var description = operation.GetProperty("description").GetString();
        description.Should().NotBeNullOrWhiteSpace();
        description!.Length.Should().BeGreaterThan(80);
        description.Should().NotContain("/api/v2/");
    }

    private static void AssertDocumentedTag(JsonElement tags, string expectedName)
    {
        var tag = tags.EnumerateArray().Single(item =>
            item.GetProperty("name").GetString() == expectedName);
        var description = tag.GetProperty("description").GetString();
        description.Should().NotBeNullOrWhiteSpace();
        description!.Length.Should().BeGreaterThan(80);
    }
}
