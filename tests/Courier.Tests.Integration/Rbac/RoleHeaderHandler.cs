namespace Courier.Tests.Integration.Rbac;

/// <summary>
/// DelegatingHandler that adds X-Test-Role header to all requests.
/// Used to parameterize the test auth handler's role.
/// </summary>
public class RoleHeaderHandler(string role) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Test-Role", role);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// DelegatingHandler that marks requests as anonymous.
/// TestAuthHandler returns NoResult when it sees this header, causing 401.
/// </summary>
public class AnonymousHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Test-Anonymous", "true");
        return base.SendAsync(request, cancellationToken);
    }
}
