using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;

namespace Dapper.PartialUpdate;

/// <summary>
/// Specifies the database type for identifier quoting.
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// SQL Server uses square brackets: [identifier]
    /// </summary>
    SqlServer,
    
    /// <summary>
    /// SQLite, PostgreSQL, and MySQL use double quotes: "identifier"
    /// </summary>
    Standard,
    
    /// <summary>
    /// MySQL also supports backticks: `identifier`
    /// </summary>
    MySql
}

/// <summary>
/// Provides extension methods for partial update and insert operations using Dapper.
/// </summary>
/// <remarks>
/// These extensions enable patch-style database operations where only explicitly set fields
/// (wrapped in <see cref="Partial{T}"/>) are included in the generated SQL. This is useful
/// for scenarios where you need to update only specific fields without affecting others.
/// </remarks>
public static class DapperPartialExtensions
{
    private sealed record PartialProp(
        string Name,
        PropertyInfo Prop,
        PropertyInfo IsSetProp,
        PropertyInfo ValueProp
    );

    private sealed record EntityPlan(
        string QualifiedTableName,
        PropertyInfo KeyProp,
        string KeyColumnName,
        IReadOnlyList<PartialProp> PartialProps,
        DatabaseType DatabaseType
    );

    private static readonly ConcurrentDictionary<Type, EntityPlan> PlanCache = new();

