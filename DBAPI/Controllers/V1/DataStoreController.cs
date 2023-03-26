namespace DBAPI.Controllers.V1;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[Route("Application/V1/")]
[ApiController]
public class DataStoreController : ControllerBase
{
    private readonly ILogger<DataStoreController> _logger;

    public DataStoreController(ILogger<DataStoreController> logger)
    {
        _logger = logger;
    }

    [HttpGet("[controller]/table/show")]
    public async Task<IActionResult> Show()
    {
        List<string> tables = new();
        try
        {
            var tablesQuery = DBHandler.ExecuteQuery($"SHOW TABLES");
            await foreach (var queryObject in tablesQuery)
            { 
                tables.Add(queryObject.ToString()!);
            }
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Content(JsonConvert.SerializeObject(tables.ToArray()));
    }

    [HttpPost("[controller]/table/create/{name}")]
    public async Task<IActionResult> Create(string name, [FromBody]object types)
    {
        if (await DBHandler.CheckTableExists(name))
            return BadRequest("Table already exists");
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(types.ToString() ?? string.Empty);
        if (deserializeObject == null || !deserializeObject.ContainsKey("types") || deserializeObject["types"] is not string typesString)
            return BadRequest("Invalid types");
        try
        {
            await DBHandler.ExecuteNonQuery($"CREATE TABLE {name} ({typesString})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/table/ensureexist/{name}")]
    public async Task<IActionResult> EnsureExist(string name, [FromBody]object types)
    {
        if (await DBHandler.CheckTableExists(name))
            return Ok();
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(types.ToString() ?? string.Empty);
        if (deserializeObject == null || !deserializeObject.ContainsKey("types") || deserializeObject["types"] is not string typesString)
            return BadRequest("Invalid types");
        try
        {
            await DBHandler.ExecuteNonQuery($"CREATE TABLE {name} ({typesString})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/table/delete/{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        if (!await DBHandler.CheckTableExists(name))
            return BadRequest("Table does not exist");
        try
        {
            await DBHandler.ExecuteNonQuery($"DROP TABLE {name}");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/insert")]
    public async Task<IActionResult> Insert([FromBody]object data)
    {
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString() ?? string.Empty);
        if (deserializeObject == null)
            return BadRequest("Body invalid");
        if (!deserializeObject.ContainsKey("table") || deserializeObject["table"] is not string tableString)
            return BadRequest("Invalid table");
        if (!await DBHandler.CheckTableExists(tableString))
            return BadRequest("Table does not exist");
        if (!deserializeObject.ContainsKey("values") || deserializeObject["values"] is not string valuesString)
            return BadRequest("Invalid values");
        try
        {
            await DBHandler.ExecuteNonQuery($"INSERT INTO {tableString} VALUES ({valuesString})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/update")]
    public async Task<IActionResult> Update([FromBody]object data)
    {
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString() ?? string.Empty);
        if (deserializeObject == null)
            return BadRequest("Body invalid");
        if (!deserializeObject.ContainsKey("table") || deserializeObject["table"] is not string tableString)
            return BadRequest("Invalid table");
        if (!await DBHandler.CheckTableExists(tableString))
            return BadRequest("Table does not exist");
        if (!deserializeObject.ContainsKey("values") || deserializeObject["values"] is not string valuesString)
            return BadRequest("Invalid values");
        if (!deserializeObject.ContainsKey("where") || deserializeObject["where"] is not string whereString)
            return BadRequest("Invalid where");
        try
        {
            await DBHandler.ExecuteNonQuery($"UPDATE {tableString} SET {valuesString} WHERE {whereString}");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/delete")]
    public async Task<IActionResult> Delete([FromBody]object data)
    {
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString() ?? string.Empty);
        if (deserializeObject == null)
            return BadRequest("Body invalid");
        if (!deserializeObject.ContainsKey("table") || deserializeObject["table"] is not string tableString)
            return BadRequest("Invalid table");
        if (!await DBHandler.CheckTableExists(tableString))
            return BadRequest("Table does not exist");
        if (!deserializeObject.ContainsKey("where") || deserializeObject["where"] is not string whereString)
            return BadRequest("Invalid where");
        try
        {
            await DBHandler.ExecuteNonQuery($"DELETE FROM {tableString} WHERE {whereString}");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/select")]
    public async Task<IActionResult> Select([FromBody]object data)
    {
        var deserializeObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString() ?? string.Empty);
        if (deserializeObject == null)
            return BadRequest("Body invalid");
        if (!deserializeObject.ContainsKey("table") || deserializeObject["table"] is not string tableString)
            return BadRequest("Invalid table");
        if (!await DBHandler.CheckTableExists(tableString))
            return BadRequest("Table does not exist");
        if (!deserializeObject.ContainsKey("where") || deserializeObject["where"] is not string whereString)
            return BadRequest("Invalid where");
        string selectString = deserializeObject.ContainsKey("select") && deserializeObject["select"] is string selectString1 ? selectString1 : "*";
        try
        {
            var query = DBHandler.ExecuteQueryMultirow($"SELECT {selectString} FROM {tableString} WHERE {whereString}");
            List<object[]> result = new();
            await foreach (var queryObject in query)
            {
                result.Add(queryObject);
            }

            return Content(JsonConvert.SerializeObject(result));
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }
    }

    [HttpGet("[controller]/selectall/{table}")]
    public async Task<IActionResult> Select(string table)
    {
        if (!await DBHandler.CheckTableExists(table))
            return BadRequest("Table does not exist");
        try
        {
            var query = DBHandler.ExecuteQueryMultirow($"SELECT * FROM {table}");
            List<object[]> result = new();
            await foreach (var queryObject in query)
            {
                result.Add(queryObject);
            }

            return Content(JsonConvert.SerializeObject(result));
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }
    }
}