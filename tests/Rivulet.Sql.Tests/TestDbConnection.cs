using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Sql.Tests;

public class TestDbConnection(
    Func<IDbCommand, object?>? executeScalarFunc = null,
    Func<IDbCommand, int>? executeNonQueryFunc = null,
    Func<IDbCommand, IDataReader>? executeReaderFunc = null)
    : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    [AllowNull]
    public override string ConnectionString { get; set; }
    public override string Database => "TestDatabase";
    public override string DataSource => "TestDataSource";
    public override string ServerVersion => "1.0.0";
    public override ConnectionState State => _state;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        return new TestDbTransaction(this, isolationLevel);
    }

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    protected override DbCommand CreateDbCommand()
    {
        return new TestDbCommand(executeScalarFunc, executeNonQueryFunc, executeReaderFunc);
    }
}

public class TestDbCommand(
    Func<IDbCommand, object?>? executeScalarFunc,
    Func<IDbCommand, int>? executeNonQueryFunc,
    Func<IDbCommand, IDataReader>? executeReaderFunc)
    : DbCommand
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

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        return executeNonQueryFunc?.Invoke(this) ?? 0;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ExecuteNonQuery());
    }

    public override object? ExecuteScalar()
    {
        return executeScalarFunc?.Invoke(this);
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ExecuteScalar());
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter()
    {
        return new TestDbParameter();
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var reader = executeReaderFunc?.Invoke(this) ?? new TestDataReader([]);
        return (DbDataReader)reader;
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        return Task.FromResult(ExecuteDbDataReader(behavior));
    }
}

public class TestDbTransaction(TestDbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public override IsolationLevel IsolationLevel { get; } = isolationLevel;
    protected override DbConnection DbConnection => connection;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public override void Commit()
    {
        IsCommitted = true;
    }

    public override void Rollback()
    {
        IsRolledBack = true;
    }
}

public class TestDbParameterCollection : DbParameterCollection
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
        foreach (var value in values)
        {
            _parameters.Add(value);
        }
    }

    public override void Clear()
    {
        _parameters.Clear();
    }

    public override bool Contains(object value)
    {
        return _parameters.Contains(value);
    }

    public override bool Contains(string value)
    {
        return false;
    }

    public override void CopyTo(Array array, int index)
    {
        _parameters.CopyTo((object[])array, index);
    }

    public override IEnumerator GetEnumerator()
    {
        return _parameters.GetEnumerator();
    }

    public override int IndexOf(object value)
    {
        return _parameters.IndexOf(value);
    }

    public override int IndexOf(string parameterName)
    {
        return -1;
    }

    public override void Insert(int index, object value)
    {
        _parameters.Insert(index, value);
    }

    public override void Remove(object value)
    {
        _parameters.Remove(value);
    }

    public override void RemoveAt(int index)
    {
        _parameters.RemoveAt(index);
    }

    public override void RemoveAt(string parameterName)
    {
    }

    protected override DbParameter GetParameter(int index)
    {
        return (DbParameter)_parameters[index];
    }

    protected override DbParameter GetParameter(string parameterName)
    {
        return new TestDbParameter();
    }

    protected override void SetParameter(int index, DbParameter value)
    {
        _parameters[index] = value;
    }

    protected override void SetParameter(string parameterName, DbParameter value)
    {
    }
}

public class TestDbParameter : DbParameter
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

    public override void ResetDbType()
    {
    }
}

public class TestDataReader(List<Dictionary<string, object>> rows) : DbDataReader
{
    private int _currentRow = -1;
    private bool _isClosed;

    public override int FieldCount => _currentRow >= 0 && _currentRow < rows.Count ? rows[_currentRow].Count : 0;
    public override bool HasRows => rows.Count > 0;
    public override bool IsClosed => _isClosed;
    public override int RecordsAffected => rows.Count;
    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
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

    public override string GetName(int ordinal)
    {
        if (_currentRow < 0 || _currentRow >= rows.Count)
        {
            throw new InvalidOperationException("No current row");
        }

        return rows[_currentRow].Keys.ElementAt(ordinal);
    }

    public override int GetOrdinal(string name)
    {
        if (_currentRow < 0 || _currentRow >= rows.Count)
        {
            throw new InvalidOperationException("No current row");
        }

        var keys = rows[_currentRow].Keys.ToList();
        return keys.IndexOf(name);
    }

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override object GetValue(int ordinal)
    {
        if (_currentRow < 0 || _currentRow >= rows.Count)
        {
            throw new InvalidOperationException("No current row");
        }

        return rows[_currentRow].Values.ElementAt(ordinal);
    }

    public override int GetValues(object[] values)
    {
        if (_currentRow < 0 || _currentRow >= rows.Count)
        {
            return 0;
        }

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

    public override void Close()
    {
        _isClosed = true;
    }
}
