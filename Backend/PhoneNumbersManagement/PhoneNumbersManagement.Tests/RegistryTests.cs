using Microsoft.EntityFrameworkCore;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;
using PhoneNumbersManagement.Infrastructure;

namespace PhoneNumbersManagement.Tests;

public sealed class RegistryTests
{
    internal static RangeInput Input(string start = "+4512345600", string end = "+4512345602") =>
        new() { Name = "Aarhus", RangeStart = start, RangeEnd = end, Active = true, Provider = "Provider", Type = RangeType.DirectRouting, VoiceRoute = "sbc.example", NetboxSiteId = "site-1" };

    [Theory]
    [InlineData("4512345678")]
    [InlineData("+012345")]
    [InlineData("+45 12345678")]
    [InlineData("+1234567890123456")]
    public void RejectsInvalidE164(string number) => Assert.Throws<RuleViolation>(() => E164.Validate(number));

    [Fact]
    public async Task RangeCreatesInclusiveNumbersAndCascadesReservations()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var numbers = await test.Service.NextAvailableAsync(range.Id, 10, default);
        Assert.Equal(new[] { "+4512345600", "+4512345601", "+4512345602" }, numbers.Select(x => x.LineUri));
        await test.Service.CreateReservationAsync(new() { ReservedFor = "system", PhoneNumberId = numbers[0].Id, System = ReservationSystem.Other }, default);

