using Csir.Spme.Domain.Hr;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public class EmployeeDependantTests
{
    [Fact]
    public void EmployeeSpouse_Normalizes_Optional_Fields()
    {
        var spouse = new EmployeeSpouse(
            Guid.NewGuid(),
            "  Akua Mensah  ",
            new DateTime(1990, 5, 12),
            " 0240000000 ",
            " akua@example.test ",
            " Accountant ",
            " CSIR ");

        spouse.Name.Should().Be("Akua Mensah");
        spouse.Phone.Should().Be("0240000000");
        spouse.Email.Should().Be("akua@example.test");
        spouse.Occupation.Should().Be("Accountant");
        spouse.Employer.Should().Be("CSIR");

        spouse.Update("Akua Owusu", null, "", " ", null, "");

        spouse.Name.Should().Be("Akua Owusu");
        spouse.DateOfBirth.Should().BeNull();
        spouse.Phone.Should().BeNull();
        spouse.Email.Should().BeNull();
        spouse.Occupation.Should().BeNull();
        spouse.Employer.Should().BeNull();
    }

    [Fact]
    public void EmployeeChild_Normalizes_Child_Fields()
    {
        var child = new EmployeeChild(
            Guid.NewGuid(),
            " Ama Mensah ",
            new DateTime(2015, 1, 3),
            " female ",
            " BC-001 ",
            null);

        child.Name.Should().Be("Ama Mensah");
        child.Gender.Should().Be("female");
        child.BirthCertificateNumber.Should().Be("BC-001");

        child.Update("Ama Owusu", new DateTime(2015, 1, 3), "female", " ", Guid.NewGuid());

        child.Name.Should().Be("Ama Owusu");
        child.BirthCertificateNumber.Should().BeNull();
        child.BirthCertificateFileId.Should().NotBeNull();
    }
}
