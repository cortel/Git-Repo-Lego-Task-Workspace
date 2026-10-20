using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement.Application;

public sealed class ReconciliationService(IRegistryStore store, ITeamsDirectory teams, TimeProvider clock)
{
    public async Task SynchronizeAsync(CancellationToken ct)
    {
        // Fetch all pages before making changes. An incomplete external snapshot must never release numbers.
        var assignments = await teams.GetAssignmentsAsync(ct);
        foreach (var assignment in assignments)
        {
            E164.Validate(assignment.Number);
            if (assignment.UserId == Guid.Empty)
            {
                throw new RuleViolation("Teams returned an invalid object ID.");
            }
        }

        if (assignments.GroupBy(x => x.Number).Any(g => g.Select(x => x.UserId).Distinct().Count() > 1))
        {
            throw new Conflict("Teams returned multiple owners for the same number.");
        }

        foreach (var assignment in assignments)
        {
            if (await teams.GetUserStateAsync(assignment.UserId, ct) == DirectoryUserState.Missing)
            {
                continue;
            }

            await store.WriteAsync(async token =>
            {
                var phone = await store.FindPhoneByNumberAsync(assignment.Number, token);
                if (phone is null)
                {
                    return false; // Only managed ranges are synchronized.
                }

                var hold = await store.ReservationForPhoneAsync(phone.Id, token);
                if (hold is not null)
                {
                    store.Remove(hold); // Authoritative assignment consumes the reservation.
                }

                phone.Assign(assignment.UserId);
                return true;
            }, ct);
        }

        for (var offset = 0; ; offset += 1000)
        {
            var page = await store.PhonesAsync(new PhoneQuery(Offset: offset, Limit: 1000), ct);
            foreach (var candidate in page.Items.Where(x => x.AssignedUser is not null))
            {
                var userId = candidate.AssignedUser!.Value;
                if (await teams.GetUserStateAsync(userId, ct) != DirectoryUserState.Missing)
                {
                    continue;
                }

                await store.WriteAsync(async token =>
                {
                    var phone = await store.FindPhoneAsync(candidate.Id, token);
                    if (phone is null || phone.AssignedUser != userId)
                    {
                        return false;
                    }

                    var hold = await store.ReservationForPhoneAsync(phone.Id, token);
                    if (hold?.Kind == ReservationKind.Offboarding)
                    {
                        return false; // Never reset the timer on retries.
                    }

                    if (hold is not null)
                    {
                        throw new Conflict("Unexpected reservation on assigned phone number.");
                    }

                    var until = clock.GetUtcNow().AddDays(30);
                    phone.StartOffboarding(until);
                    store.Add(new Reservation(phone.Id, "system", "Offboarding quarantine", ReservationSystem.MicrosoftTeams,
                        until, null, clock.GetUtcNow(), ReservationKind.Offboarding));
                    return true;
                }, ct);
            }

            if (page.Items.Count < 1000)
            {
                break;
            }
        }
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
