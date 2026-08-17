using System.Net;
using System.Text;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class CommunicationTransportTests
{
    [Fact]
    public async Task ZeptoMail_Uses_Category_Credentials_And_Typed_V11_Payload()
    {
        HttpRequestMessage? captured = null;
        string? payload = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            captured = request;
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"details\":[{\"message_uuid\":\"zepto-123\"}]}]}")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "default-token",
            FromEmail = "default@csir.test",
            AuthSendMailToken = "auth-token",
            AuthFromEmail = "accounts@csir.test",
            AuthFromName = "CSIR Accounts"
        }));

        var result = await transport.SendAsync(
            "employee@csir.test", "Activation", "<p>Code</p>", true, "Code 123456", "authentication");

        result.Accepted.Should().BeTrue();
        result.ProviderMessageId.Should().Be("zepto-123");
        captured!.RequestUri!.AbsolutePath.Should().Be("/v1.1/email");
        captured.Headers.GetValues("Authorization").Single().Should().Be("Zoho-enczapikey auth-token");
        payload.Should().Contain("accounts@csir.test").And.Contain("employee@csir.test")
            .And.Contain("htmlbody").And.Contain("textbody").And.Contain("Code 123456");
    }

    [Fact]
    public async Task ZeptoMail_Includes_Pdf_Attachments_For_Quarterly_Report_Mail()
    {
        string? payload = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"details\":[{\"message_uuid\":\"zepto-pdf\"}]}]}")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "default-token",
            FromEmail = "default@csir.test",
            NotifySendMailToken = "notify-token",
            NotifyFromEmail = "notify@csir.test",
            NotifyFromName = "CSIR Notifications"
        }));
        var pdf = Convert.ToBase64String("%PDF-1.4 test"u8.ToArray());

        var result = await transport.SendAsync(
            "hod@csir.test", "Quarterly report", "<p>Report</p>", true, "Report",
            "staff-quarterly-report-submitted", CancellationToken.None,
            [new EmailAttachment("staff-quarterly-report.pdf", "application/pdf", pdf)]);

        result.Accepted.Should().BeTrue();
        payload.Should().Contain("staff-quarterly-report.pdf").And.Contain("application/pdf").And.Contain(pdf);
    }

    [Fact]
    public void Quarterly_Report_Pdf_Contains_Report_Fields()
    {
        var pdf = StaffQuarterlyReportPdf.Build(new StaffQuarterlyReportNotification(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Hannah Reviewer", "hod@csir.test", "+233200000002", "Ada Researcher",
            "2026 Quarter 3", "Coastal materials", "Research abstract", "Completed sampling",
            "Validated samples", "Continue analysis", ["Water quality project"], [],
            [new StaffQuarterlyProjectReportContent(
                "WATER-01", "Water quality project", "PIN-001", "Ada Researcher", "12 months", "CSIR",
                "Accra", "Assess water quality", "Laboratory analysis", "Public health need",
                "Coastal communities", "Water sensor", "Licensing pathway", "Validated methodology",
                "Completed sampling", "Validated samples", "Heavy rainfall", "Laboratory analysis",
                "Continue analysis", 1, 0)],
            ["sampling-site.jpg"], DateTimeOffset.UtcNow));
        var text = Encoding.ASCII.GetString(pdf);
        text.Should().StartWith("%PDF-1.4");
        text.Should().Contain("Coastal materials").And.Contain("Ada Researcher")
            .And.Contain("Research abstract").And.Contain("Water quality project")
            .And.Contain("FORM 1 - PROJECT INCEPTION")
            .And.Contain("FORM 2 - RESEARCH IN PROGRESS")
            .And.Contain("sampling-site.jpg");
    }

    [Fact]
    public async Task ZeptoMail_Uses_Notification_Sender_For_Leave_Categories()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"details\":[{\"message_uuid\":\"zepto-leave\"}]}]}")
            });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "default-token",
            FromEmail = "default@csir.test",
            NotifySendMailToken = "notify-token",
            NotifyFromEmail = "notify@csir.test",
            NotifyFromName = "CSIR Notifications"
        }));

        var result = await transport.SendAsync(
            "hod@csir.test", "Leave", "<p>Approved</p>", true, "Approved", "leave-approved");

        result.Accepted.Should().BeTrue();
        captured!.Headers.GetValues("Authorization").Single().Should().Be("Zoho-enczapikey notify-token");
    }

    [Fact]
    public void Branded_Email_Renderer_Html_Encodes_Dynamic_Content()
    {
        var renderer = new BrandedEmailRenderer(Options.Create(new PortalUrlOptions
        {
            StaffPasswordResetUrl = "https://staff.example/reset-password",
            HrPasswordResetUrl = "https://hr.example/reset-password",
            StaffPortalUrl = "https://staff.example",
            HrPortalUrl = "https://hr.example",
            LogoUrl = "https://assets.example/logo.png?x=<unsafe>"
        }));

        var email = renderer.PasswordReset("<script>alert('name')</script>",
            "https://staff.example/reset-password?token=<unsafe>&requestId=1");

        email.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;")
            .And.Contain("token=&lt;unsafe&gt;&amp;requestId=1")
            .And.Contain("logo.png?x=&lt;unsafe&gt;");
        email.TextBody.Should().Contain("24 hours");
    }

    [Fact]
    public void Branded_Leave_Approval_Email_Encodes_Names_And_Uses_Staff_Portal()
    {
        var renderer = new BrandedEmailRenderer(Options.Create(new PortalUrlOptions
        {
            StaffPasswordResetUrl = "https://staff.example/reset-password",
            HrPasswordResetUrl = "https://hr.example/reset-password",
            StaffPortalUrl = "https://staff.example",
            HrPortalUrl = "https://hr.example",
        }));

        var leaveId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var email = renderer.LeaveAwaitingApproval(
            "<script>approver</script>",
            "<img src=x>",
            "section-head",
            "annual",
            new DateTime(2026, 12, 7),
            new DateTime(2026, 12, 8),
            2,
            leaveId);

        email.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;")
            .And.Contain($"https://staff.example/leave/{leaveId:D}").And.Contain("section head");
        email.TextBody.Should().Contain($"Review leave request: https://staff.example/leave/{leaveId:D}");
    }

    [Fact]
    public void Hr_Approval_Email_And_Sms_Include_Staff_Portal_Link()
    {
        var renderer = new BrandedEmailRenderer(Options.Create(new PortalUrlOptions
        {
            StaffPasswordResetUrl = "https://portal.csirstrategicplan.org/reset-password",
            HrPasswordResetUrl = "https://hr.example/reset-password",
            StaffPortalUrl = "https://portal.csirstrategicplan.org",
            HrPortalUrl = "https://hr.example",
        }));

        var email = renderer.HrApprovalAccess("<script>staff</script>", "1249100");
        var sms = renderer.HrApprovalAccessSms();

        email.Subject.Should().Be("Your CSIR staff portal access is ready");
        email.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;")
            .And.Contain("https://portal.csirstrategicplan.org")
            .And.Contain("1249100")
            .And.Contain("Apply for leave")
            .And.Contain("Open staff portal");
        email.TextBody.Should().Contain("https://portal.csirstrategicplan.org")
            .And.Contain("Staff ID: 1249100")
            .And.Contain("apply for leave");
        sms.Should().Contain("https://portal.csirstrategicplan.org")
            .And.Contain("approved");
        sms.Length.Should().BeLessThan(160);
    }

    [Theory]
    [InlineData("auth-token")]
    [InlineData("Zoho-enczapikey auth-token")]
    [InlineData("Zoho-enczapikey  Zoho-enczapikey auth-token")]
    [InlineData("\"auth-token\"")]
    [InlineData("'Zoho-enczapikey auth-token'")]
    [InlineData("\"Zoho-enczapikey auth-token\"\n")]
    public async Task ZeptoMail_Normalizes_Prefixed_Send_Mail_Tokens(string storedToken)
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"details\":[{\"message_uuid\":\"zepto-123\"}]}]}")
            });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "default-token",
            FromEmail = "default@csir.test",
            AuthSendMailToken = storedToken,
            AuthFromEmail = "accounts@csir.test",
            AuthFromName = "CSIR Accounts"
        }));

        var result = await transport.SendAsync(
            "employee@csir.test", "Activation", "Code 123456", false, "authentication");

        result.Accepted.Should().BeTrue();
        captured!.Headers.GetValues("Authorization").Single().Should().Be("Zoho-enczapikey auth-token");
    }

    [Fact]
    public async Task ZeptoMail_Uses_Default_From_Name_When_Category_Name_Is_Blank()
    {
        string? payload = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"details\":[{\"message_uuid\":\"zepto-123\"}]}]}")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "default-token",
            FromEmail = "default@csir.test",
            FromName = "CSIR SPME System",
            NotifySendMailToken = "notify-token",
            NotifyFromEmail = "notify@csir.test",
            NotifyFromName = ""
        }));

        var result = await transport.SendAsync(
            "employee@csir.test", "Reset", "Use this link", false, "notification");

        result.Accepted.Should().BeTrue();
        payload.Should().Contain("CSIR SPME System").And.Contain("notify@csir.test");
    }

    [Fact]
    public async Task ZeptoMail_Classifies_Throttling_As_Transient()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests))))
        { BaseAddress = new Uri("https://api.zeptomail.com") };
        var transport = new ZeptoMailTransport(httpClient, Options.Create(new ZeptoMailOptions
        {
            Enabled = true,
            SendMailToken = "token",
            FromEmail = "sender@csir.test"
        }));

        var result = await transport.SendAsync("recipient@csir.test", "Subject", "Body", false, "notification");

        result.Accepted.Should().BeFalse();
        result.ErrorCode.Should().Be("provider_rate_limited");
        result.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task MNotify_Normalizes_Ghana_Number_And_Rejects_Error_Code_In_Http_200()
    {
        string? payload = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            request.RequestUri!.Query.Should().Contain("key=test-key");
            request.RequestUri.AbsolutePath.Should().Be("/api/sms/quick");
            payload = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":\"2001\",\"message\":\"insufficient credit balance\"}")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.mnotify.com/api/") };
        var transport = new MNotifyTransport(httpClient, Options.Create(new MNotifyOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            SenderId = "CSIR",
            RetryCount = 0
        }));

        var result = await transport.SendAsync("024 123 4567", "Test message");

        result.Accepted.Should().BeFalse();
        result.ErrorCode.Should().Be("provider_credit_exhausted");
        payload.Should().Contain("233241234567").And.Contain("CSIR");
    }

    [Theory]
    [InlineData("0241234567", "233241234567")]
    [InlineData("+233 24 123 4567", "233241234567")]
    [InlineData("233241234567", "233241234567")]
    public void Ghana_Phone_Normalization_Is_Canonical(string input, string expected) =>
        LoginIdentifierNormalizer.NormalizeGhanaPhone(input).Should().Be(expected);

    [Theory]
    [InlineData("call-0241234567")]
    [InlineData("12345")]
    [InlineData("+2348012345678")]
    public void Ghana_Phone_Normalization_Rejects_Invalid_Input(string input) =>
        LoginIdentifierNormalizer.NormalizeGhanaPhone(input).Should().BeNull();

    [Theory]
    [InlineData("1537625", "staff-id", "1537625")]
    [InlineData("CSIR-1537625", "staff-id", "1537625")]
    [InlineData("csir1537625", "staff-id", "1537625")]
    [InlineData("name@csir.org.gh", "email", "NAME@CSIR.ORG.GH")]
    [InlineData("024 123 4567", "phone", "233241234567")]
    public void Login_Identifier_Normalization_Keeps_Numeric_Staff_Ids(
        string input, string expectedType, string expectedValue)
    {
        var normalized = LoginIdentifierNormalizer.Normalize(input);
        normalized.Should().NotBeNull();
        normalized!.Value.Type.Should().Be(expectedType);
        normalized.Value.Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("Development", "clean", "clean")]
    [InlineData("Development", "pending", "pending")]
    [InlineData("Production", "clean", "pending")]
    public async Task Development_Scan_Result_Is_Honored_Only_In_Development(
        string environmentName, string configured, string expected)
    {
        var scanner = new DeferredPromotionMalwareScanner(
            new TestHostEnvironment(environmentName),
            Options.Create(new PromotionUploadOptions { DevelopmentScanResult = configured }));
        (await scanner.ScanAsync("promotions/test")).Should().Be(expected);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _handler(request);
    }
}
