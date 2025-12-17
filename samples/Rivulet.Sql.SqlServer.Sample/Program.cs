using System.Data;
using Microsoft.Data.SqlClient;
using Rivulet.Core;
using Rivulet.Sql.SqlServer;

// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleLiteral

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

Console.WriteLine("=== Rivulet.Sql.SqlServer Sample ===\n");
Console.WriteLine("NOTE: Configure connection string before running!\n");

// Configure your SQL Server connection
const string connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;TrustServerCertificate=true";

try
{
    // Sample 1: SqlBulkCopy for ultra-fast inserts
    Console.WriteLine("1. BulkInsertUsingSqlBulkCopyAsync - Ultra-fast bulk inserts (10-100x faster)");

    // Create sample data - 10,000 user records
    var users = Enumerable.Range(1, 10000)
        .Select(static i => new
        {
            Id = i,
            Name = $"User{i}",
            Email = $"user{i}@test.com",
            CreatedAt = DateTime.UtcNow
        })
        .ToList();

    var startTime = DateTime.UtcNow;

    await users.BulkInsertUsingSqlBulkCopyAsync(
        static () => new SqlConnection(connectionString),
        "Users",
        static batch =>
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(int));
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Email", typeof(string));
            dataTable.Columns.Add("CreatedAt", typeof(DateTime));

            foreach (var user in batch) dataTable.Rows.Add(user.Id, user.Name, user.Email, user.CreatedAt);

            return dataTable;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort
        },
        batchSize: 5000);

    var elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {users.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {users.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 2: Bulk insert with custom column mappings
    Console.WriteLine("2. BulkInsert with custom column mappings");

    var columnMappings = new Dictionary<string, string>
    {
        ["Id"] = "UserId",
        ["Name"] = "FullName",
        ["Email"] = "EmailAddress"
    };

    var customUsers = Enumerable.Range(1, 1000)
        .Select(static i => new
        {
            Id = i + 10000,
            Name = $"CustomUser{i}",
            Email = $"custom{i}@test.com"
        })
        .ToList();

    await customUsers.BulkInsertUsingSqlBulkCopyAsync(
        static () => new SqlConnection(connectionString),
        "AlternateUsers",
        static batch =>
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Email", typeof(string));

            foreach (var user in batch) dt.Rows.Add(user.Id, user.Name, user.Email);

            return dt;
        },
        columnMappings,
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        });

    Console.WriteLine("✓ Inserted 1,000 records with custom mappings\n");

    // Sample 3: Parallel bulk inserts with batching
    Console.WriteLine("3. Parallel bulk inserts - Processing multiple batches");

    var transactions = Enumerable.Range(1, 10000)
        .Select(static i => new
        {
            Id = i,
            Name = $"Transaction_{i}",
            Value = i * 9.99m
        })
        .ToList();

    startTime = DateTime.UtcNow;

    await transactions.BulkInsertUsingSqlBulkCopyAsync(
        static () => new SqlConnection(connectionString),
        "Transactions",
        static batch =>
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Value", typeof(decimal));

            foreach (var txn in batch) dt.Rows.Add(txn.Id, txn.Name, txn.Value);

            return dt;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(500)
        },
        batchSize: 2000);

    elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {transactions.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {transactions.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 4: SqlBulkCopy with options
    Console.WriteLine("4. SqlBulkCopy with advanced options (KeepIdentity, CheckConstraints)");

    var products = Enumerable.Range(1, 500)
        .Select(static i => new
        {
            Id = i,
            ProductName = $"Product_{i}"
        })
        .ToList();

    await products.BulkInsertUsingSqlBulkCopyAsync(
        static () => new SqlConnection(connectionString),
        "Products",
        static batch =>
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("ProductName", typeof(string));

            foreach (var product in batch) dt.Rows.Add(product.Id, product.ProductName);

            return dt;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 },
        bulkCopyOptions: SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.CheckConstraints,
        batchSize: 500,
        bulkCopyTimeout: 60);

    Console.WriteLine("✓ Inserted 500 products with advanced options\n");

    // Sample 5: Bulk insert from IDataReader
    Console.WriteLine("5. Bulk insert using IDataReader (when you have large result sets)");

    // Note: This overload accepts IDataReader sources directly
    // Useful for copying data from one database to another
    Console.WriteLine("  (Skipped - requires existing data source with IDataReader)\n");

    Console.WriteLine("=== Sample Complete ===");
    Console.WriteLine("\nPerformance Summary:");
    Console.WriteLine("  - SqlBulkCopy is 10-100x faster than standard INSERT statements");
    Console.WriteLine("  - Ideal for bulk data imports, ETL pipelines, and data migrations");
    Console.WriteLine("  - Supports parallel processing for maximum throughput");
    Console.WriteLine("  - Custom column mappings for flexible schema mapping");
    Console.WriteLine("  - Process large datasets in configurable batches");
}
catch (SqlException ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
    Console.WriteLine("   Please configure a valid connection string and ensure the database & tables exist.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}