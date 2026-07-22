using Npgsql;
using NpgsqlTypes;

namespace AgroControl.Infrastructure.Persistence;

internal static class NpgsqlParameterHelper
{
    public static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, ResolveType<T>())
        {
            Value = value is null ? DBNull.Value : value
        });
    }

    private static NpgsqlDbType ResolveType<T>()
    {
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (type == typeof(string)) return NpgsqlDbType.Text;
        if (type == typeof(Guid)) return NpgsqlDbType.Uuid;
        if (type == typeof(bool)) return NpgsqlDbType.Boolean;
        if (type == typeof(decimal)) return NpgsqlDbType.Numeric;
        if (type == typeof(DateTime)) return NpgsqlDbType.Date;
        if (type == typeof(DateTimeOffset)) return NpgsqlDbType.TimestampTz;
        if (type == typeof(int)) return NpgsqlDbType.Integer;
        if (type == typeof(long)) return NpgsqlDbType.Bigint;

        throw new NotSupportedException($"Nullable PostgreSQL parameter type '{type.Name}' is not supported.");
    }
}
