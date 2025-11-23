using MySqlConnector;
using Rivulet.Core;
using Rivulet.Sql.MySql;

Console.WriteLine("=== Rivulet.Sql.MySql Sample ===\n");
Console.WriteLine("NOTE: Configure connection string before running!\n");

// Configure your MySQL connection
const string connectionString = "Server=localhost;Database=TestDB;Uid=root;Pwd=password;";

try
{
    // Sample 1: MySqlBulkLoader for ultra-fast inserts from CSV lines
    Console.WriteLine("1. BulkInsertUsingMySqlBulkLoaderAsync - Ultra-fast bulk inserts (10-100x faster)");

    // Create sample CSV data (10,000 records)
    var csvLines = Enumerable.Range(1, 10000)
        .Select(i => $"{i},User{i},user{i}@test.com,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
        .ToList();

    var startTime = DateTime.UtcNow;

    await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "Users",
        new[] { "Id", "Name", "Email", "CreatedAt" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort
        },
        batchSize: 5000,
        fieldSeparator: ",",
        lineTerminator: "\n");

    var elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {csvLines.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {csvLines.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 2: Bulk insert with tab-separated values
    Console.WriteLine("2. BulkInsert with tab-separated values");

    var tsvLines = Enumerable.Range(1, 1000)
        .Select(i => $"{i + 10000}\tCustomUser{i}\tcustom{i}@test.com")
        .ToList();

    await tsvLines.BulkInsertUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "AlternateUsers",
        new[] { "UserId", "FullName", "EmailAddress" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        },
        batchSize: 1000,
        fieldSeparator: "\t",
        lineTerminator: "\n");

    Console.WriteLine($"✓ Inserted 1,000 records with tab-separated values\n");

    // Sample 3: Parallel bulk inserts with batching
    Console.WriteLine("3. Parallel bulk inserts - Processing multiple batches");

    var largeBatches = Enumerable.Range(0, 5)
        .SelectMany(batchIndex =>
            Enumerable.Range(0, 2000)
                .Select(i =>
                {
                    var id = (batchIndex * 2000) + i + 1;
                    return $"{id},Batch{batchIndex}_Item{i},{i}.99";
                }))
        .ToList();

    startTime = DateTime.UtcNow;

    await largeBatches.BulkInsertUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "Transactions",
        new[] { "Id", "Name", "Value" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2
        },
        batchSize: 2000);

    elapsed = DateTime.UtcNow - startTime;
    Console.WriteLine($"✓ Inserted {largeBatches.Count:N0} records in {elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Rate: {largeBatches.Count / elapsed.TotalSeconds:F0} rows/second\n");

    // Sample 4: Bulk insert from CSV files
    Console.WriteLine("4. BulkInsertFromFilesUsingMySqlBulkLoaderAsync - Load from files");

    // Create temporary CSV files
    var tempFiles = Enumerable.Range(1, 3)
        .Select(fileIndex =>
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"mysql_bulk_{fileIndex}.csv");
            var lines = Enumerable.Range(1, 500)
                .Select(i => $"{(fileIndex * 500) + i},Product_{(fileIndex * 500) + i},{i * 9.99:F2}");
            File.WriteAllLines(filePath, lines);
            return filePath;
        })
        .ToList();

    await tempFiles.BulkInsertFromFilesUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "Products",
        new[] { "Id", "ProductName", "Price" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3
        },
        fieldSeparator: ",",
        lineTerminator: "\n");

    Console.WriteLine($"✓ Loaded {tempFiles.Count} CSV files (1,500 total rows)\n");

    // Cleanup temp files
    foreach (var file in tempFiles)
    {
        if (File.Exists(file))
            File.Delete(file);
    }

    // Sample 5: Bulk insert from object collection (convert to CSV)
    Console.WriteLine("5. Bulk insert from object collections");

    var products = Enumerable.Range(1, 500)
        .Select(i => new
        {
            Id = i + 2000,
            ProductName = $"Product_{i}",
            Category = $"Category_{i % 10}",
            Price = i * 9.99m,
            InStock = i % 2 == 0
        })
        .ToList();

    // Convert objects to CSV lines
    var productCsvLines = products
        .Select(p => $"{p.Id},{p.ProductName},{p.Category},{p.Price:F2},{(p.InStock ? 1 : 0)}")
        .ToList();

    await productCsvLines.BulkInsertUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "ProductCatalog",
        new[] { "Id", "ProductName", "Category", "Price", "InStock" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        },
        batchSize: 500);

    Console.WriteLine($"✓ Inserted {products.Count} products from object collection\n");

    // Sample 6: Bulk insert with custom CSV format (pipe-separated)
    Console.WriteLine("6. Custom CSV format - Pipe-separated values");

    var pipeLines = Enumerable.Range(1, 100)
        .Select(i => $"{i}|Order-{i:D6}|{i * 49.99:F2}|{DateTime.UtcNow:yyyy-MM-dd}")
        .ToList();

    await pipeLines.BulkInsertUsingMySqlBulkLoaderAsync(
        () => new MySqlConnection(connectionString),
        "Orders",
        new[] { "Id", "OrderNumber", "Amount", "OrderDate" },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1
        },
        batchSize: 100,
        fieldSeparator: "|",
        lineTerminator: "\n");

    Console.WriteLine($"✓ Inserted {pipeLines.Count} orders with pipe-separated format\n");

    Console.WriteLine("=== Sample Complete ===");
    Console.WriteLine("\nPerformance Summary:");
    Console.WriteLine("  - MySqlBulkLoader is 10-100x faster than standard INSERT statements");
    Console.WriteLine("  - Works with CSV lines (in-memory) or CSV files (file-based)");
    Console.WriteLine("  - Supports custom field separators and line terminators");
    Console.WriteLine("  - Ideal for bulk data imports, ETL pipelines, and data migrations");
    Console.WriteLine("  - Supports parallel processing for maximum throughput");
}
catch (MySqlException ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
    Console.WriteLine("   Please configure a valid connection string and ensure the database & tables exist.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}
