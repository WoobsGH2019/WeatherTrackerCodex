using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WeatherTracker.Services;

public sealed class WeatherGovClient(HttpClient httpClient)
{
    public async Task<WeatherReport> GetReportAsync(IEnumerable<City> cities, int hoursToDisplay)
    {
        var generatedAt = DateTimeOffset.Now;
        var cityReports = new List<CityWeatherReport>();

        foreach (var city in cities)
        {
            try
            {
                var pointData = await GetPointDataAsync(city.Latitude, city.Longitude);

                if (string.IsNullOrWhiteSpace(pointData.Properties?.ForecastHourly) ||
                    string.IsNullOrWhiteSpace(pointData.Properties.Forecast))
                {
                    cityReports.Add(CityWeatherReport.FromError(
                        city,
                        "Point response did not include forecast links."));
                    continue;
                }

                var hourly = await GetForecastAsync(pointData.Properties.ForecastHourly);
                var daily = await GetForecastAsync(pointData.Properties.Forecast);

                if (hourly.Properties?.Periods is null || hourly.Properties.Periods.Length == 0)
                {
                    cityReports.Add(CityWeatherReport.FromError(
                        city,
                        "Hourly forecast response did not contain periods."));
                    continue;
                }

                var (sunrise, sunset) = GetSunEvents(daily, pointData.Properties.TimeZone);
                var maxRows = Math.Min(hoursToDisplay, hourly.Properties.Periods.Length);
                var hourlyRows = hourly.Properties.Periods
                    .Take(maxRows)
                    .Select(period => new HourWeather(
                        period.StartTime,
                        period.Temperature,
                        period.TemperatureUnit ?? "F",
                        period.ShortForecast ?? "Unknown",
                        ClassifyCondition(period.ShortForecast ?? string.Empty)))
                    .ToArray();

                cityReports.Add(new CityWeatherReport(
                    city.Name,
                    city.Latitude,
                    city.Longitude,
                    sunrise,
                    sunset,
                    hourlyRows,
                    null));
            }
            catch (HttpRequestException ex)
            {
                cityReports.Add(CityWeatherReport.FromError(city, $"Request failed: {ex.Message}"));
            }
            catch (Exception ex)
            {
                cityReports.Add(CityWeatherReport.FromError(city, $"Unexpected error: {ex.Message}"));
            }
        }

        return new WeatherReport(generatedAt, cityReports);
    }

    private async Task<PointResponse> GetPointDataAsync(double latitude, double longitude)
    {
        var endpoint = $"/points/{latitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}";
        var result = await httpClient.GetFromJsonAsync<PointResponse>(endpoint);
        return result ?? throw new InvalidOperationException("Empty points response payload.");
    }

    private async Task<ForecastResponse> GetForecastAsync(string endpointOrUrl)
    {
        var result = await httpClient.GetFromJsonAsync<ForecastResponse>(endpointOrUrl);
        return result ?? throw new InvalidOperationException("Empty forecast response payload.");
    }

    private static (DateTimeOffset? Sunrise, DateTimeOffset? Sunset) GetSunEvents(ForecastResponse daily, string? timeZoneId)
    {
        if (daily.Properties?.Periods is null || daily.Properties.Periods.Length == 0)
        {
            return (null, null);
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

        return (sunrise, sunset);
    }

    private static TimeZoneInfo ResolveTimeZoneOrDefault(string? timeZoneId)
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

    private static string ClassifyCondition(string forecastText)
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
}

public sealed record WeatherReport(DateTimeOffset GeneratedAt, IReadOnlyList<CityWeatherReport> Cities);

public sealed record CityWeatherReport(
    string CityName,
    double Latitude,
    double Longitude,
    DateTimeOffset? Sunrise,
    DateTimeOffset? Sunset,
    IReadOnlyList<HourWeather> Hours,
    string? Error)
{
    public static CityWeatherReport FromError(City city, string error) =>
        new(city.Name, city.Latitude, city.Longitude, null, null, [], error);
}

public sealed record HourWeather(
    DateTimeOffset StartTime,
    int Temperature,
    string TemperatureUnit,
    string ShortForecast,
    string Condition);

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
