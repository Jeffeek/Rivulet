using Microsoft.Data.SqlClient;
using Rivulet.Core;

Console.WriteLine("=== Rivulet.Sql Sample ===\n");
Console.WriteLine("NOTE: Configure connection string before running!\n");

// Configure your database connection
const string connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;TrustServerCertificate=true";

try
{
    // Sample 1: Execute multiple queries in parallel
    Console.WriteLine("1. SelectParallelAsync - Execute multiple queries");

    var queries = new[]
    {
        "SELECT COUNT(*) FROM Users",
        "SELECT COUNT(*) FROM Orders",
        "SELECT COUNT(*) FROM Products"
    };

    var results = await queries.SelectParallelAsync(
        async (query, ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(query, connection);
            var count = (int?)await command.ExecuteScalarAsync(ct);
            return (query, count);
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3,
            ErrorMode = ErrorMode.BestEffort
        });

    Console.WriteLine($"✓ Executed {results.Count} queries in parallel");
    foreach (var (query, count) in results)
    {
        Console.WriteLine($"  {query}: {count} rows");
    }
    Console.WriteLine();

    // Sample 2: Parameterized queries in parallel
    Console.WriteLine("2. Parameterized queries - Safe SQL execution");

    var userIds = Enumerable.Range(1, 10).ToList();

    var users = await userIds.SelectParallelAsync(
        async (userId, ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(
                "SELECT Id, Name FROM Users WHERE Id = @UserId",
                connection);
            command.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new { Id = reader.GetInt32(0), Name = reader.GetString(1) };
            }
            return null;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            ErrorMode = ErrorMode.BestEffort
        });

    Console.WriteLine($"✓ Retrieved {users.Count(u => u != null)} user records\n");

    // Sample 3: Parallel INSERT operations
    Console.WriteLine("3. Parallel INSERT operations - Execute many inserts");

    var dataToInsert = Enumerable.Range(1, 20)
        .Select(i => new { Id = i + 1000, Name = $"NewUser{i}", Email = $"newuser{i}@test.com" })
        .ToList();

    var insertResults = await dataToInsert.SelectParallelAsync(
        async (data, ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(
                "INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
                connection);
            command.Parameters.AddWithValue("@Id", data.Id);
            command.Parameters.AddWithValue("@Name", data.Name);
            command.Parameters.AddWithValue("@Email", data.Email);

            var rowsAffected = await command.ExecuteNonQueryAsync(ct);
            return (data.Id, rowsAffected);
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2
        });

    Console.WriteLine($"✓ Executed {insertResults.Count} INSERT statements\n");

    // Sample 4: Parallel UPDATE operations
    Console.WriteLine("4. Parallel UPDATE operations - Update multiple records");

    var idsToUpdate = Enumerable.Range(1, 10).ToList();

    var updateResults = await idsToUpdate.SelectParallelAsync(
        async (id, ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(
                "UPDATE Users SET LastLogin = GETDATE() WHERE Id = @Id",
                connection);
            command.Parameters.AddWithValue("@Id", id);

            var rowsAffected = await command.ExecuteNonQueryAsync(ct);
            return (id, rowsAffected);
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5
        });

    Console.WriteLine($"✓ Executed {updateResults.Count} UPDATE statements\n");

    Console.WriteLine("=== Sample Complete ===");
    Console.WriteLine("\nKey Features:");
    Console.WriteLine("  - Execute multiple SQL operations in parallel");
    Console.WriteLine("  - Supports SELECT, INSERT, UPDATE, DELETE");
    Console.WriteLine("  - Parameterized queries for SQL injection protection");
    Console.WriteLine("  - Automatic retries and error handling");
    Console.WriteLine("  - Works with any ADO.NET provider");
}
catch (SqlException ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
    Console.WriteLine("   Please configure a valid connection string and ensure the database exists.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}
