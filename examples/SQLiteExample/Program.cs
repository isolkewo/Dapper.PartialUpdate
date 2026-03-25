using System.Data;
using Dapper;
using Dapper.PartialUpdate;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// This example demonstrates how to use Dapper.PartialUpdate with SQLite
// to perform partial updates and inserts.

Console.WriteLine("=== Dapper.PartialUpdate SQLite Example ===\n");

// Create an in-memory SQLite database
using var connection = new SqliteConnection("Filename=:memory:");
connection.Open();

// Initialize the database schema
connection.Execute(@"
    CREATE TABLE Products (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT,
        Price REAL,
        Stock INTEGER,
        Description TEXT
    );
");

Console.WriteLine("Database initialized with Products table.\n");

// Example 1: Insert with only some fields set
Console.WriteLine("1. Inserting a product with only Name and Price:");
var newProduct = new Product();
newProduct.Name = "Laptop";
newProduct.Price = 999.99;
// Note: Stock and Description are not set, so they won't be inserted

connection.InsertPartials(newProduct, DatabaseType.Standard);
Console.WriteLine($"   Inserted product with Id = {newProduct.Id}");

// Verify the insert
var inserted = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = newProduct.Id });
Console.WriteLine($"   Name: {inserted.NameValue}, Price: {inserted.PriceValue}, Stock: {inserted.StockValue ?? 0}, Description: {inserted.DescriptionValue ?? "(null)"}\n");

// Example 2: Insert with all fields set
Console.WriteLine("2. Inserting a product with all fields:");
var product2 = new Product();
product2.Name = "Mouse";
product2.Price = 29.99;
product2.Stock = 100;
product2.Description = "Wireless optical mouse";

connection.InsertPartials(product2, DatabaseType.Standard);
Console.WriteLine($"   Inserted product with Id = {product2.Id}");

// Verify the insert
var inserted2 = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = product2.Id });
Console.WriteLine($"   Name: {inserted2.NameValue}, Price: {inserted2.PriceValue}, Stock: {inserted2.StockValue}, Description: {inserted2.DescriptionValue}\n");

// Example 3: Update only specific fields
Console.WriteLine("3. Updating only the Stock field of the first product:");
var productToUpdate = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = 1");
productToUpdate.Stock = 50; // Only set Stock, leave other fields unset

connection.UpdatePartials(productToUpdate, DatabaseType.Standard);
Console.WriteLine("   Updated Stock to 50");

// Verify the update
var updated = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = 1");
Console.WriteLine($"   Name: {updated.NameValue}, Price: {updated.PriceValue}, Stock: {updated.StockValue}, Description: {updated.DescriptionValue ?? "(null)"}\n");

// Example 4: Update multiple fields at once
Console.WriteLine("4. Updating Price and Description of the second product:");
var productToUpdate2 = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = 2");
productToUpdate2.Price = 24.99;
productToUpdate2.Description = "Ergonomic wireless mouse";

connection.UpdatePartials(productToUpdate2, DatabaseType.Standard);
Console.WriteLine("   Updated Price to 24.99 and Description");

// Verify the update
var updated2 = connection.QuerySingle<Product>("SELECT * FROM Products WHERE Id = 2");
Console.WriteLine($"   Name: {updated2.NameValue}, Price: {updated2.PriceValue}, Stock: {updated2.StockValue}, Description: {updated2.DescriptionValue}\n");

// Example 5: Using async methods
Console.WriteLine("5. Using async methods to insert a new product:");
var product3 = new Product();
product3.Name = "Keyboard";
product3.Price = 79.99;
product3.Stock = 25;

await connection.InsertPartialsAsync(product3, DatabaseType.Standard);
Console.WriteLine($"   Inserted product with Id = {product3.Id}");

// Example 6: Insert with no fields set (uses DEFAULT VALUES)
Console.WriteLine("6. Inserting a product with no fields set:");
var emptyProduct = new Product();
connection.InsertPartials(emptyProduct, DatabaseType.Standard);
Console.WriteLine($"   Inserted empty product with Id = {emptyProduct.Id}");

// Verify all products
Console.WriteLine("\n=== All Products in Database ===");
var allProducts = connection.Query<Product>("SELECT * FROM Products").ToList();
foreach (var p in allProducts)
{
    Console.WriteLine($"   Id: {p.Id}, Name: {p.NameValue ?? "(null)"}, Price: {p.PriceValue ?? 0}, Stock: {p.StockValue ?? 0}, Description: {p.DescriptionValue ?? "(null)"}");
}

Console.WriteLine("\n=== Example Complete ===");

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    public Partial<string> Name { get; set; }
    public Partial<decimal> Price { get; set; }
    public Partial<int> Stock { get; set; }
    public Partial<string> Description { get; set; }

    // Helper properties for reading values in the example
    public string? NameValue => Name.IsSet ? Name.Value : null;
    public decimal? PriceValue => Price.IsSet ? Price.Value : null;
    public int? StockValue => Stock.IsSet ? Stock.Value : null;
    public string? DescriptionValue => Description.IsSet ? Description.Value : null;
}
