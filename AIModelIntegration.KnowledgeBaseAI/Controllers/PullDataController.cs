using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
public class PullDataController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IHttpClientFactory _httpClientFactory;

    public PullDataController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("PostgreSQLConnection") ?? throw new InvalidOperationException("Connection string 'PostgresConnection' is not configured.");
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    [HttpPost("pull-from-api")]
    public async Task<IActionResult> PullFromApi([FromBody] string apiUrl)
    {
        try
        {
            // 1. Call the external API
            var httpClient = _httpClientFactory.CreateClient();
            var jsonData = await httpClient.GetStringAsync(apiUrl);

            // 2. Ask LLM to generate CREATE TABLE SQL
            var prompt = $"""
            Analyze this JSON and return:
            1. Suggested PostgreSQL table name
            2. CREATE TABLE SQL based on the structure
            3. Name of the main array property (if any)
            
            JSON: {apiUrl}{jsonData}
            """;

            var llmResult = await CallOpenAiAsync(prompt);
            if (string.IsNullOrWhiteSpace(llmResult))
                return StatusCode(500, "LLM response was empty");

            // 3. Parse LLM result (extract table name + CREATE SQL)
            var tableName = ExtractBetween(llmResult, "`", "`")?.Trim() ?? $"dynamic_table_{DateTime.Now.Ticks}";
            var rawSqlBlock = ExtractSqlBlock(llmResult);
            if (string.IsNullOrWhiteSpace(rawSqlBlock))
                return BadRequest("No SQL block found.");

            var matchCreateTable = Regex.Match(rawSqlBlock, @"CREATE\s+TABLE\s+[\s\S]+?\);", RegexOptions.IgnoreCase);
            if (!matchCreateTable.Success)
                return BadRequest("Valid CREATE TABLE SQL not found.");

            var fullCreateSql = matchCreateTable.Value.Trim();

            // 4. Create the table in PostgreSQL
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            // Check if table already exists
            var checkExistsTable = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}');";
            await using (var checkCmd = new NpgsqlCommand(checkExistsTable, conn))
            {
                var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

                if (!exists)
                {
                    await using var createCmd = new NpgsqlCommand(fullCreateSql, conn);
                    await createCmd.ExecuteNonQueryAsync();
                }
            }

            // 5. Insert the data  
            var totalRecords = 0;
            JsonNode? jsonObj = JsonNode.Parse(jsonData);
            if (jsonObj == null)
                return BadRequest("Invalid JSON data.");

            if (jsonObj["data"] is JsonArray dataArray && dataArray.Any())
            {
                foreach (var row in dataArray)
                {
                    if (row is not JsonObject rowObject) continue;
                    await InsertRowAsync(conn, tableName, rowObject);
                    totalRecords++;
                }
            }
            else if (jsonObj is JsonObject singleRow)
            {
                await InsertRowAsync(conn, tableName, singleRow);
                totalRecords = 1;
            }
            else
            {
                return BadRequest("Could not find valid JSON object or 'data' array.");
            }

            return Ok(new { Message = "Data pulled and saved successfully", DataTableName = tableName, TotalRecords = totalRecords });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }

    #region Private Methods

    private async Task<string?> CallOpenAiAsync(string prompt)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        var body = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadAsStringAsync();
        var parsed = JsonNode.Parse(result);
        if (parsed == null)
            return null;

        return parsed["choices"]?[0]?["message"]?["content"]?.ToString();
    }

    private static string? ExtractBetween(string input, string start, string end)
    {
        var startIndex = input.IndexOf(start);
        if (startIndex == -1)
            return null;

        startIndex += start.Length;

        var endIndex = input.IndexOf(end, startIndex);
        if (endIndex == -1)
            return null;

        return input.Substring(startIndex, endIndex - startIndex);
    }

    private static string? ExtractSqlBlock(string input)
    {
        var start = input.IndexOf("```sql", StringComparison.OrdinalIgnoreCase);
        var end = input.IndexOf("```", start + 6); // skip past opening ```sql

        if (start >= 0 && end > start)
        {
            return input.Substring(start + 6, end - (start + 6)).Trim();
        }

        return null;
    }

    private async Task InsertRowAsync(NpgsqlConnection conn, string tableName, JsonObject rowObject)
    {
        try
        {
            var tableColumns = await GetTableColumnsAsync(conn, tableName);

            var filteredPairs = rowObject
             .Where(p => tableColumns.Contains(ToSnakeCase(p.Key)))
             .ToDictionary(
                 p => $"\"{ToSnakeCase(p.Key)}\"",
                 p => p.Value is null || p.Value.ToString() == "null"
                     ? "NULL"
                     : $"'{p.Value?.ToString()?.Replace("'", "''")}'"
             );

            if (!filteredPairs.Any())
                return;

            var keys = string.Join(",", filteredPairs.Keys);
            var values = string.Join(",", filteredPairs.Values);
            var updateSet = string.Join(", ", filteredPairs.Keys
                .Where(k => k != "\"id\"") // skip id in update
                .Select(k => $"{k} = EXCLUDED.{k}"));

             var upsertSql = $"""
                INSERT INTO "{tableName}" ({keys}) 
                VALUES ({values})
                ON CONFLICT ("id") DO UPDATE SET {updateSet};
                """;

            await using var insertCmd = new NpgsqlCommand(upsertSql, conn);
            await insertCmd.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(NpgsqlConnection conn, string tableName)
    {
        var sql = $@"
        SELECT column_name 
        FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = '{tableName.ToLower()}';";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && i > 0)
                result.Append('_');

            result.Append(char.ToLowerInvariant(input[i]));
        }

        return result.ToString();
    }

    #endregion
}


