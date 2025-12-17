using Npgsql;
using Rivulet.Core;
using Rivulet.Sql.PostgreSql;

// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

Console.WriteLine("=== Rivulet.Sql.PostgreSql Sample ===\n");
Console.WriteLine("NOTE: Configure connection string before running!\n");

// Configure your PostgreSQL connection
const string connectionString = "Host=localhost;Database=testdb;Username=postgres;Password=password";

try
{
    // Sample 1: COPY with binary format - ultra-fast inserts
    Console.WriteLine("1. BulkInsertUsingCopyAsync - Ultra-fast bulk inserts (10-100x faster)");

    // Create sample data
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

    await users.BulkInsertUsingCopyAsync(
        static () => new NpgsqlConnection(connectionString),
        "users",
        ["id", "name", "email", "created_at"],
        static user => [user.Id, user.Name, user.Email, user.CreatedAt],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort
        },
        batchSize: 5000);

    var elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {users.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {users.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 2: Bulk insert with custom object mapping
    Console.WriteLine("2. BulkInsert with custom object mapping");

    var customUsers = Enumerable.Range(1, 1000)
        .Select(static i => new
        {
            UserId = i + 10000,
            FullName = $"CustomUser{i}",
            EmailAddress = $"custom{i}@test.com",
            IsActive = i % 2 == 0
        })
        .ToList();

    await customUsers.BulkInsertUsingCopyAsync(
        static () => new NpgsqlConnection(connectionString),
        "alternate_users",
        ["user_id", "full_name", "email_address", "is_active"],
        static u => [u.UserId, u.FullName, u.EmailAddress, u.IsActive],
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

    await transactions.BulkInsertUsingCopyAsync(
        static () => new NpgsqlConnection(connectionString),
        "transactions",
        ["id", "name", "value"],
        static t => [t.Id, t.Name, t.Value],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2
        },
        batchSize: 2000);

    elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {transactions.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {transactions.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 4: COPY with CSV format (text-based import)
    Console.WriteLine("4. BulkInsertUsingCopyCsvAsync - CSV format import");

    var csvLines = Enumerable.Range(1, 1000)
        .Select(static i => $"{i},Product_{i},{i * 9.99:F2}")
        .ToList();

    await csvLines.BulkInsertUsingCopyCsvAsync(
        static () => new NpgsqlConnection(connectionString),
        "products",
        ["id", "product_name", "price"],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        },
        batchSize: 1000,
        hasHeader: false,
        delimiter: ',');

    Console.WriteLine("✓ Inserted 1,000 products using CSV format\n");

    // Sample 5: COPY with text format (tab-delimited)
    Console.WriteLine("5. BulkInsertUsingCopyTextAsync - Text format (tab-delimited)");

    var textLines = Enumerable.Range(1, 500)
        .Select(static i => $"{i}\tOrder-{i:D6}\t{i * 49.99:F2}\t{DateTime.UtcNow:yyyy-MM-dd}")
        .ToList();

    await textLines.BulkInsertUsingCopyTextAsync(
        static () => new NpgsqlConnection(connectionString),
        "orders",
        ["id", "order_number", "total_amount", "order_date"],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        },
        batchSize: 500);

    Console.WriteLine("✓ Inserted 500 orders using text format\n");

    // Sample 6: Complex object mapping with nulls
    Console.WriteLine("6. Bulk insert with nullable fields");

    var inventory = Enumerable.Range(1, 1000)
        .Select(static i => new
        {
            Id = i,
            ItemName = $"Item_{i}",
            Quantity = i % 3 == 0 ? (int?)null : i * 10,
            LastRestocked = i % 2 == 0 ? (DateTime?)DateTime.UtcNow.AddDays(-i) : null
        })
        .ToList();

    await inventory.BulkInsertUsingCopyAsync(
        static () => new NpgsqlConnection(connectionString),
        "inventory",
        ["id", "item_name", "quantity", "last_restocked"],
        static item => [item.Id, item.ItemName, item.Quantity, item.LastRestocked],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3
        },
        batchSize: 1000);

    Console.WriteLine($"✓ Inserted {inventory.Count} inventory items with nullable fields\n");

    // Sample 7: Using COPY with array types
    Console.WriteLine("7. Bulk insert with PostgreSQL arrays");

    var documents = Enumerable.Range(1, 100)
        .Select(static i => new
        {
            Id = i,
            Title = $"Document_{i}",
            Tags = new[] { $"tag{i % 5}", $"category{i % 3}", "important" },
            Scores = new[] { i * 1.1, i * 2.2, i * 3.3 }
        })
        .ToList();

    await documents.BulkInsertUsingCopyAsync(
        static () => new NpgsqlConnection(connectionString),
        "documents",
        ["id", "title", "tags", "scores"],
        static doc => [doc.Id, doc.Title, doc.Tags, doc.Scores],
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        });

    Console.WriteLine($"✓ Inserted {documents.Count} documents with array types\n");

    Console.WriteLine("=== Sample Complete ===");
    Console.WriteLine("\nPerformance Summary:");
    Console.WriteLine("  - PostgreSQL COPY is 10-100x faster than standard INSERT statements");
    Console.WriteLine("  - Binary format (BulkInsertUsingCopyAsync) provides optimal performance");
    Console.WriteLine("  - CSV format (BulkInsertUsingCopyCsvAsync) for text-based imports");
    Console.WriteLine("  - Text format (BulkInsertUsingCopyTextAsync) for tab-delimited data");
    Console.WriteLine("  - Supports complex types: nulls, arrays, custom objects");
    Console.WriteLine("  - Ideal for bulk data imports, ETL pipelines, and data migrations");
    Console.WriteLine("  - Supports parallel processing for maximum throughput");
}
catch (NpgsqlException ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
    Console.WriteLine("   Please configure a valid connection string and ensure the database & tables exist.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}