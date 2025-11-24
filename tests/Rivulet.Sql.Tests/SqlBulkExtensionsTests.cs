using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Sql.Tests;

public class SqlBulkExtensionsTests
{
    [Fact]
    public async Task BulkInsertAsync_WithValidItems_ShouldInsertAllBatches()
    {
        var items = Enumerable.Range(1, 2500).Select(i => new { Id = i, Name = $"User{i}" }).ToList();
        var totalAffectedRows = 0;

        var result = await items.BulkInsertAsync(
            () => new TestDbConnection(executeNonQueryFunc: cmd =>
            {
                var batchSize = cmd.CommandText.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
                totalAffectedRows += batchSize;
                return batchSize;
            }),
            async (batch, cmd, _) =>
            {
                cmd.CommandText = string.Join(";", batch.Select(item =>
                    $"INSERT INTO Users (Id, Name) VALUES ({item.Id}, '{item.Name}')"));
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 1000
            });

        result.Should().BeGreaterThan(0);
        totalAffectedRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkInsertAsync_WithNullItems_ShouldThrowArgumentNullException()
    {
        IEnumerable<object> items = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await items.BulkInsertAsync(
                () => new TestDbConnection(),
                (_, _, _) => ValueTask.CompletedTask));
    }

    [Fact]
    public async Task BulkInsertAsync_WithNullConnectionFactory_ShouldThrowArgumentNullException()
    {
        var items = new[] { 1, 2, 3 };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await items.BulkInsertAsync(
                null!,
                (_, _, _) => ValueTask.CompletedTask));
    }

    [Fact]
    public async Task BulkInsertAsync_WithNullCommandBuilder_ShouldThrowArgumentNullException()
    {
        var items = new[] { 1, 2, 3 };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await items.BulkInsertAsync(
                () => new TestDbConnection(),
                null!));
    }

    [Fact]
    public async Task BulkUpdateAsync_WithValidItems_ShouldUpdateAllBatches()
    {
        var items = Enumerable.Range(1, 1500).Select(i => new { Id = i, Name = $"UpdatedUser{i}" }).ToList();
        var batchesExecuted = 0;

        var result = await items.BulkUpdateAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                Interlocked.Increment(ref batchesExecuted);
                return 500;
            }),
            async (batch, cmd, _) =>
            {
                cmd.CommandText = string.Join(";", batch.Select(item =>
                    $"UPDATE Users SET Name = '{item.Name}' WHERE Id = {item.Id}"));
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 500
            });

        result.Should().Be(1500);
        batchesExecuted.Should().Be(3);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithValidItems_ShouldDeleteAllBatches()
    {
        var ids = Enumerable.Range(1, 2000).ToList();
        var result = await ids.BulkDeleteAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 1000),
            async (batch, cmd, _) =>
            {
                cmd.CommandText = $"DELETE FROM Users WHERE Id IN ({string.Join(",", batch)})";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 1000
            });

        result.Should().Be(2000);
    }

    [Fact]
    public async Task BulkInsertAsync_WithTransactions_ShouldUseTransactions()
    {
        var items = Enumerable.Range(1, 100).ToList();
        TestDbTransaction? capturedTransaction = null;

        var result = await items.BulkInsertAsync(
            () =>
            {
                var conn = new TestDbConnection(executeNonQueryFunc: cmd =>
                {
                    capturedTransaction = cmd.Transaction as TestDbTransaction;
                    return 100;
                });
                return conn;
            },
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                UseTransaction = true,
                BatchSize = 100
            });

        result.Should().Be(100);
        capturedTransaction.Should().NotBeNull();
        capturedTransaction!.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAsync_WithTransactionAndError_ShouldRollback()
    {
        var items = Enumerable.Range(1, 100).ToList();
        TestDbTransaction? capturedTransaction = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await items.BulkInsertAsync(
                () =>
                {
                    var conn = new TestDbConnection(executeNonQueryFunc: cmd =>
                    {
                        capturedTransaction = cmd.Transaction as TestDbTransaction;
                        throw new InvalidOperationException("Insert failed");
                    });
                    return conn;
                },
                async (_, cmd, _) =>
                {
                    cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                    await Task.CompletedTask;
                },
                new()
                {
                    UseTransaction = true,
                    BatchSize = 100,
                    SqlOptions = new()
                    {
                        ParallelOptions = new()
                        {
                            MaxRetries = 0
                        }
                    }
                }));

        capturedTransaction.Should().NotBeNull();
        capturedTransaction!.IsRolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAsync_WithBatchCallbacks_ShouldInvokeCallbacks()
    {
        var items = Enumerable.Range(1, 300).ToList();
        var batchStarted = new List<int>();
        var batchCompleted = new List<int>();
        var lockObj = new object();

        var result = await items.BulkInsertAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 100),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 100,
                OnBatchStartAsync = (_, batchNum) =>
                {
                    lock (lockObj) { batchStarted.Add(batchNum); }
                    return ValueTask.CompletedTask;
                },
                OnBatchCompleteAsync = (_, batchNum, _) =>
                {
                    lock (lockObj) { batchCompleted.Add(batchNum); }
                    return ValueTask.CompletedTask;
                }
            });

        result.Should().Be(300);
        batchStarted.Should().HaveCount(3);
        batchCompleted.Should().HaveCount(3);
        batchStarted.Should().Contain([0, 1, 2]);
        batchCompleted.Should().Contain([0, 1, 2]);
    }

    [Fact]
    public async Task BulkInsertAsync_WithBatchError_ShouldInvokeErrorCallback()
    {
        var items = Enumerable.Range(1, 100).ToList();
        var errorBatchNumber = -1;
        Exception? errorException = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await items.BulkInsertAsync(
                () => new TestDbConnection(executeNonQueryFunc: _ => throw new InvalidOperationException("Batch failed")),
                async (_, cmd, _) =>
                {
                    cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                    await Task.CompletedTask;
                },
                new()
                {
                    BatchSize = 100,
                    OnBatchErrorAsync = (_, batchNum, ex) =>
                    {
                        errorBatchNumber = batchNum;
                        errorException = ex;
                        return ValueTask.CompletedTask;
                    },
                    SqlOptions = new()
                    {
                        ParallelOptions = new()
                        {
                            MaxRetries = 0
                        }
                    }
                }));

        errorBatchNumber.Should().Be(0);
        errorException.Should().NotBeNull();
        errorException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkInsertAsync_WithCustomBatchSize_ShouldCreateCorrectNumberOfBatches()
    {
        var items = Enumerable.Range(1, 2500).ToList();
        var batchesExecuted = 0;

        var result = await items.BulkInsertAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                Interlocked.Increment(ref batchesExecuted);
                return 250;
            }),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 250
            });

        result.Should().BeGreaterThan(0);
        batchesExecuted.Should().Be(10);
    }

    [Fact]
    public async Task BulkInsertAsync_WithParallelExecution_ShouldRespectConcurrency()
    {
        var items = Enumerable.Range(1, 2000).ToList();
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var result = await items.BulkInsertAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                }

                Task.Delay(50, CancellationToken.None).Wait();

                lock (lockObj)
                {
                    currentConcurrent--;
                }

                return 500;
            }),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 500,
                SqlOptions = new()
                {
                    ParallelOptions = new()
                    {
                        MaxDegreeOfParallelism = 3
                    }
                }
            });

        result.Should().BeGreaterThan(0);
        maxConcurrent.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task BulkInsertAsync_WithIsolationLevel_ShouldUseCorrectIsolation()
    {
        var items = Enumerable.Range(1, 100).ToList();
        IsolationLevel? capturedIsolationLevel = null;

        var result = await items.BulkInsertAsync(
            () =>
            {
                var conn = new TestDbConnection(executeNonQueryFunc: cmd =>
                {
                    capturedIsolationLevel = (cmd.Transaction as TestDbTransaction)?.IsolationLevel;
                    return 100;
                });
                return conn;
            },
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                UseTransaction = true,
                BatchSize = 100,
                SqlOptions = new()
                {
                    IsolationLevel = IsolationLevel.Serializable
                }
            });

        result.Should().Be(100);
        capturedIsolationLevel.Should().Be(IsolationLevel.Serializable);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithRetryOnTransientError_ShouldRetry()
    {
        var items = Enumerable.Range(1, 100).ToList();
        var attemptCount = 0;

        var result = await items.BulkUpdateAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new TimeoutException("Timeout occurred");
                }

                return 100;
            }),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "UPDATE Users SET Name = 'Updated'";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 100,
                SqlOptions = new()
                {
                    ParallelOptions = new()
                    {
                        MaxRetries = 3,
                        BaseDelay = TimeSpan.FromMilliseconds(10)
                    }
                }
            });

        attemptCount.Should().Be(2);
        result.Should().Be(100);
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task BulkInsertAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        var items = Enumerable.Range(1, 10000).ToList();
        using var cts = new CancellationTokenSource();

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await items.BulkInsertAsync(
                () => new TestDbConnection(executeNonQueryFunc: _ =>
                {
                    Task.Delay(200, cts.Token).Wait(cts.Token);
                    return 1000;
                }),
                async (_, cmd, _) =>
                {
                    cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                    await Task.CompletedTask;
                },
                new()
                {
                    BatchSize = 1000
                },
                cts.Token));
    }

    [Fact]
    public async Task BulkInsertAsync_WithNullOptions_ShouldUseDefaultOptions()
    {
        var items = Enumerable.Range(1, 10).ToList();

        var result = await items.BulkInsertAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 10),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            options: null);

        result.Should().Be(10);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithNullOptions_ShouldUseDefaultOptions()
    {
        var items = Enumerable.Range(1, 10).ToList();

        var result = await items.BulkUpdateAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 10),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "UPDATE Users SET Name = 'Updated'";
                await Task.CompletedTask;
            },
            options: null);

        result.Should().Be(10);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithNullOptions_ShouldUseDefaultOptions()
    {
        var items = Enumerable.Range(1, 10).ToList();

        var result = await items.BulkDeleteAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 10),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "DELETE FROM Users WHERE Id IN (...)";
                await Task.CompletedTask;
            },
            options: null);

        result.Should().Be(10);
    }

    [Fact]
    public async Task BulkInsertAsync_WithAutoManageConnection_ShouldCloseConnectionAfterBatch()
    {
        var items = Enumerable.Range(1, 10).ToList();
        TestDbConnection? capturedConnection = null;

        var result = await items.BulkInsertAsync(
            () =>
            {
                var conn = new TestDbConnection(executeNonQueryFunc: _ => 10);
                capturedConnection = conn;
                return conn;
            },
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 10,
                SqlOptions = new()
                {
                    AutoManageConnection = true
                }
            });

        result.Should().Be(10);
        capturedConnection.Should().NotBeNull();
        capturedConnection!.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task BulkInsertAsync_WithNonDbConnection_ShouldUseTaskRunFallback()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var openCalled = false;

        var result = await items.BulkInsertAsync(
            () => new NonDbConnectionMock(() => openCalled = true, () => 10),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 10
            });

        result.Should().Be(10);
        openCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAsync_WithNonDbCommand_ShouldUseTaskRunFallback()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var executeNonQueryCalled = false;

        var result = await items.BulkInsertAsync(
            () => new NonDbConnectionWithNonDbCommandMock(() =>
            {
                executeNonQueryCalled = true;
                return 10;
            }),
            async (_, cmd, _) =>
            {
                cmd.CommandText = "INSERT INTO Users (Id) VALUES (...)";
                await Task.CompletedTask;
            },
            new()
            {
                BatchSize = 10
            });

        result.Should().Be(10);
        executeNonQueryCalled.Should().BeTrue();
    }

    // Mock IDbConnection that does NOT extend DbConnection (for lines 219, 225)
    private class NonDbConnectionMock(Action onOpen, Func<int> executeNonQueryFunc) : IDbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "TestDB";
        public ConnectionState State => _state;

        public void Open()
        {
            _state = ConnectionState.Open;
            onOpen();
        }

        public void Close()
        {
            _state = ConnectionState.Closed;
        }

        public IDbTransaction BeginTransaction() => new NonDbTransactionMock();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new NonDbTransactionMock();
        public void ChangeDatabase(string databaseName) { }

        public IDbCommand CreateCommand() => new NonDbCommandMock(executeNonQueryFunc);

        public void Dispose()
        {
            _state = ConnectionState.Closed;
        }
    }

    // Mock IDbCommand that does NOT extend DbCommand (for lines 231, 236)
    private class NonDbCommandMock(Func<int> executeNonQueryFunc) : IDbCommand
    {
        [AllowNull]
        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new NonDbParameterCollectionMock();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public int ExecuteNonQuery() => executeNonQueryFunc();
        public IDataReader ExecuteReader() => throw new NotImplementedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();
        public object ExecuteScalar() => throw new NotImplementedException();
        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotImplementedException();
        public void Prepare() { }
        public void Dispose() { }
    }

    private class NonDbTransactionMock : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }

    private class NonDbParameterCollectionMock : IDataParameterCollection
    {
        private readonly List<object?> _items = [];
        public object this[string parameterName] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object? this[int index] { get => _items[index]; set => _items[index] = value; }
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public int Count => _items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public int Add(object? value) { _items.Add(value); return _items.Count - 1; }
        public void Clear() => _items.Clear();
        public bool Contains(object? value) => _items.Contains(value);
        public bool Contains(string parameterName) => throw new NotImplementedException();
        public void CopyTo(Array array, int index) => throw new NotImplementedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(object? value) => _items.IndexOf(value);
        public int IndexOf(string parameterName) => throw new NotImplementedException();
        public void Insert(int index, object? value) => _items.Insert(index, value);
        public void Remove(object? value) => _items.Remove(value);
        public void RemoveAt(int index) => _items.RemoveAt(index);
        public void RemoveAt(string parameterName) => throw new NotImplementedException();
    }

    // Mock that uses NonDbCommandMock for testing line 236
    private class NonDbConnectionWithNonDbCommandMock(Func<int> executeNonQueryFunc) : IDbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "TestDB";
        public ConnectionState State => _state;

        public void Open() => _state = ConnectionState.Open;
        public void Close() => _state = ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => new NonDbTransactionMock();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new NonDbTransactionMock();
        public void ChangeDatabase(string databaseName) { }
        public IDbCommand CreateCommand() => new NonDbCommandMock(executeNonQueryFunc);
        public void Dispose() => _state = ConnectionState.Closed;
    }
}
