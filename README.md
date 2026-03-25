# Dapper.PartialUpdate

`Dapper.PartialUpdate` adds a `Partial<T>` wrapper and Dapper extension methods that only send explicitly-set fields during `INSERT` and `UPDATE`.

## Why

Use this when you need patch-style updates/inserts:
- Sometimes you send a full model.
- Sometimes you need to omit fields so they are not written at all.

`Partial<T>` tracks whether a value has been set (`IsSet`), and extensions build SQL using only those set fields.

## Features

- **Partial Updates**: Update only the fields you've explicitly set
- **Partial Inserts**: Insert only the fields you've set, or use DEFAULT VALUES
- **Database Agnostic**: Supports SQL Server, SQLite, PostgreSQL, and MySQL with automatic identifier quoting
- **Async Support**: Full async/await support for all operations
- **Comprehensive Testing**: Unit tests and SQLite integration tests included
- **Example Project**: Ready-to-run SQLite example demonstrating all features

## Install

```bash
dotnet add package Dapper.PartialUpdate
```

## Quick Example

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.PartialUpdate;

[Table("Fixture")]
public sealed class Fixture
{
    [Key]
    public int Id { get; set; }

    public Partial<int> FixtureType { get; set; }
    public Partial<string?> Name { get; set; }
}
```

```csharp
var fixture = new Fixture { Id = 12 };
fixture.Name = "Updated Name";     // marks IsSet = true
fixture.FixtureType = 3;           // marks IsSet = true

await connection.UpdatePartialsAsync(fixture, tx);
```

Only `Name` and `FixtureType` are included in the `SET` clause.

## Partial<T>

```csharp
var p = new Partial<string>("abc"); // IsSet = true
p.Unset();                          // IsSet = false
p.Value = "xyz";                    // IsSet = true
```

You can also assign directly with implicit conversion:

```csharp
entity.Name = "Alice"; // Partial<string> IsSet=true
```

## Database Type Support

The library supports multiple database types with appropriate identifier quoting:

```csharp
// SQL Server (default, uses [brackets])
connection.UpdatePartials(entity);

// SQLite, PostgreSQL (uses "double quotes")
connection.UpdatePartials(entity, DatabaseType.Standard);

// MySQL (uses `backticks`)
connection.UpdatePartials(entity, DatabaseType.MySql);
```

### Identifier Quoting

- **SQL Server**: `[TableName]`, `[ColumnName]`
- **SQLite/PostgreSQL**: `"TableName"`, `"ColumnName"`
- **MySQL**: `` `TableName` ``, `` `ColumnName` ``

The library automatically escapes special characters in identifiers.

## Extension Methods

`DapperPartialExtensions` provides:
- `UpdatePartials<T>(...)`
- `UpdatePartialsAsync<T>(...)`
- `InsertPartials<T>(...)`
- `InsertPartialsAsync<T>(...)`

All methods have overloads that accept a `DatabaseType` parameter for database-specific identifier quoting.

### Update behavior

- Table name from `[Table]` or class name
- Key from `[Key]`, or `Id`, or `{TypeName}Id`
- Column names from `[Column]` or property name
- Only `Partial<T>` properties with `IsSet == true` are updated
- If no fields are set, update returns `0` without executing SQL
- Improved error messages with entity and property names

### Insert behavior

- Only set `Partial<T>` properties are included
- If no partial fields are set, executes `INSERT ... DEFAULT VALUES`

## Examples

See the `examples/SQLiteExample` directory for a complete working example using SQLite.

## Testing

Run the test suite:

```bash
cd tests/Dapper.PartialUpdate.Tests
dotnet test
```

The test suite includes:
- Unit tests for `Partial<T>` wrapper
- Database type quoting tests
- SQLite integration tests for insert and update operations

## Build NuGet Package (local)

```powershell
.\pack.ps1
```

Override version:

```powershell
.\pack.ps1 -Version 0.1.1
```

Packages are written to `artifacts\nuget`.
