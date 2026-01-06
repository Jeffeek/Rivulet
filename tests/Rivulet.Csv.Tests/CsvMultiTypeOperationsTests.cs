using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

/// <summary>
///     Tests for multi-type CSV read and write operations with 2-5 generic parameters.
/// </summary>
public sealed class CsvMultiTypeOperationsTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvMultiTypeOperationsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvMultiTypeTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    #region Read Tests (2-5 Types)

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithTwoTypes_ShouldParseAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products.csv");
        var customersPath = Path.Combine(_testDirectory, "customers.csv");

        await File.WriteAllTextAsync(
            productsPath,
            """
            Id,Name,Price
            1,Product A,10.50
            2,Product B,20.00
            """);

        await File.WriteAllTextAsync(
            customersPath,
            """
            Id,Name,Email
            1,John Doe,john@example.com
            2,Jane Smith,jane@example.com
            """);

        var productReads = new[] { new RivuletCsvReadFile<Product>(productsPath, null) };
        var customerReads = new[] { new RivuletCsvReadFile<Customer>(customersPath, null) };

        // Act
        var (products, customers) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
            productReads,
            customerReads);

        // Assert
        products.Count.ShouldBe(1);
        products[productsPath].Count.ShouldBe(2);
        products[productsPath][0].Name.ShouldBe("Product A");
        products[productsPath][1].Name.ShouldBe("Product B");

        customers.Count.ShouldBe(1);
        customers[customersPath].Count.ShouldBe(2);
        customers[customersPath][0].Name.ShouldBe("John Doe");
        customers[customersPath][1].Name.ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithThreeTypes_ShouldParseAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products.csv");
        var customersPath = Path.Combine(_testDirectory, "customers.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders.csv");

        await File.WriteAllTextAsync(productsPath, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(customersPath, "Id,Name,Email\n1,John Doe,john@example.com");
        await File.WriteAllTextAsync(ordersPath, "Id,ProductId,CustomerId,Quantity\n1,1,1,5");

        var productReads = new[] { new RivuletCsvReadFile<Product>(productsPath, null) };
        var customerReads = new[] { new RivuletCsvReadFile<Customer>(customersPath, null) };
        var orderReads = new[] { new RivuletCsvReadFile<Order>(ordersPath, null) };

        // Act
        var (products, customers, orders) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
            productReads,
            customerReads,
            orderReads);

        // Assert
        products.Count.ShouldBe(1);
        customers.Count.ShouldBe(1);
        orders.Count.ShouldBe(1);
        orders[ordersPath][0].Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithFourTypes_ShouldParseAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products.csv");
        var customersPath = Path.Combine(_testDirectory, "customers.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders.csv");
        var categoriesPath = Path.Combine(_testDirectory, "categories.csv");

        await File.WriteAllTextAsync(productsPath, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(customersPath, "Id,Name,Email\n1,John Doe,john@example.com");
        await File.WriteAllTextAsync(ordersPath, "Id,ProductId,CustomerId,Quantity\n1,1,1,5");
        await File.WriteAllTextAsync(categoriesPath, "Id,Name\n1,Electronics");

        var productReads = new[] { new RivuletCsvReadFile<Product>(productsPath, null) };
        var customerReads = new[] { new RivuletCsvReadFile<Customer>(customersPath, null) };
        var orderReads = new[] { new RivuletCsvReadFile<Order>(ordersPath, null) };
        var categoryReads = new[] { new RivuletCsvReadFile<Category>(categoriesPath, null) };

        // Act
        var (products, customers, orders, categories) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
            productReads,
            customerReads,
            orderReads,
            categoryReads);

        // Assert
        products.Count.ShouldBe(1);
        customers.Count.ShouldBe(1);
        orders.Count.ShouldBe(1);
        categories.Count.ShouldBe(1);
        categories[categoriesPath][0].Name.ShouldBe("Electronics");
    }

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithFiveTypes_ShouldParseAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products.csv");
        var customersPath = Path.Combine(_testDirectory, "customers.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders.csv");
        var categoriesPath = Path.Combine(_testDirectory, "categories.csv");
        var suppliersPath = Path.Combine(_testDirectory, "suppliers.csv");

        await File.WriteAllTextAsync(productsPath, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(customersPath, "Id,Name,Email\n1,John Doe,john@example.com");
        await File.WriteAllTextAsync(ordersPath, "Id,ProductId,CustomerId,Quantity\n1,1,1,5");
        await File.WriteAllTextAsync(categoriesPath, "Id,Name\n1,Electronics");
        await File.WriteAllTextAsync(suppliersPath, "Id,Name,Country\n1,Supplier Inc,USA");

        var productReads = new[] { new RivuletCsvReadFile<Product>(productsPath, null) };
        var customerReads = new[] { new RivuletCsvReadFile<Customer>(customersPath, null) };
        var orderReads = new[] { new RivuletCsvReadFile<Order>(ordersPath, null) };
        var categoryReads = new[] { new RivuletCsvReadFile<Category>(categoriesPath, null) };
        var supplierReads = new[] { new RivuletCsvReadFile<Supplier>(suppliersPath, null) };

        // Act
        var (products, customers, orders, categories, suppliers) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
            productReads,
            customerReads,
            orderReads,
            categoryReads,
            supplierReads);

        // Assert
        products.Count.ShouldBe(1);
        customers.Count.ShouldBe(1);
        orders.Count.ShouldBe(1);
        categories.Count.ShouldBe(1);
        suppliers.Count.ShouldBe(1);
        suppliers[suppliersPath][0].Country.ShouldBe("USA");
    }

    #endregion

    #region Write Tests (2-5 Types)

    [Fact]
    public async Task WriteCsvParallelAsync_WithTwoTypes_ShouldWriteAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products_out.csv");
        var customersPath = Path.Combine(_testDirectory, "customers_out.csv");

        var products = new[] { new Product { Id = 1, Name = "Product A", Price = 10.50m } };
        var customers = new[] { new Customer { Id = 1, Name = "John Doe", Email = "john@example.com" } };

        var productWrites = new[] { new RivuletCsvWriteFile<Product>(productsPath, products, null) };
        var customerWrites = new[] { new RivuletCsvWriteFile<Customer>(customersPath, customers, null) };

        // Act
        await CsvParallelExtensions.WriteCsvParallelAsync(
            productWrites,
            customerWrites);

        // Assert
        File.Exists(productsPath).ShouldBeTrue();
        File.Exists(customersPath).ShouldBeTrue();

        var productContent = await File.ReadAllTextAsync(productsPath);
        productContent.ShouldContain("Product A");

        var customerContent = await File.ReadAllTextAsync(customersPath);
        customerContent.ShouldContain("John Doe");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithThreeTypes_ShouldWriteAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products_out.csv");
        var customersPath = Path.Combine(_testDirectory, "customers_out.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders_out.csv");

        var products = new[] { new Product { Id = 1, Name = "Product A", Price = 10.50m } };
        var customers = new[] { new Customer { Id = 1, Name = "John Doe", Email = "john@example.com" } };
        var orders = new[] { new Order { Id = 1, ProductId = 1, CustomerId = 1, Quantity = 5 } };

        var productWrites = new[] { new RivuletCsvWriteFile<Product>(productsPath, products, null) };
        var customerWrites = new[] { new RivuletCsvWriteFile<Customer>(customersPath, customers, null) };
        var orderWrites = new[] { new RivuletCsvWriteFile<Order>(ordersPath, orders, null) };

        // Act
        await CsvParallelExtensions.WriteCsvParallelAsync(
            productWrites,
            customerWrites,
            orderWrites);

        // Assert
        File.Exists(productsPath).ShouldBeTrue();
        File.Exists(customersPath).ShouldBeTrue();
        File.Exists(ordersPath).ShouldBeTrue();

        var orderContent = await File.ReadAllTextAsync(ordersPath);
        orderContent.ShouldContain("5");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithFourTypes_ShouldWriteAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products_out.csv");
        var customersPath = Path.Combine(_testDirectory, "customers_out.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders_out.csv");
        var categoriesPath = Path.Combine(_testDirectory, "categories_out.csv");

        var products = new[] { new Product { Id = 1, Name = "Product A", Price = 10.50m } };
        var customers = new[] { new Customer { Id = 1, Name = "John Doe", Email = "john@example.com" } };
        var orders = new[] { new Order { Id = 1, ProductId = 1, CustomerId = 1, Quantity = 5 } };
        var categories = new[] { new Category { Id = 1, Name = "Electronics" } };

        var productWrites = new[] { new RivuletCsvWriteFile<Product>(productsPath, products, null) };
        var customerWrites = new[] { new RivuletCsvWriteFile<Customer>(customersPath, customers, null) };
        var orderWrites = new[] { new RivuletCsvWriteFile<Order>(ordersPath, orders, null) };
        var categoryWrites = new[] { new RivuletCsvWriteFile<Category>(categoriesPath, categories, null) };

        // Act
        await CsvParallelExtensions.WriteCsvParallelAsync(
            productWrites,
            customerWrites,
            orderWrites,
            categoryWrites);

        // Assert
        File.Exists(productsPath).ShouldBeTrue();
        File.Exists(customersPath).ShouldBeTrue();
        File.Exists(ordersPath).ShouldBeTrue();
        File.Exists(categoriesPath).ShouldBeTrue();

        var categoryContent = await File.ReadAllTextAsync(categoriesPath);
        categoryContent.ShouldContain("Electronics");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithFiveTypes_ShouldWriteAllFiles()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products_out.csv");
        var customersPath = Path.Combine(_testDirectory, "customers_out.csv");
        var ordersPath = Path.Combine(_testDirectory, "orders_out.csv");
        var categoriesPath = Path.Combine(_testDirectory, "categories_out.csv");
        var suppliersPath = Path.Combine(_testDirectory, "suppliers_out.csv");

        var products = new[] { new Product { Id = 1, Name = "Product A", Price = 10.50m } };
        var customers = new[] { new Customer { Id = 1, Name = "John Doe", Email = "john@example.com" } };
        var orders = new[] { new Order { Id = 1, ProductId = 1, CustomerId = 1, Quantity = 5 } };
        var categories = new[] { new Category { Id = 1, Name = "Electronics" } };
        var suppliers = new[] { new Supplier { Id = 1, Name = "Supplier Inc", Country = "USA" } };

        var productWrites = new[] { new RivuletCsvWriteFile<Product>(productsPath, products, null) };
        var customerWrites = new[] { new RivuletCsvWriteFile<Customer>(customersPath, customers, null) };
        var orderWrites = new[] { new RivuletCsvWriteFile<Order>(ordersPath, orders, null) };
        var categoryWrites = new[] { new RivuletCsvWriteFile<Category>(categoriesPath, categories, null) };
        var supplierWrites = new[] { new RivuletCsvWriteFile<Supplier>(suppliersPath, suppliers, null) };

        // Act
        await CsvParallelExtensions.WriteCsvParallelAsync(
            productWrites,
            customerWrites,
            orderWrites,
            categoryWrites,
            supplierWrites);

        // Assert
        File.Exists(productsPath).ShouldBeTrue();
        File.Exists(customersPath).ShouldBeTrue();
        File.Exists(ordersPath).ShouldBeTrue();
        File.Exists(categoriesPath).ShouldBeTrue();
        File.Exists(suppliersPath).ShouldBeTrue();

        var supplierContent = await File.ReadAllTextAsync(suppliersPath);
        supplierContent.ShouldContain("Supplier Inc");
        supplierContent.ShouldContain("USA");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithTwoTypes_WithParallelOptions_ShouldRespectConcurrency()
    {
        // Arrange
        var productsPath = Path.Combine(_testDirectory, "products_concurrent.csv");
        var customersPath = Path.Combine(_testDirectory, "customers_concurrent.csv");

        var products = Enumerable.Range(1, 100)
            .Select(static i => new Product { Id = i, Name = $"Product {i}", Price = i * 1.5m })
            .ToArray();

        var customers = Enumerable.Range(1, 100)
            .Select(static i => new Customer { Id = i, Name = $"Customer {i}", Email = $"customer{i}@example.com" })
            .ToArray();

        var productWrites = new[] { new RivuletCsvWriteFile<Product>(productsPath, products, null) };
        var customerWrites = new[] { new RivuletCsvWriteFile<Customer>(customersPath, customers, null) };

        var options = new CsvOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            }
        };

        // Act
        await CsvParallelExtensions.WriteCsvParallelAsync(
            productWrites,
            customerWrites,
            options);

        // Assert
        File.Exists(productsPath).ShouldBeTrue();
        File.Exists(customersPath).ShouldBeTrue();

        var productLines = (await File.ReadAllLinesAsync(productsPath)).Length;
        var customerLines = (await File.ReadAllLinesAsync(customersPath)).Length;

        productLines.ShouldBe(101); // 100 records + 1 header
        customerLines.ShouldBe(101); // 100 records + 1 header
    }

    #endregion

    #region Test Models

    [
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    [
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    private sealed class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int CustomerId { get; set; }
        public int Quantity { get; set; }
    }

    [
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    private sealed class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    private sealed class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    #endregion
}
