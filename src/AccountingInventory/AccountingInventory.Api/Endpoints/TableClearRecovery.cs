using System.Text.Json;
using Npgsql;

namespace AccountingInventory.Api.Endpoints;

public static class TableClearRecovery
{
    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    public static async Task<Guid> ClearAsync(string connectionString, string schema, IReadOnlyList<string> tables, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        async Task Execute(string sql) { await using var cmd = new NpgsqlCommand(sql, conn, tx); await cmd.ExecuteNonQueryAsync(ct); }
        await Execute("CREATE SCHEMA IF NOT EXISTS admin_table_clear; CREATE TABLE IF NOT EXISTS admin_table_clear.operations (id uuid PRIMARY KEY, source_schema text NOT NULL, tables_json text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), reverted boolean NOT NULL DEFAULT false);");
        var affected = new List<string[]>();
        await using (var cmd = new NpgsqlCommand("""
            WITH RECURSIVE affected(oid) AS (
              SELECT c.oid FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=@schema AND c.relname=ANY(@tables)
              UNION SELECT fk.conrelid FROM pg_constraint fk JOIN affected a ON fk.confrelid=a.oid WHERE fk.contype='f'
            ) SELECT n.nspname,c.relname FROM affected a JOIN pg_class c ON c.oid=a.oid JOIN pg_namespace n ON n.oid=c.relnamespace ORDER BY n.nspname,c.relname
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("schema", schema); cmd.Parameters.AddWithValue("tables", tables.ToArray());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) affected.Add([reader.GetString(0), reader.GetString(1)]);
        }
        if (affected.Count == 0) throw new InvalidOperationException("No tables were found to clear.");
        if (affected.Any(t => t[1].StartsWith("__") || t[1] == "table_registry" || t[0].StartsWith("admin_table_clear") || t[0].StartsWith("table_clear_")))
            throw new InvalidOperationException("This operation would affect protected system or recovery tables.");
        var targets = string.Join(",", affected.Select(t => $"{Quote(t[0])}.{Quote(t[1])}"));
        await Execute($"LOCK TABLE {targets} IN ACCESS EXCLUSIVE MODE;");
        var id = Guid.NewGuid(); var backup = "table_clear_" + id.ToString("N");
        await Execute($"CREATE SCHEMA {Quote(backup)};");
        for (var i = 0; i < affected.Count; i++)
            await Execute($"CREATE TABLE {Quote(backup)}.t{i} AS SELECT * FROM {Quote(affected[i][0])}.{Quote(affected[i][1])};");
        await using (var cmd = new NpgsqlCommand("INSERT INTO admin_table_clear.operations(id,source_schema,tables_json) VALUES(@id,@schema,@tables)", conn, tx))
        {
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("schema", schema); cmd.Parameters.AddWithValue("tables", JsonSerializer.Serialize(affected));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await Execute($"TRUNCATE TABLE {targets} RESTRICT;");
        await tx.CommitAsync(ct);
        return id;
    }

    public static async Task RestoreAsync(string connectionString, Guid id, string schema, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        List<string[]> affected;
        await using (var cmd = new NpgsqlCommand("SELECT tables_json FROM admin_table_clear.operations WHERE id=@id AND source_schema=@schema AND NOT reverted FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("schema", schema);
            var json = await cmd.ExecuteScalarAsync(ct) as string ?? throw new InvalidOperationException("This deletion was already reverted or its recovery copy is unavailable.");
            affected = JsonSerializer.Deserialize<List<string[]>>(json)!;
        }
        async Task Execute(string sql) { await using var cmd = new NpgsqlCommand(sql, conn, tx); await cmd.ExecuteNonQueryAsync(ct); }
        var targets = string.Join(",", affected.Select(t => $"{Quote(t[0])}.{Quote(t[1])}"));
        await Execute($"LOCK TABLE {targets} IN ACCESS EXCLUSIVE MODE;");
        foreach (var table in affected)
        {
            await using var cmd = new NpgsqlCommand($"SELECT EXISTS(SELECT 1 FROM {Quote(table[0])}.{Quote(table[1])})", conn, tx);
            if ((bool)(await cmd.ExecuteScalarAsync(ct))!) throw new InvalidOperationException("Restore is blocked because affected tables contain new records. No data was changed.");
        }
        var backup = "table_clear_" + id.ToString("N");
        for (var i = 0; i < affected.Count; i++)
        {
            await using var shape = new NpgsqlCommand("""
                SELECT
                  (SELECT jsonb_agg(jsonb_build_array(attname,atttypid,atttypmod) ORDER BY attnum) FROM pg_attribute
                    WHERE attrelid=to_regclass(format('%I.%I',@schema,@table)) AND attnum>0 AND NOT attisdropped)
                  IS NOT DISTINCT FROM
                  (SELECT jsonb_agg(jsonb_build_array(attname,atttypid,atttypmod) ORDER BY attnum) FROM pg_attribute
                    WHERE attrelid=to_regclass(format('%I.%I',@backup,@copy)) AND attnum>0 AND NOT attisdropped)
                """, conn, tx);
            shape.Parameters.AddWithValue("schema", affected[i][0]); shape.Parameters.AddWithValue("table", affected[i][1]);
            shape.Parameters.AddWithValue("backup", backup); shape.Parameters.AddWithValue("copy", $"t{i}");
            if (!(bool)(await shape.ExecuteScalarAsync(ct))!)
                throw new InvalidOperationException("Restore is blocked because the table structure changed. No records were changed.");
        }
        // Restore parents before children; deferred constraints support cycles
        // only when the original constraints were declared deferrable.
        await Execute("SET CONSTRAINTS ALL DEFERRED;");
        var remaining = Enumerable.Range(0, affected.Count).ToList();
        while (remaining.Count > 0)
        {
            var progress = false;
            foreach (var i in remaining.ToArray())
            {
                await Execute("SAVEPOINT restore_table;");
                try
                {
                    var table = affected[i];
                    await using var columns = new NpgsqlCommand("SELECT string_agg(quote_ident(attname), ',' ORDER BY attnum) FROM pg_attribute WHERE attrelid=to_regclass(format('%I.%I',@schema,@table)) AND attnum>0 AND NOT attisdropped AND attgenerated=''", conn, tx);
                    columns.Parameters.AddWithValue("schema", table[0]); columns.Parameters.AddWithValue("table", table[1]);
                    var names = (string)(await columns.ExecuteScalarAsync(ct))!;
                    await Execute($"INSERT INTO {Quote(table[0])}.{Quote(table[1])} ({names}) OVERRIDING SYSTEM VALUE SELECT {names} FROM {Quote(backup)}.t{i};");
                    await Execute("RELEASE SAVEPOINT restore_table;"); remaining.Remove(i); progress = true;
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                { await Execute("ROLLBACK TO SAVEPOINT restore_table; RELEASE SAVEPOINT restore_table;"); }
            }
            if (!progress) throw new InvalidOperationException("Restore is blocked by circular database relationships. No data was changed.");
        }
        await using (var cmd = new NpgsqlCommand("UPDATE admin_table_clear.operations SET reverted=true WHERE id=@id", conn, tx))
        { cmd.Parameters.AddWithValue("id", id); await cmd.ExecuteNonQueryAsync(ct); }
        await Execute($"DROP SCHEMA {Quote(backup)} CASCADE;");
        await tx.CommitAsync(ct);
    }
}
