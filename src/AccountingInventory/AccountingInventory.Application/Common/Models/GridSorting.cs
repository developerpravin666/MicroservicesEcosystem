using System.Linq.Expressions;
using System.Reflection;

namespace AccountingInventory.Application.Common.Models;

public sealed class SortSelectors<T>() : Dictionary<string, Expression<Func<T, object?>>>(StringComparer.OrdinalIgnoreCase) { }

public static class GridSorting
{
    // Resolve only public scalar properties or explicitly supplied selectors.
    // Build typed OrderBy/ThenBy expressions so EF sorts in SQL before pagination.
    public static IOrderedQueryable<T> Apply<T>(IQueryable<T> query, string? sortBy, string? sortDirection,
        string defaultField = "Name", bool defaultDescending = false, SortSelectors<T>? selectors = null)
    {
        var fields = (sortBy ?? "").Split(',', StringSplitOptions.TrimEntries);
        var directions = (sortDirection ?? "").Split(',', StringSplitOptions.TrimEntries);
        IOrderedQueryable<T>? sorted = null;
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < fields.Length; index++)
            Add(fields[index], string.Equals(directions.ElementAtOrDefault(index), "desc", StringComparison.OrdinalIgnoreCase));
        foreach (var field in defaultField.Split(',', StringSplitOptions.TrimEntries))
            Add(field, defaultDescending);
        Add("Id", false); // Stable order for records with equal sort values across pages.
        return sorted ?? throw new ArgumentException("A valid default sort field is required.", nameof(defaultField));

        void Add(string field, bool descending)
        {
            if (string.IsNullOrWhiteSpace(field) || applied.Contains(field)) return;
            LambdaExpression selector;
            if (selectors is not null && selectors.TryGetValue(field, out var custom))
            {
                var body = custom.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
                    ? conversion.Operand : custom.Body;
                selector = Expression.Lambda(body, custom.Parameters);
            }
            else
            {
                var property = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null || property.GetIndexParameters().Length != 0) return;
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (!(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
                    || type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(DateTime)
                    || type == typeof(DateTimeOffset))) return;
                var parameter = Expression.Parameter(typeof(T), "row");
                selector = Expression.Lambda(Expression.Property(parameter, property), parameter);
            }
            var method = sorted is null
                ? descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)
                : descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            sorted = (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(Expression.Call(
                typeof(Queryable), method, [typeof(T), selector.ReturnType],
                (sorted ?? query).Expression, Expression.Quote(selector)));
            applied.Add(field);
        }
    }
}
