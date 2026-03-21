namespace Courier.Tests.Integration.Rbac;

[Collection("Rbac")]
public abstract class RbacTestBase(RbacFixture fixture)
{
    protected RbacFixture Fixture { get; } = fixture;

    protected HttpClient ClientForRole(string role) => role switch
    {
        "admin" => Fixture.AdminClient,
        "operator" => Fixture.OperatorClient,
        "viewer" => Fixture.ViewerClient,
        "anonymous" => Fixture.AnonymousClient,
        _ => throw new ArgumentException($"Unknown role: {role}"),
    };
}
