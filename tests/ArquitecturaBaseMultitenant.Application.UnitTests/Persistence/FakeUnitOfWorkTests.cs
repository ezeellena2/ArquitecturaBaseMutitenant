using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Persistence;

public sealed class FakeUnitOfWorkTests
{
    private static readonly Error Rejected = Error.Conflict("Tests.Persistence.Rejected", "Rejected by the test.");

    [Theory]
    [InlineData(CommitPolicy.OnSuccess)]
    [InlineData(CommitPolicy.OnAnyResult)]
    public async Task Successful_result_commits_once(CommitPolicy policy)
    {
        var events = new List<string>();
        var unitOfWork = new FakeUnitOfWork(events);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            _ => Task.FromResult(Result.Success()), policy, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
        Assert.Equal(policy, unitOfWork.LastPolicy);
        Assert.Equal(["commit"], events);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Failed_result_rolls_back_with_OnSuccess()
    {
        var committed = false;
        var unitOfWork = new FakeUnitOfWork { OnCommit = () => committed = true };
        var failure = Result.Failure(Rejected);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            _ => Task.FromResult(failure), CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);

        Assert.Same(failure, result);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.False(committed);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Failed_result_commits_with_OnAnyResult()
    {
        var committed = false;
        var unitOfWork = new FakeUnitOfWork { OnCommit = () => committed = true };
        var failure = Result.Failure(Rejected);

        var result = await unitOfWork.ExecuteInTransactionAsync(
            _ => Task.FromResult(failure), CommitPolicy.OnAnyResult, TestContext.Current.CancellationToken);

        Assert.Same(failure, result);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
        Assert.True(committed);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Nested_boundary_throws_and_rolls_back_outer_boundary()
    {
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(
                ct => unitOfWork.ExecuteInTransactionAsync(
                    _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, ct),
                CommitPolicy.OnSuccess,
                TestContext.Current.CancellationToken));

        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Work_exception_rolls_back_and_restores_boundary()
    {
        var unitOfWork = new FakeUnitOfWork();
        var failure = new InvalidOperationException("work failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync<Result>(
                _ => throw failure, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Commit_exception_rolls_back_and_restores_boundary()
    {
        var failure = new InvalidOperationException("commit failed");
        var unitOfWork = new FakeUnitOfWork { CommitFailure = failure };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Invalid_arguments_are_rejected_before_opening_boundary()
    {
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            unitOfWork.ExecuteInTransactionAsync<Result>(null!, CommitPolicy.OnSuccess,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), (CommitPolicy)42,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, unitOfWork.Transactions);
        Assert.False(unitOfWork.InTransaction);
    }

    [Fact]
    public async Task Work_receives_the_callers_cancellation_token()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var unitOfWork = new FakeUnitOfWork();
        var observed = default(CancellationToken);

        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            observed = ct;
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, cancellation.Token);

        Assert.Equal(cancellation.Token, observed);
    }
}
