using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Base.Tests;

public sealed class TestDbConnection(
    Func<IDbCommand, object?>? executeScalarFunc = null,
    Func<IDbCommand, int>? executeNonQueryFunc = null,
    Func<IDbCommand, IDataReader>? executeReaderFunc = null
) : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    [AllowNull]
    public override string ConnectionString { get; set; }

    public override string Database => "TestDatabase";
    public override string DataSource => "TestDataSource";
    public override string ServerVersion => "1.0.0";

    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override ConnectionState State => _state;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        new TestDbTransaction(this, isolationLevel);

    public override void ChangeDatabase(string databaseName) { }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    protected override DbCommand CreateDbCommand() =>
        new TestDbCommand(executeScalarFunc, executeNonQueryFunc, executeReaderFunc);
}

internal sealed class TestDbCommand(
    Func<IDbCommand, object?>? executeScalarFunc,
    Func<IDbCommand, int>? executeNonQueryFunc,
    Func<IDbCommand, IDataReader>? executeReaderFunc
) : DbCommand
{
    [AllowNull]
    public override string CommandText { get; set; }

    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; } = new TestDbParameterCollection();

    public override void Cancel() { }

    public override int ExecuteNonQuery() => executeNonQueryFunc?.Invoke(this) ?? 0;

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteNonQuery());

    public override object? ExecuteScalar() => executeScalarFunc?.Invoke(this);

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteScalar());

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new TestDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var reader = executeReaderFunc?.Invoke(this) ?? new TestDataReader([]);
        return (DbDataReader)reader;
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
        CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteDbDataReader(behavior));
}

public sealed class TestDbTransaction(DbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public override IsolationLevel IsolationLevel { get; } = isolationLevel;
    protected override DbConnection DbConnection => connection;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public override void Commit() => IsCommitted = true;

    public override void Rollback() => IsRolledBack = true;
}

internal sealed class TestDbParameterCollection : DbParameterCollection
{
    private readonly List<object> _parameters = [];

    public override int Count => _parameters.Count;
    public override object SyncRoot => _parameters;

    public override int Add(object value)
    {
        _parameters.Add(value);
        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values) _parameters.Add(value);
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains(value);

    public override bool Contains(string value) => false;

    public override void CopyTo(Array array, int index) => _parameters.CopyTo((object[])array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf(value);

    public override int IndexOf(string parameterName) => -1;

    public override void Insert(int index, object value) => _parameters.Insert(index, value);

    public override void Remove(object value) => _parameters.Remove(value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName) { }

    protected override DbParameter GetParameter(int index) => (DbParameter)_parameters[index];

    protected override DbParameter GetParameter(string parameterName) => new TestDbParameter();

    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) { }
}

internal sealed class TestDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }

    public override void ResetDbType() { }
}

public sealed class TestDataReader(List<Dictionary<string, object>> rows) : DbDataReader
{
    private int _currentRow = -1;
    private bool _isClosed;

