using System.Net;
using System.Net.Http.Json;
using Azure.Core;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Infrastructure;
namespace PhoneNumbersManagement.Tests;

public sealed class GraphTests
{
    [Fact]
    public async Task FollowsAllAssignmentPagesAndKeepsOnlyUserAssignments()
    {
        var user = Guid.NewGuid();
        var calls = new List<Uri>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            calls.Add(request.RequestUri!);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var secondPage = request.RequestUri!.Query.Contains("skip");
            object payload = secondPage
                ? new { value = new[] { new { assignmentStatus = "userAssigned", telephoneNumber = "+4512345601", assignmentTargetId = user } } }
                : new Dictionary<string, object> { ["value"] = new[] {
                    new { assignmentStatus = "userAssigned", telephoneNumber = "+4512345600", assignmentTargetId = user },
                    new { assignmentStatus = "unassigned", telephoneNumber = "+4512345602", assignmentTargetId = Guid.Empty } },
                    ["@odata.nextLink"] = "https://graph.microsoft.com/v1.0/admin/teams/telephoneNumberManagement/numberAssignments?$skip=2" };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
        }));
        var adapter = new GraphTeamsDirectory(client, new StubCredential());
        var assignments = await adapter.GetAssignmentsAsync(default);
        Assert.Equal(2, calls.Count); Assert.Equal(2, assignments.Count);
        Assert.All(assignments, x => Assert.Equal(user, x.UserId));
    }
    [Theory]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public async Task Only404MeansMissingUser(HttpStatusCode code, bool missing)
    {
        using var client = new HttpClient(new StubHandler(_ => new(code)));
        var adapter = new GraphTeamsDirectory(client, new StubCredential());
        if (missing) Assert.Equal(DirectoryUserState.Missing, await adapter.GetUserStateAsync(Guid.NewGuid(), default));
        else await Assert.ThrowsAsync<HttpRequestException>(() => adapter.GetUserStateAsync(Guid.NewGuid(), default));
    }
    [Fact]
    public async Task RejectsPaginationOutsideMicrosoftGraph()
    {
        using var client = new HttpClient(new StubHandler(_ => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new Dictionary<string, object> { ["value"] = Array.Empty<object>(), ["@odata.nextLink"] = "https://example.invalid/steal" }) }));
        var adapter = new GraphTeamsDirectory(client, new StubCredential());
        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.GetAssignmentsAsync(default));
    }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handler(request));
    }
    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("test-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
}
