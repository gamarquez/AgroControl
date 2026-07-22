using AgroControl.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace AgroControl.Api.Tests;

public sealed class NpgsqlParameterHelperTests
{
    [Theory]
    [InlineData("text", NpgsqlDbType.Text)]
    [InlineData("uuid", NpgsqlDbType.Uuid)]
    [InlineData("boolean", NpgsqlDbType.Boolean)]
    [InlineData("numeric", NpgsqlDbType.Numeric)]
    [InlineData("date", NpgsqlDbType.Date)]
    public void AddNullable_WithNullValue_AssignsExplicitPostgresType(
        string type,
        NpgsqlDbType expectedType)
    {
        using var command = new NpgsqlCommand();

        switch (type)
        {
            case "text":
                NpgsqlParameterHelper.AddNullable<string>(command, "value", null);
                break;
            case "uuid":
                NpgsqlParameterHelper.AddNullable<Guid?>(command, "value", null);
                break;
            case "boolean":
                NpgsqlParameterHelper.AddNullable<bool?>(command, "value", null);
                break;
            case "numeric":
                NpgsqlParameterHelper.AddNullable<decimal?>(command, "value", null);
                break;
            case "date":
                NpgsqlParameterHelper.AddNullable<DateTime?>(command, "value", null);
                break;
            default:
                throw new InvalidOperationException("Unsupported test parameter type.");
        }

        var parameter = Assert.Single(command.Parameters.Cast<NpgsqlParameter>());
        Assert.Equal(expectedType, parameter.NpgsqlDbType);
        Assert.Equal(DBNull.Value, parameter.Value);
    }
}
