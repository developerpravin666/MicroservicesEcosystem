using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Common;

/// <summary>
/// Tolerant enum converter that supports:
/// 1. PascalCase string (e.g. "FullTime")
/// 2. UPPER_SNAKE_CASE string (e.g. "FULL_TIME")
/// 3. lowercase / string variation (e.g. "fulltime", "full_time")
/// 4. Integer numeric value (e.g. 1)
/// </summary>
public sealed class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var intVal = reader.GetInt32();
            if (Enum.IsDefined(typeof(T), intVal))
            {
                return (T)Enum.ToObject(typeof(T), intVal);
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var strVal = reader.GetString();
            if (!string.IsNullOrWhiteSpace(strVal))
            {
                // Normalize snake_case or SCREAMING_SNAKE_CASE to alphanumeric lowercase
                var clean = strVal.Replace("_", "").Replace("-", "").Trim().ToLowerInvariant();

                foreach (var name in Enum.GetNames<T>())
                {
                    if (name.ToLowerInvariant() == clean)
                    {
                        return Enum.Parse<T>(name, ignoreCase: true);
                    }
                }

                // Standard parse attempt
                if (Enum.TryParse<T>(strVal, ignoreCase: true, out var result))
                {
                    return result;
                }
            }
        }

        // Fallback default
        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