    /// <summary>
    /// Updates only the fields of an entity that have been explicitly set using <see cref="Partial{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity to update. Must have a key property.</typeparam>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="entity">The entity with partially set fields. Cannot be null.</param>
    /// <param name="transaction">Optional database transaction. Defaults to null.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds. Defaults to null.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="entity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no key property is found or key value is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no fields are set on the entity.</exception>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1 };
    /// user.Name = "John"; // Only Name will be updated
    /// connection.UpdatePartials(user);
    /// </code>
    /// </example>
    public static int UpdatePartials<T>(
        this IDbConnection connection,
        T entity,
        DatabaseType databaseType = DatabaseType.SqlServer,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), type => BuildPlan(type, databaseType));
        var keyValue = plan.KeyProp.GetValue(entity)
            ?? throw new InvalidOperationException($"Key value for '{plan.KeyProp.Name}' cannot be null on entity of type '{typeof(T).Name}'.");

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("__key", keyValue);

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            setClauses.Add($"{QuoteIdentifier(columnName, plan.DatabaseType)} = @{paramName}");
            parameters.Add(paramName, value);
        }

        if (setClauses.Count == 0)
            return 0;

        var sql = $"UPDATE {plan.QualifiedTableName} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(plan.KeyColumnName, plan.DatabaseType)} = @__key;";
        return connection.Execute(sql, parameters, transaction, commandTimeout);
    }

    /// <summary>
    /// Asynchronously updates only the fields of an entity that have been explicitly set using <see cref="Partial{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity to update. Must have a key property.</typeparam>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="entity">The entity with partially set fields. Cannot be null.</param>
    /// <param name="transaction">Optional database transaction. Defaults to null.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation. The result is the number of rows affected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="entity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no key property is found or key value is null.</exception>
    public static Task<int> UpdatePartialsAsync<T>(
        this IDbConnection connection,
        T entity,
        DatabaseType databaseType = DatabaseType.SqlServer,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), type => BuildPlan(type, databaseType));
        var keyValue = plan.KeyProp.GetValue(entity)
            ?? throw new InvalidOperationException($"Key value for '{plan.KeyProp.Name}' cannot be null on entity of type '{typeof(T).Name}'.");

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("__key", keyValue);

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            setClauses.Add($"{QuoteIdentifier(columnName, plan.DatabaseType)} = @{paramName}");
            parameters.Add(paramName, value);
        }

        if (setClauses.Count == 0)
            return Task.FromResult(0);

        var sql = $"UPDATE {plan.QualifiedTableName} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(plan.KeyColumnName, plan.DatabaseType)} = @__key;";
        return connection.ExecuteAsync(sql, parameters, transaction, commandTimeout);
    }

    /// <summary>
    /// Inserts only the fields of an entity that have been explicitly set using <see cref="Partial{T}"/>.
    /// If no fields are set, executes an INSERT with DEFAULT VALUES.
    /// </summary>
    /// <typeparam name="T">The type of the entity to insert.</typeparam>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="entity">The entity with partially set fields. Cannot be null.</param>
    /// <param name="transaction">Optional database transaction. Defaults to null.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds. Defaults to null.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="entity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no key property is found.</exception>
    public static int InsertPartials<T>(
        this IDbConnection connection,
        T entity,
        DatabaseType databaseType = DatabaseType.SqlServer,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), type => BuildPlan(type, databaseType));
        var parameters = new DynamicParameters();
        var columns = new List<string>();
        var valueParams = new List<string>();

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            columns.Add(QuoteIdentifier(columnName, plan.DatabaseType));
            valueParams.Add($"@{paramName}");
            parameters.Add(paramName, value);
        }

        string sql;
        if (columns.Count == 0)
        {
            sql = $"INSERT INTO {plan.QualifiedTableName} DEFAULT VALUES;";
        }
        else
        {
            sql = $"INSERT INTO {plan.QualifiedTableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", valueParams)});";
        }

        connection.Execute(sql, parameters, transaction, commandTimeout);
        
        // Retrieve the auto-generated ID and set it on the entity
        var keyColumnName = plan.KeyColumnName;
        var keyPropType = plan.KeyProp.PropertyType;
        
        // Get the last inserted ID based on database type
        object? insertedId = databaseType switch
        {
            DatabaseType.SqlServer => connection.QuerySingle<decimal>(
                $"SELECT SCOPE_IDENTITY()", 
                null, 
                transaction),
            DatabaseType.MySql => connection.QuerySingle<long>(
                $"SELECT LAST_INSERT_ID()", 
                null, 
                transaction),
            _ => connection.QuerySingle<long>(
                $"SELECT last_insert_rowid()", 
                null, 
                transaction)
        };
        
        // Set the ID on the entity if we got a value
        if (insertedId != null)
        {
            // Convert to the correct type based on the key property type
            object? convertedId = insertedId switch
            {
                long l => keyPropType == typeof(int) ? (object)Convert.ToInt32(l) : 
                          keyPropType == typeof(long) ? (object)l :
                          keyPropType == typeof(short) ? (object)Convert.ToInt16(l) :
                          keyPropType == typeof(byte) ? (object)Convert.ToByte(l) :
                          keyPropType == typeof(uint) ? (object)Convert.ToUInt32(l) :
                          keyPropType == typeof(ulong) ? (object)Convert.ToUInt64(l) :
                          l,
                decimal d => keyPropType == typeof(int) ? (object)Convert.ToInt32(d) :
                             keyPropType == typeof(long) ? (object)Convert.ToInt64(d) :
                             keyPropType == typeof(decimal) ? (object)d :
                             d,
                int i => keyPropType == typeof(int) ? (object)i :
                         keyPropType == typeof(long) ? (object)Convert.ToInt64(i) :
                         i,
                _ => insertedId
            };
            
            plan.KeyProp.SetValue(entity, convertedId);
        }
        
        return 1;
    }

    /// <summary>
    /// Asynchronously inserts only the fields of an entity that have been explicitly set using <see cref="Partial{T}"/>.
    /// If no fields are set, executes an INSERT with DEFAULT VALUES.
    /// </summary>
    /// <typeparam name="T">The type of the entity to insert.</typeparam>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="entity">The entity with partially set fields. Cannot be null.</param>
    /// <param name="transaction">Optional database transaction. Defaults to null.</param>
    /// <param name="commandTimeout">Optional command timeout in seconds. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation. The result is the number of rows affected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="entity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no key property is found.</exception>
    public static async Task<int> InsertPartialsAsync<T>(
        this IDbConnection connection,
        T entity,
        DatabaseType databaseType = DatabaseType.SqlServer,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), type => BuildPlan(type, databaseType));
        var parameters = new DynamicParameters();
        var columns = new List<string>();
        var valueParams = new List<string>();

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            columns.Add(QuoteIdentifier(columnName, plan.DatabaseType));
            valueParams.Add($"@{paramName}");
            parameters.Add(paramName, value);
        }

        string sql;
        if (columns.Count == 0)
        {
            sql = $"INSERT INTO {plan.QualifiedTableName} DEFAULT VALUES;";
        }
        else
        {
            sql = $"INSERT INTO {plan.QualifiedTableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", valueParams)});";
        }

        await connection.ExecuteAsync(sql, parameters, transaction, commandTimeout);
        
        // Retrieve the auto-generated ID and set it on the entity
        var keyPropType = plan.KeyProp.PropertyType;
        
        // Get the last inserted ID based on database type
        object? insertedId = databaseType switch
        {
            DatabaseType.SqlServer => await connection.QuerySingleAsync<decimal>(
                $"SELECT SCOPE_IDENTITY()", 
                null, 
                transaction),
            DatabaseType.MySql => await connection.QuerySingleAsync<long>(
                $"SELECT LAST_INSERT_ID()", 
                null, 
                transaction),
            _ => await connection.QuerySingleAsync<long>(
                $"SELECT last_insert_rowid()", 
                null, 
                transaction)
        };
        
        // Set the ID on the entity if we got a value
        if (insertedId != null)
        {
            // Convert to the correct type based on the key property type
            object? convertedId = insertedId switch
            {
                long l => keyPropType == typeof(int) ? (object)Convert.ToInt32(l) : 
                          keyPropType == typeof(long) ? (object)l :
                          keyPropType == typeof(short) ? (object)Convert.ToInt16(l) :
                          keyPropType == typeof(byte) ? (object)Convert.ToByte(l) :
                          keyPropType == typeof(uint) ? (object)Convert.ToUInt32(l) :
                          keyPropType == typeof(ulong) ? (object)Convert.ToUInt64(l) :
                          l,
                decimal d => keyPropType == typeof(int) ? (object)Convert.ToInt32(d) :
                             keyPropType == typeof(long) ? (object)Convert.ToInt64(d) :
                             keyPropType == typeof(decimal) ? (object)d :
                             d,
                int i => keyPropType == typeof(int) ? (object)i :
                         keyPropType == typeof(long) ? (object)Convert.ToInt64(i) :
                         i,
                _ => insertedId
            };
            
            plan.KeyProp.SetValue(entity, convertedId);
        }
        
        return 1;
    }

    private static EntityPlan BuildPlan(Type entityType, DatabaseType databaseType = DatabaseType.SqlServer)
    {
        var props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetMethod is not null && p.SetMethod is not null)
            .ToArray();

        var keyProp = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            ?? props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
            ?? props.FirstOrDefault(p => string.Equals(p.Name, entityType.Name + "Id", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No key property found on '{entityType.Name}'. Add [Key] attribute or use conventional 'Id' or '{entityType.Name}Id' property name.");

        var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? entityType.Name;
        var schema = tableAttr?.Schema;
        var qualifiedTableName = BuildQualifiedTableName(tableName, schema, databaseType);
        var keyColumnName = GetColumnName(keyProp) ?? keyProp.Name;

        var partialProps = new List<PartialProp>();
        foreach (var prop in props)
        {
            if (prop == keyProp)
                continue;

            if (!IsPartialType(prop.PropertyType, out var isSetProp, out var valueProp))
                continue;

            partialProps.Add(new PartialProp(prop.Name, prop, isSetProp, valueProp));
        }

        return new EntityPlan(qualifiedTableName, keyProp, keyColumnName, partialProps, databaseType);
    }

    private static bool IsPartialType(Type type, out PropertyInfo isSetProp, out PropertyInfo valueProp)
    {
        isSetProp = null!;
        valueProp = null!;

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Partial<>))
            return false;

        isSetProp = type.GetProperty(nameof(Partial<int>.IsSet))!;
        valueProp = type.GetProperty(nameof(Partial<int>.Value))!;

        return isSetProp is not null && valueProp is not null;
    }

    private static bool TryReadSetValue<T>(PartialProp partial, T entity, out object? value)
        where T : class
    {
        value = null;
        var partialObj = partial.Prop.GetValue(entity);
        if (partialObj is null)
            return false;

        var isSet = (bool?)partial.IsSetProp.GetValue(partialObj);
        if (isSet != true)
            return false;

        value = partial.ValueProp.GetValue(partialObj);
        return true;
    }

    private static string? GetColumnName(PropertyInfo property)
        => property.GetCustomAttribute<ColumnAttribute>()?.Name;

    private static string BuildQualifiedTableName(string tableName, string? schema, DatabaseType databaseType)
    {
        if (string.IsNullOrWhiteSpace(schema))
            return QuoteIdentifier(tableName, databaseType);

        return $"{QuoteIdentifier(schema, databaseType)}.{QuoteIdentifier(tableName, databaseType)}";
    }

    /// <summary>
    /// Quotes an identifier for use in SQL statements, based on the database type.
    /// </summary>
    /// <param name="identifier">The identifier to quote.</param>
    /// <param name="databaseType">The type of database determining the quoting style.</param>
    /// <returns>The quoted identifier.</returns>
    /// <remarks>
    /// SQL Server uses square brackets [identifier], while SQLite/PostgreSQL/MySQL use double quotes "identifier" or backticks `identifier`.
    /// </remarks>
    private static string QuoteIdentifier(string identifier, DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DatabaseType.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        };
    }
}
