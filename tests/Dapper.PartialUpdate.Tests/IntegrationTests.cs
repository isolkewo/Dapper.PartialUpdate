using Microsoft.Data.Sqlite;
using Dapper;

namespace Dapper.PartialUpdate.Tests;

public class IntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public IntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _connection.Execute(@"
            CREATE TABLE Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT,
                Email TEXT,
                Age INTEGER
            );
        ");
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    [Fact]
    public void InsertPartials_WithSomeFields_InsertsOnlySetFields()
    {
        // Arrange
        var user = new User();
        user.Name = "John Doe";
        user.Age = 30;

        // Act
        var rowsAffected = _connection.InsertPartials(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected);

        var inserted = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Equal("John Doe", inserted.Name);
        Assert.Equal(30, inserted.Age);
        Assert.Null(inserted.Email);
    }

    [Fact]
    public void InsertPartials_WithNoFields_InsertsWithDefaults()
    {
        // Arrange
        var user = new User();

        // Act
        var rowsAffected = _connection.InsertPartials(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected);

        var inserted = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Null(inserted.Name);
        Assert.Null(inserted.Email);
        Assert.Null(inserted.Age);
    }

    [Fact]
    public void UpdatePartials_UpdatesOnlySetFields()
    {
        // Arrange
        _connection.Execute("INSERT INTO Users (Name, Email, Age) VALUES ('Jane', 'jane@example.com', 25)");

        var user = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        user.Name = "Jane Updated";

        // Act
        var rowsAffected = _connection.UpdatePartials(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected);

        var updated = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Equal("Jane Updated", updated.Name);
        Assert.Equal("jane@example.com", updated.Email); // Should remain unchanged
        Assert.Equal(25, updated.Age); // Should remain unchanged
    }

    [Fact]
    public void UpdatePartials_WithMultipleFields_UpdatesAllSetFields()
    {
        // Arrange
        _connection.Execute("INSERT INTO Users (Name, Email, Age) VALUES ('Bob', 'bob@example.com', 40)");

        var user = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        user.Email = "newbob@example.com";
        user.Age = 41;

        // Act
        var rowsAffected = _connection.UpdatePartials(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected);

        var updated = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Equal("Bob", updated.Name); // Should remain unchanged
        Assert.Equal("newbob@example.com", updated.Email);
        Assert.Equal(41, updated.Age);
    }

    [Fact]
    public void UpdatePartials_WithNoSetFields_ReturnsZero()
    {
        // Arrange
        _connection.Execute("INSERT INTO Users (Name, Email, Age) VALUES ('Alice', 'alice@example.com', 30)");

        var user = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");

        // Act
        var rowsAffected = _connection.UpdatePartials(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public void InsertPartialsAsync_WithSomeFields_InsertsOnlySetFields()
    {
        // Arrange
        var user = new User();
        user.Name = "Async User";
        user.Email = "async@example.com";

        // Act
        var rowsAffected = await _connection.InsertPartialsAsync(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected);

        var inserted = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Equal("Async User", inserted.Name);
        Assert.Equal("async@example.com", inserted.Email);
        Assert.Null(inserted.Age);
    }

    [Fact]
    public void UpdatePartialsAsync_UpdatesOnlySetFields()
    {
        // Arrange
        _connection.Execute("INSERT INTO Users (Name, Email, Age) VALUES ('Sync', 'sync@example.com', 50)");

        var user = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        user.Name = "Sync Updated";

        // Act
        var rowsAffected = _connection.UpdatePartialsAsync(user, DatabaseType.Standard);

        // Assert
        Assert.Equal(1, rowsAffected.Result);

        var updated = _connection.QuerySingle<User>("SELECT * FROM Users WHERE Id = 1");
        Assert.Equal("Sync Updated", updated.Name);
        Assert.Equal("sync@example.com", updated.Email);
    }
}

[Table("Users")]
public class User
{
    [Key]
    public int Id { get; set; }

    public Partial<string> Name { get; set; }
    public Partial<string> Email { get; set; }
    public Partial<int> Age { get; set; }

    // Helper properties for reading values in tests
    public string? NameValue => Name.IsSet ? Name.Value : null;
    public string? EmailValue => Email.IsSet ? Email.Value : null;
    public int? AgeValue => Age.IsSet ? Age.Value : (int?)null;
}
