using System.Data;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;

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

        result.ShouldBeGreaterThan(0);
        totalAffectedRows.ShouldBeGreaterThan(0);
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

        result.ShouldBe(1500);
        batchesExecuted.ShouldBe(3);
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

        result.ShouldBe(2000);
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

        result.ShouldBe(100);
        capturedTransaction.ShouldNotBeNull();
        capturedTransaction!.IsCommitted.ShouldBeTrue();
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

        capturedTransaction.ShouldNotBeNull();
        capturedTransaction!.IsRolledBack.ShouldBeTrue();
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

        result.ShouldBe(300);
        batchStarted.Count.ShouldBe(3);
        batchCompleted.Count.ShouldBe(3);
        batchStarted.ShouldContain(0);
        batchStarted.ShouldContain(1);
        batchStarted.ShouldContain(2);
        batchCompleted.ShouldContain(0);
        batchCompleted.ShouldContain(1);
        batchCompleted.ShouldContain(2);
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

        errorBatchNumber.ShouldBe(0);
        errorException.ShouldNotBeNull();
        errorException.ShouldBeOfType<InvalidOperationException>();
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

        result.ShouldBeGreaterThan(0);
        batchesExecuted.ShouldBe(10);
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

        result.ShouldBeGreaterThan(0);
        maxConcurrent.ShouldBeLessThanOrEqualTo(3);
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

        result.ShouldBe(100);
        capturedIsolationLevel.ShouldBe(IsolationLevel.Serializable);
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

        attemptCount.ShouldBe(2);
        result.ShouldBe(100);
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

        result.ShouldBe(10);
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

        result.ShouldBe(10);
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

        result.ShouldBe(10);
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

        result.ShouldBe(10);
        capturedConnection.ShouldNotBeNull();
        capturedConnection!.State.ShouldBe(ConnectionState.Closed);
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

        result.ShouldBe(10);
        openCalled.ShouldBeTrue();
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

        result.ShouldBe(10);
        executeNonQueryCalled.ShouldBeTrue();
    }

    // Mock IDbConnection that does NOT extend DbConnection
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

    // Mock IDbCommand that does NOT extend DbCommand
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

    // Mock that uses NonDbCommandMock for testing
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