        // Clear tracking so this verifies database cascading, rather than EF's in-memory relationship fixup.
        test.Db.ChangeTracker.Clear();
        await test.Service.DeleteRangeAsync(range.Id, default);
        Assert.Empty(await test.Db.PhoneNumbers.ToListAsync());
        Assert.Empty(await test.Db.Reservations.ToListAsync());
    }

    [Fact]
    public async Task DoubleReservationFailsWithoutChangingFirstReservation()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var phone = (await test.Service.NextAvailableAsync(range.Id, 1, default))[0];
        var input = new ReservationInput { ReservedFor = "user", PhoneNumberId = phone.Id, System = ReservationSystem.MicrosoftTeams, TicketNumber = "TASK-1" };
        var first = await test.Service.CreateReservationAsync(input, default);
        await Assert.ThrowsAsync<Conflict>(() => test.Service.CreateReservationAsync(input, default));
        Assert.Single(await test.Db.Reservations.ToListAsync());
        Assert.Equal(first.Id, (await test.Db.Reservations.SingleAsync()).Id);
        Assert.Equal(PhoneStatus.Reserved, (await test.Service.PhoneAsync(phone.Id, default)).Status);
    }

    [Fact]
    public async Task OverlappingRangeIsRejectedAtomically()
    {
        using var test = new TestDatabase();
        await test.Service.CreateRangeAsync(Input(), default);
        await Assert.ThrowsAsync<Conflict>(() => test.Service.CreateRangeAsync(Input("+4512345602", "+4512345604"), default));
        Assert.Equal(3, await test.Db.PhoneNumbers.CountAsync());
        Assert.Equal(1, await test.Db.Ranges.CountAsync());
    }

    [Fact]
    public async Task InactiveRangeCannotBeReserved()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(Input() with
        {
            Active = false
        }, default);
        var phone = await test.Db.PhoneNumbers.FirstAsync();
        await Assert.ThrowsAsync<Conflict>(() => test.Service.CreateReservationAsync(new() { ReservedFor = "system", PhoneNumberId = phone.Id, System = ReservationSystem.Other }, default));
    }

    [Fact]
    public async Task ExpiryReleasesNumberButNeverExpiresPermanentReservation()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var phones = await test.Service.NextAvailableAsync(range.Id, 2, default);
        await test.Service.CreateReservationAsync(new() { ReservedFor = "system", PhoneNumberId = phones[0].Id, System = ReservationSystem.Other, Expires = test.Clock.Now.AddHours(1) }, default);
        await test.Service.CreateReservationAsync(new() { ReservedFor = "system", PhoneNumberId = phones[1].Id, System = ReservationSystem.Other }, default);
        test.Clock.Now = test.Clock.Now.AddHours(2);
        await new ReconciliationService(new EfRegistryStore(test.Db), new FakeTeams(), test.Clock).CleanupAsync(default);
        Assert.Equal(PhoneStatus.Available, (await test.Service.PhoneAsync(phones[0].Id, default)).Status);
        Assert.Equal(PhoneStatus.Reserved, (await test.Service.PhoneAsync(phones[1].Id, default)).Status);
        Assert.Single(await test.Db.Reservations.ToListAsync());
    }

    [Fact]
    public async Task OffboardingIsIdempotentAndReleasesOnlyAfterThirtyDays()
    {
        using var test = new TestDatabase();
        var user = Guid.NewGuid();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var phone = (await test.Service.NextAvailableAsync(range.Id, 1, default))[0];
        var teams = new FakeTeams { Assignments = [new(phone.LineUri, user)] };
        var jobs = new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock);
        await jobs.SynchronizeAsync(default);
        teams.State = DirectoryUserState.Missing; // Keep stale assignment in snapshot deliberately.
        await jobs.SynchronizeAsync(default);
        var until = (await test.Service.PhoneAsync(phone.Id, default)).ScheduledUnassignment;
        test.Clock.Now = test.Clock.Now.AddDays(1);
        await jobs.SynchronizeAsync(default);
        Assert.Equal(until, (await test.Service.PhoneAsync(phone.Id, default)).ScheduledUnassignment);
        Assert.Single(await test.Db.Reservations.ToListAsync());
        await jobs.CleanupAsync(default);
        Assert.Equal(user, (await test.Service.PhoneAsync(phone.Id, default)).AssignedUser);
        test.Clock.Now = test.Clock.Now.AddDays(30);
        await jobs.CleanupAsync(default);
        var released = await test.Service.PhoneAsync(phone.Id, default);
        Assert.Null(released.AssignedUser);
        Assert.Equal(PhoneStatus.Available, released.Status);
    }

    [Fact]
    public async Task ReturnedUserCancelsOffboardingAndGraphFailuresDoNotRelease()
    {
        using var test = new TestDatabase();
        var user = Guid.NewGuid();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var phone = (await test.Service.NextAvailableAsync(range.Id, 1, default))[0];
        var teams = new FakeTeams { Assignments = [new(phone.LineUri, user)] };
        var jobs = new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock);
        await jobs.SynchronizeAsync(default);
        teams.State = DirectoryUserState.Missing;
        await jobs.SynchronizeAsync(default);
        test.Clock.Now = test.Clock.Now.AddDays(31);
        teams.Fail = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => jobs.CleanupAsync(default));
        Assert.Equal(user, (await test.Service.PhoneAsync(phone.Id, default)).AssignedUser);
        teams.Fail = false;
        teams.State = DirectoryUserState.Exists;
        await jobs.SynchronizeAsync(default);
        Assert.Empty(await test.Db.Reservations.ToListAsync());
        Assert.Null((await test.Service.PhoneAsync(phone.Id, default)).ScheduledUnassignment);
        Assert.Equal(PhoneStatus.InUse, (await test.Service.PhoneAsync(phone.Id, default)).Status);
    }

    [Fact]
    public async Task PatchCannotOverwriteAnInterveningUpdate()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(Input(), default);
        var version = range.Version;
        await test.Service.UpdateRangeAsync(range.Id, Input() with
        {
            Name = "Updated"
        }, default);
        await Assert.ThrowsAsync<Conflict>(() => test.Service.UpdateRangeAsync(range.Id, Input(), default, version));
        Assert.Equal("Updated", (await test.Service.RangeAsync(range.Id, default)).Name);
    }

    [Fact]
    public async Task DatabaseEnforcesOneReservationPerNumber()
    {
        using var test = new TestDatabase();
        await test.Service.CreateRangeAsync(Input(), default);
        var phone = await test.Db.PhoneNumbers.FirstAsync();
        test.Db.Reservations.Add(new(phone.Id, new ReservationDetails { ReservedFor = "system", System = ReservationSystem.Other }, test.Clock.Now));
        test.Db.Reservations.Add(new(phone.Id, new ReservationDetails { ReservedFor = "external", System = ReservationSystem.Other }, test.Clock.Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
    }
}