    public override int FieldCount => rows.Count > 0 ? rows[0].Count : 0;
    public override bool HasRows => rows.Count > 0;

    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => rows.Count;
    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal,
        long dataOffset,
        byte[]? buffer,
        int bufferOffset,
        int length) => 0;

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal,
        long dataOffset,
        char[]? buffer,
        int bufferOffset,
        int length) => 0;

    public override string GetDataTypeName(int ordinal) => GetValue(ordinal).GetType().Name;
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override Type GetFieldType(int ordinal) => GetValue(ordinal).GetType();
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetName(int ordinal) =>
        rows.Count == 0 ? throw new InvalidOperationException("No rows available") : rows[0].Keys.ElementAt(ordinal);

    public override int GetOrdinal(string name)
    {
        if (rows.Count == 0) throw new InvalidOperationException("No rows available");

        var keys = rows[0].Keys.ToList();
        return keys.IndexOf(name);
    }

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override object GetValue(int ordinal) =>
        _currentRow < 0 || _currentRow >= rows.Count
            ? throw new InvalidOperationException("No current row")
            : rows[_currentRow].Values.ElementAt(ordinal);

    public override int GetValues(object[] values)
    {
        if (_currentRow < 0 || _currentRow >= rows.Count) return 0;

        var rowValues = rows[_currentRow].Values.ToArray();
        var count = Math.Min(values.Length, rowValues.Length);
        Array.Copy(rowValues, values, count);
        return count;
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;

    public override bool NextResult() => false;

    public override bool Read()
    {
        _currentRow++;
        return _currentRow < rows.Count;
    }

    public override IEnumerator GetEnumerator() => rows.GetEnumerator();

    public override void Close() => _isClosed = true;

    public override DataTable? GetSchemaTable()
    {
        if (rows.Count == 0) return null;

        var schemaTable = new DataTable("SchemaTable");
        schemaTable.Columns.Add("ColumnName", typeof(string));
        schemaTable.Columns.Add("ColumnOrdinal", typeof(int));
        schemaTable.Columns.Add("ColumnSize", typeof(int));
        schemaTable.Columns.Add("DataType", typeof(Type));

        var ordinal = 0;
        foreach (var key in rows[0].Keys)
        {
            var row = schemaTable.NewRow();
            row["ColumnName"] = key;
            row["ColumnOrdinal"] = ordinal++;
            row["ColumnSize"] = -1;
            row["DataType"] = rows[0][key].GetType();
            schemaTable.Rows.Add(row);
        }

        return schemaTable;
    }
}

/// <summary>
///     A test connection that implements IDbConnection directly (not DbConnection)
///     to test the Task.Run fallback paths in SqlParallelExtensions.
/// </summary>
public sealed class NonDbConnection(
    Func<IDbCommand, object?>? executeScalarFunc = null,
    Func<IDbCommand, int>? executeNonQueryFunc = null,
    Func<IDbCommand, IDataReader>? executeReaderFunc = null
) : IDbConnection
{
    [AllowNull]
    public string ConnectionString { get; set; } = string.Empty;

    public int ConnectionTimeout => 30;
    public string Database => "TestDatabase";
    public ConnectionState State { get; private set; } = ConnectionState.Closed;

    public IDbTransaction BeginTransaction() =>
        new TestDbTransaction(new TestDbConnection(), IsolationLevel.Unspecified);

    public IDbTransaction BeginTransaction(IsolationLevel il) => new TestDbTransaction(new TestDbConnection(), il);
    public void ChangeDatabase(string databaseName) { }

    public void Close() => State = ConnectionState.Closed;

    public void Open() => State = ConnectionState.Open;

    public IDbCommand CreateCommand() => new NonDbCommand(executeScalarFunc, executeNonQueryFunc, executeReaderFunc);

    public void Dispose() => State = ConnectionState.Closed;
}

/// <summary>
///     A test command that implements IDbCommand directly (not DbCommand)
///     to test the Task.Run fallback paths in SqlParallelExtensions.
/// </summary>
internal sealed class NonDbCommand(
    Func<IDbCommand, object?>? executeScalarFunc,
    Func<IDbCommand, int>? executeNonQueryFunc,
    Func<IDbCommand, IDataReader>? executeReaderFunc
) : IDbCommand
{
    [AllowNull]
    public string CommandText { get; set; } = string.Empty;

    public int CommandTimeout { get; set; } = 30;
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }
    public IDataParameterCollection Parameters { get; } = new TestDbParameterCollection();

    public void Cancel() { }

    public IDbDataParameter CreateParameter() => new TestDbParameter();

    public int ExecuteNonQuery() => executeNonQueryFunc?.Invoke(this) ?? 0;

    public IDataReader ExecuteReader() => executeReaderFunc?.Invoke(this) ?? new TestDataReader([]);

    public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();

    public object? ExecuteScalar() => executeScalarFunc?.Invoke(this);

    public void Prepare() { }

    public void Dispose() { }
}