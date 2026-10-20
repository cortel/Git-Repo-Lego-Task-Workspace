using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using PhoneNumbersManagement.Application;
namespace PhoneNumbersManagement.Infrastructure;

public sealed class GraphTeamsDirectory(HttpClient http, TokenCredential credential) : ITeamsDirectory
{
    private const string Root = "https://graph.microsoft.com/v1.0/";
    public async Task<IReadOnlyList<TeamsAssignment>> GetAssignmentsAsync(CancellationToken ct)
    {
        var result = new List<TeamsAssignment>();
        string? next = Root + "admin/teams/telephoneNumberManagement/numberAssignments?$top=1000";
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var skip = 0;
        while (next is not null)
        {
            if (!visited.Add(next)) throw new HttpRequestException("Graph returned a repeated pagination link.");
            using var response = await SendAsync(next, ct);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var values = document.RootElement.GetProperty("value");
            foreach (var item in values.EnumerateArray())
            {
                if (!item.TryGetProperty("assignmentStatus", out var status) || status.GetString() != "userAssigned") continue;
                var number = item.GetProperty("telephoneNumber").GetString() ?? throw new HttpRequestException("Missing Teams phone number.");
                var user = item.GetProperty("assignmentTargetId").GetGuid();
                result.Add(new(number, user));
            }
            skip += values.GetArrayLength();
            next = document.RootElement.TryGetProperty("@odata.nextLink", out var link) ? link.GetString()
                : values.GetArrayLength() == 1000 ? Root + $"admin/teams/telephoneNumberManagement/numberAssignments?$top=1000&$skip={skip}" : null;
        }
        return result;
    }
    public async Task<DirectoryUserState> GetUserStateAsync(Guid userId, CancellationToken ct)
    {
        using var response = await SendAsync(Root + $"users/{userId}?$select=id", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return DirectoryUserState.Missing;
        response.EnsureSuccessStatusCode();
        return DirectoryUserState.Exists;
    }
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "graph.microsoft.com" || uri.Port != 443)
            throw new HttpRequestException("Untrusted Graph pagination link.");
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await http.SendAsync(request, ct);
    }
}