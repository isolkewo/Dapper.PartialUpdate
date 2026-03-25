# Dapper.PartialUpdate

`Dapper.PartialUpdate` adds a `Partial<T>` wrapper and Dapper extension methods that only send explicitly-set fields during `INSERT` and `UPDATE`.

## Why

Use this when you need patch-style updates/inserts:
- Sometimes you send a full model.
- Sometimes you need to omit fields so they are not written at all.

`Partial<T>` tracks whether a value has been set (`IsSet`), and extensions build SQL using only those set fields.

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

## Extension Methods

`DapperPartialExtensions` provides:
- `UpdatePartials<T>(...)`
- `UpdatePartialsAsync<T>(...)`
- `InsertPartials<T>(...)`
- `InsertPartialsAsync<T>(...)`

### Update behavior

- Table name from `[Table]` or class name.
- Key from `[Key]`, or `Id`, or `{TypeName}Id`.
- Column names from `[Column]` or property name.
- Only `Partial<T>` properties with `IsSet == true` are updated.
- If no fields are set, update returns `0` without executing SQL.

### Insert behavior

- Only set `Partial<T>` properties are included.
- If no partial fields are set, executes `INSERT ... DEFAULT VALUES`.

## Build NuGet Package (local)

```powershell
.\pack.ps1
```

Override version:

```powershell
.\pack.ps1 -Version 0.1.1
```

Packages are written to `artifacts\nuget`.
