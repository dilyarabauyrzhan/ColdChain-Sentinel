using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdChainMonitor;

public static class DataFileReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    // File access and parsing exceptions are converted at the I/O boundary.
    public static Result<IReadOnlyList<Reading>, DomainError> ReadJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var readings = JsonSerializer.Deserialize<List<Reading>>(json, JsonOptions);
            if (readings is null)
                return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                    new("EMPTY_JSON", $"JSON file '{path}' did not contain a reading list."));
            return Result<IReadOnlyList<Reading>, DomainError>.Ok(readings.AsReadOnly());
        }
        catch (FileNotFoundException)
        {
            return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                new("FILE_NOT_FOUND", $"Data file '{path}' was not found."));
        }
        catch (DirectoryNotFoundException)
        {
            return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                new("DIRECTORY_NOT_FOUND", $"Directory for data file '{path}' was not found."));
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                new("MALFORMED_JSON", $"Data file '{path}' contains invalid JSON: {ex.Message}"));
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                new("FILE_READ_ERROR", $"Could not read '{path}': {ex.Message}"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<IReadOnlyList<Reading>, DomainError>.Fail(
                new("FILE_ACCESS_DENIED", $"Access to '{path}' was denied: {ex.Message}"));
        }
    }
}
