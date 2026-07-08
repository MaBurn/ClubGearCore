using System.Data;
using System.Data.Common;
using System.Globalization;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Plugins.Persistence;

public sealed class PluginDataStore : IPluginMigrationContext
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PluginSchemaNamePolicy _schemaNamePolicy;

    public PluginDataStore(string moduleId, ApplicationDbContext dbContext, PluginSchemaNamePolicy schemaNamePolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        _dbContext = dbContext;
        _schemaNamePolicy = schemaNamePolicy;
        ModuleId = moduleId;
        TablePrefix = _schemaNamePolicy.GetTablePrefix(moduleId);
    }

    public string ModuleId { get; }

    public string TablePrefix { get; }

    public string GetTableName(string localName)
        => _schemaNamePolicy.GetTableName(ModuleId, localName);

    public async Task ExecuteAsync(
        string sql,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _schemaNamePolicy.ValidateSql(ModuleId, sql);

        await using DbCommand command = await CreateCommandAsync(sql, parameters, cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PluginDataRow>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _schemaNamePolicy.ValidateSql(ModuleId, sql);

        await using DbCommand command = await CreateCommandAsync(sql, parameters, cancellationToken);
        var rows = new List<PluginDataRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.IsDBNull(index)
                    ? null
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                values[reader.GetName(index)] = value;
            }

            rows.Add(new PluginDataRow(values));
        }

        return rows;
    }

    private async Task<DbCommand> CreateCommandAsync(
        string sql,
        IReadOnlyDictionary<string, string?>? parameters,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Key.StartsWith("@", StringComparison.Ordinal)
                    ? parameter.Key
                    : $"@{parameter.Key}";
                dbParameter.Value = parameter.Value ?? (object)DBNull.Value;
                command.Parameters.Add(dbParameter);
            }
        }

        return command;
    }
}