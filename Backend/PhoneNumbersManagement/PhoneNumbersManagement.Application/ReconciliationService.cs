using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed class ReconciliationService(IRegistryStore store, ITeamsDirectory teams, TimeProvider clock)
{
    private const int BatchSize = 500;
    private static readonly TimeSpan ObservationReuseWindow = TimeSpan.FromSeconds(30);

    public async Task SynchronizeAsync(CancellationToken ct)
    {
        // Fully retrieve and validate the external scan before changing local assignments.
        var snapshot = await teams.GetAssignmentsAsync(ct);
        var assignments = ValidateAssignments(snapshot);
        var observations = new Dictionary<Guid, (DirectoryUserState State, DateTimeOffset ObservedAt)>();

        async Task<DirectoryUserState> UserStateAsync(Guid id, CancellationToken token)
        {
            if (observations.TryGetValue(id, out var observation))
            {
                var age = clock.GetUtcNow() - observation.ObservedAt;
                if (age >= TimeSpan.Zero && age < ObservationReuseWindow)
                {
                    return observation.State;
                }
            }

            var state = await teams.GetUserStateAsync(id, token);
            observations[id] = (state, clock.GetUtcNow());
            return state;
        }

        foreach (var batch in assignments.Chunk(BatchSize))
        {
            var managed = await store.ManagedPhonesAsync(batch.Select(x => x.Number).ToArray(), ct);
            foreach (var assignment in batch)
            {
                // Do not perform directory lookups or open write transactions for unmanaged numbers.
                if (!managed.TryGetValue(assignment.Number, out var phoneState))
                {
                    continue;
                }

                if (await UserStateAsync(assignment.UserId, ct) == DirectoryUserState.Missing)
                {
                    continue;
                }

                if (phoneState.MatchesAssignment(assignment.UserId))
                {
                    continue;
                }

                await store.WriteAsync(async token =>
                {
                    // Always reload inside the transaction; a batch projection is not a write authority.
                    var phone = await store.FindPhoneByNumberAsync(assignment.Number, token);
                    if (phone is null)
                    {
                        return false;
                    }

                    var hold = await store.ReservationForPhoneAsync(phone.Id, token);
                    if (hold is not null)
                    {
                        store.Remove(hold);
                    }

                    phone.Assign(assignment.UserId);
                    return true;
                }, ct);
            }
        }

        // Scan only assigned numbers, with a stable numeric cursor rather than growing OFFSETs.
        long cursor = 0;
        while (true)
        {
            var page = await store.AssignedPhonesAfterAsync(cursor, BatchSize, ct);
            foreach (var candidate in page)
            {
                if (await UserStateAsync(candidate.UserId, ct) != DirectoryUserState.Missing)
                {
                    continue;
                }

                await store.WriteAsync(async token =>
                {
                    var phone = await store.FindPhoneAsync(candidate.Id, token);
                    if (phone is null || phone.AssignedUser != candidate.UserId)
                    {
                        return false;
                    }

                    var hold = await store.ReservationForPhoneAsync(phone.Id, token);
                    if (hold?.Kind == ReservationKind.Offboarding)
                    {
                        return false;
                    }

                    if (hold is not null)
                    {
                        throw new Conflict("Unexpected reservation on assigned phone number.");
                    }

                    var now = clock.GetUtcNow();
                    var until = now.AddDays(30);
                    phone.StartOffboarding(until);
                    store.Add(new Reservation(phone.Id, new ReservationDetails
                    {
                        ReservedFor = "system",
                        Reason = "Offboarding quarantine",
                        System = ReservationSystem.MicrosoftTeams,
                        Expires = until
                    }, now, ReservationKind.Offboarding));
                    return true;
                }, ct);
            }

            if (page.Count < BatchSize)
            {
                break;
            }

            cursor = page[^1].NumericValue;
        }
    }

    private static IReadOnlyCollection<TeamsAssignment> ValidateAssignments(IReadOnlyList<TeamsAssignment> snapshot)
    {
        var unique = new Dictionary<string, TeamsAssignment>(snapshot.Count, StringComparer.Ordinal);
        foreach (var assignment in snapshot)
        {
            E164.Validate(assignment.Number);
            if (assignment.UserId == Guid.Empty)
            {
                throw new RuleViolation("Teams returned an invalid object ID.");
            }

            if (unique.TryGetValue(assignment.Number, out var existing) && existing.UserId != assignment.UserId)
            {
                throw new Conflict("Teams returned multiple owners for the same number.");
            }

            unique[assignment.Number] = assignment;
        }

        return unique.Values;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var expired = await store.ExpiredAsync(clock.GetUtcNow(), 1000, ct);
        foreach (var item in expired)
        {
            if (item.Kind == ReservationKind.Offboarding)
            {
                var phone = await store.FindPhoneAsync(item.PhoneNumberId, ct);
                if (phone?.AssignedUser is { } userId && await teams.GetUserStateAsync(userId, ct) != DirectoryUserState.Missing)
                {
                    await EndAsync(item.Id, releaseAssignment: false, ct);
                    continue;
                }
            }

            await EndAsync(item.Id, item.Kind == ReservationKind.Offboarding, ct);
        }
    }

    private Task<bool> EndAsync(Guid id, bool releaseAssignment, CancellationToken ct) => store.WriteAsync(async token =>
    {
        var reservation = await store.FindReservationAsync(id, token);
        if (reservation is null || reservation.Expires is null || reservation.Expires > clock.GetUtcNow())
        {
            return false;
        }

        var phone = await store.FindPhoneAsync(reservation.PhoneNumberId, token) ?? throw new EntityNotFound("Phone number");
        store.Remove(reservation);
        if (releaseAssignment)
        {
            phone.Release(false);
        }
        else
        {
            phone.ReservationEnded();
        }

        return true;
    }, ct);
}
