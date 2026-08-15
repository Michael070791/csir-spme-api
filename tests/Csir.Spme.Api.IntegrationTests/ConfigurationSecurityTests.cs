using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class ConfigurationSecurityTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public ConfigurationSecurityTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public void TestHost_Uses_The_Isolated_Jwt_Key_For_Issuing_And_Validation()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var configuredKey = configuration["Jwt:Key"];
        configuredKey.Should().NotBeNullOrWhiteSpace();

        var options = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var signingKey = options.TokenValidationParameters.IssuerSigningKey
            .Should().BeOfType<SymmetricSecurityKey>().Subject;

        signingKey.Key.Should().Equal(Encoding.UTF8.GetBytes(configuredKey!));
    }
}
