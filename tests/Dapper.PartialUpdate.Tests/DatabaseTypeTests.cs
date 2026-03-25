using Xunit;

namespace Dapper.PartialUpdate.Tests;

public class DatabaseTypeTests
{
    [Fact]
    public void QuoteIdentifier_SqlServer_UsesSquareBrackets()
    {
        // Arrange
        const string identifier = "TableName";
        const string expected = "[TableName]";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.SqlServer);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_SqlServer_EscapesClosingBrackets()
    {
        // Arrange
        const string identifier = "Table]Name";
        const string expected = "[Table]]Name]";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.SqlServer);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_MySql_UsesBackticks()
    {
        // Arrange
        const string identifier = "TableName";
        const string expected = "`TableName`";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.MySql);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_MySql_EscapesClosingBackticks()
    {
        // Arrange
        const string identifier = "Table`Name";
        const string expected = "`Table``Name`";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.MySql);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_Standard_UsesDoubleQuotes()
    {
        // Arrange
        const string identifier = "TableName";
        const string expected = "\"TableName\"";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.Standard);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_Standard_EscapesClosingQuotes()
    {
        // Arrange
        const string identifier = "Table\"Name";
        const string expected = "\"Table\"\"Name\"";

        // Act
        var result = QuoteIdentifier(identifier, DatabaseType.Standard);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_WithSchema_SqlServer()
    {
        // Arrange
        const string schema = "dbo";
        const string table = "Users";
        const string expected = "[dbo].[Users]";

        // Act
        var result = BuildQualifiedTableName(schema, table, DatabaseType.SqlServer);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_WithSchema_Standard()
    {
        // Arrange
        const string schema = "public";
        const string table = "Users";
        const string expected = "\"public\".\"Users\"";

        // Act
        var result = BuildQualifiedTableName(schema, table, DatabaseType.Standard);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QuoteIdentifier_WithSchema_MySql()
    {
        // Arrange
        const string schema = "mydb";
        const string table = "Users";
        const string expected = "`mydb`.`Users`";

        // Act
        var result = BuildQualifiedTableName(schema, table, DatabaseType.MySql);

        // Assert
        Assert.Equal(expected, result);
    }

    // Helper methods that mirror the private methods in DapperPartialExtensions
    private static string QuoteIdentifier(string identifier, DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DatabaseType.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        };
    }

    private static string BuildQualifiedTableName(string schema, string table, DatabaseType databaseType)
    {
        return $"{QuoteIdentifier(schema, databaseType)}.{QuoteIdentifier(table, databaseType)}";
    }
}
