using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;

namespace Dapper.PartialUpdate;

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
        IReadOnlyList<PartialProp> PartialProps
    );

    private static readonly ConcurrentDictionary<Type, EntityPlan> PlanCache = new();

    public static int UpdatePartials<T>(
        this IDbConnection connection,
        T entity,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), BuildPlan);
        var keyValue = plan.KeyProp.GetValue(entity)
            ?? throw new InvalidOperationException("Key value cannot be null.");

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("__key", keyValue);

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            setClauses.Add($"{QuoteIdentifier(columnName)} = @{paramName}");
            parameters.Add(paramName, value);
        }

        if (setClauses.Count == 0)
            return 0;

        var sql = $"UPDATE {plan.QualifiedTableName} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(plan.KeyColumnName)} = @__key;";
        return connection.Execute(sql, parameters, transaction, commandTimeout);
    }

    public static Task<int> UpdatePartialsAsync<T>(
        this IDbConnection connection,
        T entity,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), BuildPlan);
        var keyValue = plan.KeyProp.GetValue(entity)
            ?? throw new InvalidOperationException("Key value cannot be null.");

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("__key", keyValue);

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            setClauses.Add($"{QuoteIdentifier(columnName)} = @{paramName}");
            parameters.Add(paramName, value);
        }

        if (setClauses.Count == 0)
            return Task.FromResult(0);

        var sql = $"UPDATE {plan.QualifiedTableName} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(plan.KeyColumnName)} = @__key;";
        return connection.ExecuteAsync(sql, parameters, transaction, commandTimeout);
    }

    public static int InsertPartials<T>(
        this IDbConnection connection,
        T entity,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), BuildPlan);
        var parameters = new DynamicParameters();
        var columns = new List<string>();
        var valueParams = new List<string>();

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            columns.Add(QuoteIdentifier(columnName));
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

        return connection.Execute(sql, parameters, transaction, commandTimeout);
    }

    public static Task<int> InsertPartialsAsync<T>(
        this IDbConnection connection,
        T entity,
        IDbTransaction? transaction = null,
        int? commandTimeout = null)
        where T : class
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        var plan = PlanCache.GetOrAdd(typeof(T), BuildPlan);
        var parameters = new DynamicParameters();
        var columns = new List<string>();
        var valueParams = new List<string>();

        foreach (var partial in plan.PartialProps)
        {
            if (!TryReadSetValue(partial, entity, out var value))
                continue;

            var paramName = $"p_{partial.Name}";
            var columnName = GetColumnName(partial.Prop) ?? partial.Name;
            columns.Add(QuoteIdentifier(columnName));
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

        return connection.ExecuteAsync(sql, parameters, transaction, commandTimeout);
    }

    private static EntityPlan BuildPlan(Type entityType)
    {
        var props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetMethod is not null && p.SetMethod is not null)
            .ToArray();

        var keyProp = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            ?? props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
            ?? props.FirstOrDefault(p => string.Equals(p.Name, entityType.Name + "Id", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No key property found on {entityType.Name}. Add [Key] or a conventional Id property.");

        var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? entityType.Name;
        var schema = tableAttr?.Schema;
        var qualifiedTableName = BuildQualifiedTableName(tableName, schema);
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

        return new EntityPlan(qualifiedTableName, keyProp, keyColumnName, partialProps);
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

    private static string BuildQualifiedTableName(string tableName, string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
            return QuoteIdentifier(tableName);

        return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        var clean = identifier.Replace("]", "]]", StringComparison.Ordinal);
        return $"[{clean}]";
    }
}
