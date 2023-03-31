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
    public async Task<IActionResult> Create(string name)
    {
        if (await DBHandler.CheckTableExists(name))
            return BadRequest("Table already exists");
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Invalid body");
        try
        {
            await DBHandler.ExecuteNonQuery($"CREATE TABLE {name} ({body})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/table/ensureexist/{name}")]
    public async Task<IActionResult> EnsureExist(string name)
    {
        if (await DBHandler.CheckTableExists(name))
            return Ok();
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Invalid body, (missing types?)");
        try
        {
            await DBHandler.ExecuteNonQuery($"CREATE TABLE {name} ({body})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpDelete("[controller]/table/delete/{name}")]
    public async Task<IActionResult> DeleteTable(string name)
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

    [HttpPost("[controller]/insert/{table}")]
    public async Task<IActionResult> Insert(string table)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Body invalid");
        if (!await DBHandler.CheckTableExists(table))
            return BadRequest("Table does not exist");
        try
        {
            await DBHandler.ExecuteNonQuery($"INSERT INTO {table} VALUES ({body})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/insert")]
    public async Task<IActionResult> InsertPartial()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Body invalid");
        var split = body.Split("|");
        if (split.Length != 2)
            return BadRequest("Body invalid, must be in format \"table|where\"");
        if (!await DBHandler.CheckTableExists(split[0]))
            return BadRequest("Table does not exist");
        try
        {
            await DBHandler.ExecuteNonQuery($"INSERT INTO {split[0]} VALUES ({split[1]})");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/update/{table}")]
    public async Task<IActionResult> Update(string table)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Body invalid");
        var split = body.Split("|");
        if (split.Length != 2)
            return BadRequest("Body invalid, must be in format \"values|where\"");
        if (!await DBHandler.CheckTableExists(table))
            return BadRequest("Table does not exist");
        try
        {
            await DBHandler.ExecuteNonQuery($"UPDATE {table} SET {split[0]} WHERE {split[1]}");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/delete/{table}")]
    public async Task<IActionResult> Delete(string table)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Body invalid");
        if (!await DBHandler.CheckTableExists(table))
            return BadRequest("Table does not exist");
        try
        {
            await DBHandler.ExecuteNonQuery($"DELETE FROM {table} WHERE {body}");
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine(e, ConsoleColor.Red);
            return Problem(e.ToString());
        }

        return Ok();
    }

    [HttpPost("[controller]/select/{table}")]
    public async Task<IActionResult> Select(string table)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Body invalid");
        var split = body.Split("|");
        if (split.Length != 2)
            return BadRequest("Body invalid, must be in format \"select|where\"");
        if (!await DBHandler.CheckTableExists(table))
            return BadRequest("Table does not exist");
        try
        {
            var query = DBHandler.ExecuteQueryMultirow($"SELECT {split[0]} FROM {table} WHERE {split[1]}");
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
    public async Task<IActionResult> SelectAll(string table)
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