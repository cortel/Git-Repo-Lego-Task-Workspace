using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public interface ITeamsDirectory
{
    Task<IReadOnlyList<TeamsAssignment>> GetAssignmentsAsync(CancellationToken ct);

    // Missing must mean an authoritative 404, never a timeout, permission failure, or incomplete page.
    Task<DirectoryUserState> GetUserStateAsync(Guid userId, CancellationToken ct);
}
