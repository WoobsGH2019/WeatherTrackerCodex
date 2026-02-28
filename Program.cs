using System.Net.Http.Json;
using System.Text.Json.Serialization;

const string baseUrl = "https://api.weather.gov";
const int hoursToDisplay = 24;

var cities = new[]
{
    new City("Greenville, SC", 34.8526, -82.3940),
    new City("Redmond, WA", 47.6770, -122.1180),
    new City("New Braunfels, TX", 29.7046, -98.1039),
    new City("Washington, DC", 38.8979, -77.0365)
};

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(baseUrl)
};

httpClient.DefaultRequestHeaders.Add("User-Agent", "RobertGithub@wubortsoft.com");
httpClient.DefaultRequestHeaders.Add("Accept", "application/geo+json");

try
{
    Console.WriteLine("Weather.gov hourly forecast");
    Console.WriteLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
    Console.WriteLine();

    foreach (var city in cities)
    {
        var pointData = await GetPointDataAsync(httpClient, city.Latitude, city.Longitude);

        if (string.IsNullOrWhiteSpace(pointData.Properties?.ForecastHourly) ||
            string.IsNullOrWhiteSpace(pointData.Properties.Forecast))
        {
            Console.Error.WriteLine($"[{city.Name}] Point response did not include forecast links.");
            Console.WriteLine();
            continue;
        }

        var hourly = await GetForecastAsync(httpClient, pointData.Properties.ForecastHourly);
        var daily = await GetForecastAsync(httpClient, pointData.Properties.Forecast);

        if (hourly.Properties?.Periods is null || hourly.Properties.Periods.Length == 0)
        {
            Console.Error.WriteLine($"[{city.Name}] Hourly forecast response did not contain periods.");
            Console.WriteLine();
            continue;
        }

        Console.WriteLine(city.Name);
        Console.WriteLine($"Coordinates: {city.Latitude:F6},{city.Longitude:F6}");
        PrintSunEvents(daily, pointData.Properties.TimeZone);
        Console.WriteLine();

        var maxRows = Math.Min(hoursToDisplay, hourly.Properties.Periods.Length);
        for (var i = 0; i < maxRows; i++)
        {
            var period = hourly.Properties.Periods[i];
            var temperatureUnit = period.TemperatureUnit ?? "F";
            var shortForecast = period.ShortForecast ?? "Unknown";
            var condition = ClassifyCondition(shortForecast);

            Console.WriteLine($"{period.StartTime,-25} {period.Temperature,3}{temperatureUnit,-2} {condition,-8}  {shortForecast}");
        }

        Console.WriteLine();
    }
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    Environment.Exit(1);
}

return;

static async Task<PointResponse> GetPointDataAsync(HttpClient httpClient, double latitude, double longitude)
{
    var endpoint = $"/points/{latitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}";
    var result = await httpClient.GetFromJsonAsync<PointResponse>(endpoint);
    return result ?? throw new InvalidOperationException("Empty points response payload.");
}

static async Task<ForecastResponse> GetForecastAsync(HttpClient httpClient, string endpointOrUrl)
{
    var result = await httpClient.GetFromJsonAsync<ForecastResponse>(endpointOrUrl);
    return result ?? throw new InvalidOperationException("Empty forecast response payload.");
}

static void PrintSunEvents(ForecastResponse daily, string? timeZoneId)
{
    if (daily.Properties?.Periods is null || daily.Properties.Periods.Length == 0)
    {
        Console.WriteLine("Sunrise: n/a");
        Console.WriteLine("Sunset : n/a");
        return;
    }

    var tz = ResolveTimeZoneOrDefault(timeZoneId);
    var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    var today = DateOnly.FromDateTime(nowLocal.DateTime);

    var todayPeriods = daily.Properties.Periods
        .Where(p => DateOnly.FromDateTime(p.StartTime.Date) == today)
        .ToArray();

    var sunrise = todayPeriods.FirstOrDefault(p => p.IsDaytime)?.StartTime;
    DateTimeOffset? sunset = null;

    if (sunrise is not null)
    {
        sunset = todayPeriods
            .Where(p => !p.IsDaytime && p.StartTime > sunrise.Value)
            .Select(p => (DateTimeOffset?)p.StartTime)
            .FirstOrDefault();
    }

    if (sunrise is not null)
    {
        Console.WriteLine($"Sunrise (est): {sunrise.Value:yyyy-MM-dd HH:mm zzz}");
    }
    else
    {
        Console.WriteLine("Sunrise (est): n/a");
    }

    if (sunset is not null)
    {
        Console.WriteLine($"Sunset  (est): {sunset.Value:yyyy-MM-dd HH:mm zzz}");
    }
    else
    {
        Console.WriteLine("Sunset  (est): n/a");
    }
}

static TimeZoneInfo ResolveTimeZoneOrDefault(string? timeZoneId)
{
    if (!string.IsNullOrWhiteSpace(timeZoneId))
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }
    }

    return TimeZoneInfo.Local;
}

static string ClassifyCondition(string forecastText)
{
    if (forecastText.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("shower", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("thunder", StringComparison.OrdinalIgnoreCase))
    {
        return "Raining";
    }

    if (forecastText.Contains("snow", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("flurr", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("sleet", StringComparison.OrdinalIgnoreCase))
    {
        return "Snowing";
    }

    if (forecastText.Contains("sun", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("clear", StringComparison.OrdinalIgnoreCase))
    {
        return "Sunny";
    }

    if (forecastText.Contains("cloud", StringComparison.OrdinalIgnoreCase) ||
        forecastText.Contains("overcast", StringComparison.OrdinalIgnoreCase))
    {
        return "Cloudy";
    }

    return "Other";
}

public sealed class PointResponse
{
    [JsonPropertyName("properties")]
    public PointProperties? Properties { get; init; }
}

public sealed class PointProperties
{
    [JsonPropertyName("forecast")]
    public string? Forecast { get; init; }

    [JsonPropertyName("forecastHourly")]
    public string? ForecastHourly { get; init; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; init; }
}

public sealed class ForecastResponse
{
    [JsonPropertyName("properties")]
    public ForecastProperties? Properties { get; init; }
}

public sealed class ForecastProperties
{
    [JsonPropertyName("periods")]
    public ForecastPeriod[]? Periods { get; init; }
}

public sealed class ForecastPeriod
{
    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; init; }

    [JsonPropertyName("temperature")]
    public int Temperature { get; init; }

    [JsonPropertyName("temperatureUnit")]
    public string? TemperatureUnit { get; init; }

    [JsonPropertyName("shortForecast")]
    public string? ShortForecast { get; init; }

    [JsonPropertyName("isDaytime")]
    public bool IsDaytime { get; init; }
}
