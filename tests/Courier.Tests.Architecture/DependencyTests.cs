using NetArchTest.Rules;
using Shouldly;

namespace Courier.Tests.Architecture;

public class DependencyTests
{
    private static readonly string DomainNamespace = "Courier.Domain";
    private static readonly string InfrastructureNamespace = "Courier.Infrastructure";
    private static readonly string FeaturesNamespace = "Courier.Features";
    private static readonly string MigrationsNamespace = "Courier.Migrations";

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Job).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain should not depend on Infrastructure. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Features()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Job).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FeaturesNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain should not depend on Features. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Migrations()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Job).Assembly)
            .ShouldNot()
            .HaveDependencyOn(MigrationsNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain should not depend on Migrations. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Domain_ShouldHaveNoDependencyOnExternalPackages()
    {
        // Domain must be BCL-only — no NuGet references
        var domainAssembly = typeof(Domain.Entities.Job).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System") && !n.StartsWith("Microsoft") && n != "netstandard")
            .ToList();

        referencedAssemblies.ShouldBeEmpty(
            $"Domain references external assemblies: {string.Join(", ", referencedAssemblies)}");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Features()
    {
        var result = Types.InAssembly(typeof(Infrastructure.Data.CourierDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FeaturesNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Infrastructure should not depend on Features. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Migrations()
    {
        var result = Types.InAssembly(typeof(Infrastructure.Data.CourierDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn(MigrationsNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Infrastructure should not depend on Migrations. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Migrations_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(typeof(Migrations.MigrationMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(DomainNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Migrations should not depend on Domain. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Migrations_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Migrations.MigrationMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Migrations should not depend on Infrastructure. Failing types: {FormatFailures(result)}");
    }

    [Fact]
    public void Migrations_ShouldNotDependOn_Features()
    {
        var result = Types.InAssembly(typeof(Migrations.MigrationMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(FeaturesNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Migrations should not depend on Features. Failing types: {FormatFailures(result)}");
    }

    private static string FormatFailures(NetArchTest.Rules.TestResult result)
    {
        if (result.FailingTypes == null) return "(none)";
        return string.Join(", ", result.FailingTypes.Select(t => t.FullName));
    }
}
