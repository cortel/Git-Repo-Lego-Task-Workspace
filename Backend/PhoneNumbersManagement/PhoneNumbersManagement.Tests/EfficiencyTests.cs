using Microsoft.EntityFrameworkCore;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Domain;
using PhoneNumbersManagement.Infrastructure;

namespace PhoneNumbersManagement.Tests;

public sealed class EfficiencyTests
{
    [Fact]
    public async Task ReadQueriesReturnDtosWithoutTrackingEntities()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var listed = await test.Service.RangesAsync(new(), default);
        var fetched = await test.Service.RangeAsync(range.Id, default);
        var phones = await test.Service.NextAvailableAsync(range.Id, 3, default);
        Assert.IsType<RangeResponse>(listed.Items.Single());
        Assert.Equal(range, fetched);
        Assert.All(phones, phone => Assert.IsType<PhoneResponse>(phone));
        Assert.Empty(test.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task UnmanagedAssignmentsDoNotQueryDirectoryOrStartWriteTransactions()
    {
        using var test = new TestDatabase();
        var teams = new FakeTeams { Assignments = [new("+4512345678", Guid.NewGuid())] };
        test.Work.Reset();
        await new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock).SynchronizeAsync(default);
        Assert.Empty(teams.UserLookups);
        Assert.Equal(0, test.Work.TransactionsStarted);
    }

    [Fact]
    public async Task SharedUserLookupIsReusedAndUnchangedAssignmentsAvoidWriteTransactions()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var phones = await test.Service.NextAvailableAsync(range.Id, 3, default);
        var user = Guid.NewGuid();
        var teams = new FakeTeams { Assignments = phones.Select(x => new TeamsAssignment(x.LineUri, user)).ToArray() };
        var job = new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock);
        await job.SynchronizeAsync(default);
        Assert.Single(teams.UserLookups);
        teams.UserLookups.Clear();
        test.Work.Reset();
        await job.SynchronizeAsync(default);
        Assert.Single(teams.UserLookups);
        Assert.Equal(0, test.Work.TransactionsStarted);
    }

    [Fact]
    public async Task ConflictingSnapshotIsRejectedBeforeAnyAssignmentWrite()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var phone = (await test.Service.NextAvailableAsync(range.Id, 1, default))[0];
        var teams = new FakeTeams { Assignments = [new(phone.LineUri, Guid.NewGuid()), new(phone.LineUri, Guid.NewGuid())] };
        test.Work.Reset();
        await Assert.ThrowsAsync<Conflict>(() => new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock).SynchronizeAsync(default));
        Assert.Empty(teams.UserLookups);
        Assert.Equal(0, test.Work.TransactionsStarted);
        Assert.Equal(PhoneStatus.Available, (await test.Service.PhoneAsync(phone.Id, default)).Status);
    }

    [Fact]
    public async Task AssignedCursorFiltersUnassignedRowsAndReturnsEachNumberOnce()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var phones = await test.Service.NextAvailableAsync(range.Id, 3, default);
        var user = Guid.NewGuid();
        foreach (var phone in new[] { phones[0], phones[2] })
        {
            await test.Service.UpdatePhoneAsync(phone.Id,
                new() { LineUri = phone.LineUri, RangeId = phone.RangeId, AssignedUser = user, Status = PhoneStatus.InUse }, default);
        }

        var store = new EfRegistryStore(test.Db);
        var first = await store.AssignedPhonesAfterAsync(0, 1, default);
        var second = await store.AssignedPhonesAfterAsync(first.Single().NumericValue, 1, default);
        var end = await store.AssignedPhonesAfterAsync(second.Single().NumericValue, 1, default);
        Assert.Equal(phones[0].Id, first.Single().Id);
        Assert.Equal(phones[2].Id, second.Single().Id);
        Assert.Empty(end);
    }

    [Fact]
    public async Task ObservationsAreNotReusedAcrossSynchronizationRunsOrCleanup()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var phone = (await test.Service.NextAvailableAsync(range.Id, 1, default))[0];
        var user = Guid.NewGuid();
        var teams = new FakeTeams { Assignments = [new(phone.LineUri, user)] };
        var job = new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock);
        await job.SynchronizeAsync(default);
        teams.State = DirectoryUserState.Missing;
        await job.SynchronizeAsync(default);
        Assert.Single(await test.Db.Reservations.ToListAsync());
        teams.State = DirectoryUserState.Exists;
        test.Clock.Now = test.Clock.Now.AddDays(31);
        teams.UserLookups.Clear();
        await job.CleanupAsync(default);
        Assert.Single(teams.UserLookups);
        Assert.Equal(user, (await test.Service.PhoneAsync(phone.Id, default)).AssignedUser);
    }

    [Fact]
    public async Task AssignedScanContinuesPastTheBatchBoundary()
    {
        using var test = new TestDatabase();
        await test.Service.CreateRangeAsync(RegistryTests.Input(end: "+4512346200"), default);
        var phones = await test.Db.PhoneNumbers.OrderBy(x => x.NumericValue).ToListAsync();
        var firstUser = Guid.NewGuid();
        var lastUser = Guid.NewGuid();
        foreach (var phone in phones)
        {
            phone.Assign(phone == phones[^1] ? lastUser : firstUser);
        }

        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        test.Work.Reset();
        var teams = new FakeTeams();
        await new ReconciliationService(new EfRegistryStore(test.Db), teams, test.Clock).SynchronizeAsync(default);
        Assert.Equal(601, phones.Count);
        Assert.Equal(2, teams.UserLookups.Count);
        Assert.Contains(lastUser, teams.UserLookups);
        Assert.Equal(0, test.Work.TransactionsStarted);
    }

    [Fact]
    public async Task OldObservationsAreRefreshedWithinLongSynchronizationRuns()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var phones = await test.Service.NextAvailableAsync(range.Id, 2, default);
        var user = Guid.NewGuid();
        var teams = new FakeTeams { Assignments = phones.Select(x => new TeamsAssignment(x.LineUri, user)).ToArray() };
        await new ReconciliationService(new EfRegistryStore(test.Db), teams, new AdvancingTimeProvider()).SynchronizeAsync(default);

        // Each observation is older than the reuse window when the next lookup occurs.
        Assert.Equal(4, teams.UserLookups.Count);
    }

    [Fact]
    public async Task ResponseDtosPreserveTheExistingJsonFields()
    {
        using var test = new TestDatabase();
        var range = await test.Service.CreateRangeAsync(RegistryTests.Input(), default);
        var entity = await test.Db.Ranges.AsNoTracking().SingleAsync(x => x.Id == range.Id);
        var entityJson = System.Text.Json.JsonSerializer.SerializeToNode(entity);
        var responseJson = System.Text.Json.JsonSerializer.SerializeToNode(range);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(entityJson, responseJson));
        var phone = await test.Db.PhoneNumbers.AsNoTracking().FirstAsync();
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.JsonSerializer.SerializeToNode(phone),
            System.Text.Json.JsonSerializer.SerializeToNode(await test.Service.PhoneAsync(phone.Id, default))));
        var reservation = await test.Service.CreateReservationAsync(new()
        {
            PhoneNumberId = phone.Id,
            ReservedFor = "system",
            System = ReservationSystem.Other
        }, default);
        var reservationEntity = await test.Db.Reservations.AsNoTracking().SingleAsync();
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.JsonSerializer.SerializeToNode(reservationEntity),
            System.Text.Json.JsonSerializer.SerializeToNode(reservation)));
    }
}
