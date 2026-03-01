using System.Net;
using Microsoft.Extensions.Options;
using WeatherTracker.Configuration;
using WeatherTracker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<WeatherGovClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri("https://api.weather.gov");
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherTrackerCodex/1.0 (+mailto:RobertGithub@wubortsoft.com)");
    httpClient.DefaultRequestHeaders.Add("Accept", "application/geo+json");
});

builder.Services
    .AddOptions<WeatherSettings>()
    .Bind(builder.Configuration.GetSection(WeatherSettings.SectionName))
    .Validate(settings => settings.HoursToDisplay > 0, "HoursToDisplay must be greater than 0.")
    .Validate(settings => settings.Cities.Count > 0, "At least one city is required.")
    .ValidateOnStart();

var app = builder.Build();

app.MapGet("/api/weather", async (WeatherGovClient weatherGovClient, IOptions<WeatherSettings> weatherSettings) =>
{
    var settings = weatherSettings.Value;
    var cities = settings.Cities.Select(ToDomainCity);
    var report = await weatherGovClient.GetReportAsync(cities, settings.HoursToDisplay);
    return Results.Ok(report);
});

app.MapGet("/api/weather/{cityName}", async (string cityName, WeatherGovClient weatherGovClient, IOptions<WeatherSettings> weatherSettings) =>
{
    var settings = weatherSettings.Value;
    var selectedCity = settings.Cities.FirstOrDefault(city =>
        string.Equals(city.CityName, cityName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(GetDisplayName(city), cityName, StringComparison.OrdinalIgnoreCase));

    if (selectedCity is null)
    {
        return Results.NotFound(new
        {
            Message = $"City '{cityName}' was not found.",
            AvailableCities = settings.Cities.Select(GetDisplayName)
        });
    }

    var report = await weatherGovClient.GetReportAsync(
    [
        ToDomainCity(selectedCity)
    ], settings.HoursToDisplay);

    var cityReport = report.Cities.FirstOrDefault();
    return cityReport is null
        ? Results.Problem("No report data was returned for the selected city.")
        : Results.Ok(cityReport);
});

app.MapGet("/", async (WeatherGovClient weatherGovClient, IOptions<WeatherSettings> weatherSettings) =>
{
    var settings = weatherSettings.Value;
    var cities = settings.Cities.Select(ToDomainCity);
    var hoursToDisplay = settings.HoursToDisplay;
    var report = await weatherGovClient.GetReportAsync(cities, hoursToDisplay);
    return Results.Content(RenderHtml(report), "text/html");
});

app.Run();

static string RenderHtml(WeatherReport report)
{
    var generatedAt = WebUtility.HtmlEncode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm zzz"));
    var body = $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Weather Tracker</title>
  <style>
    :root { color-scheme: light; }
    body { margin: 0; font-family: "Segoe UI", Tahoma, sans-serif; background: #f6f9fc; color: #1f2937; }
    main { max-width: 1100px; margin: 0 auto; padding: 24px 16px 48px; }
    h1 { margin: 0 0 8px; font-size: 1.8rem; }
    .meta { margin: 0 0 20px; color: #4b5563; }
    .city { background: #fff; border: 1px solid #dbe3ef; border-radius: 10px; padding: 14px; margin-bottom: 14px; box-shadow: 0 1px 2px rgba(0, 0, 0, .04); }
    .city h2 { margin: 0 0 6px; font-size: 1.2rem; }
    .coords { color: #4b5563; margin: 0 0 8px; }
    .sun { margin: 0 0 10px; font-size: .95rem; color: #374151; }
    .error { color: #b91c1c; margin: 0; }
    table { width: 100%; border-collapse: collapse; font-size: .92rem; }
    th, td { text-align: left; padding: 6px 8px; border-top: 1px solid #e5e7eb; vertical-align: top; }
    th { background: #f8fafc; font-weight: 600; }
  </style>
</head>
<body>
  <main>
    <h1>Weather.gov Hourly Forecast</h1>
    <p class="meta">Generated: {{generatedAt}}</p>
    {{string.Join(Environment.NewLine, report.Cities.Select(RenderCitySection))}}
  </main>
</body>
</html>
""";

    return body;
}

static string RenderCitySection(CityWeatherReport city)
{
    var cityName = WebUtility.HtmlEncode(city.CityName);
    var coordinates = WebUtility.HtmlEncode($"{city.Latitude:F6},{city.Longitude:F6}");

    if (!string.IsNullOrWhiteSpace(city.Error))
    {
        var error = WebUtility.HtmlEncode(city.Error);
        return $$"""
<section class="city">
  <h2>{{cityName}}</h2>
  <p class="coords">Coordinates: {{coordinates}}</p>
  <p class="error">{{error}}</p>
</section>
""";
    }

    var sunrise = city.Sunrise is null
        ? "n/a"
        : WebUtility.HtmlEncode(city.Sunrise.Value.ToString("yyyy-MM-dd HH:mm zzz"));
    var sunset = city.Sunset is null
        ? "n/a"
        : WebUtility.HtmlEncode(city.Sunset.Value.ToString("yyyy-MM-dd HH:mm zzz"));

    var rows = string.Join(Environment.NewLine, city.Hours.Select(period =>
    {
        var start = WebUtility.HtmlEncode(period.StartTime.ToString("yyyy-MM-dd HH:mm zzz"));
        var temp = WebUtility.HtmlEncode($"{period.Temperature}{period.TemperatureUnit}");
        var condition = WebUtility.HtmlEncode(period.Condition);
        var forecast = WebUtility.HtmlEncode(period.ShortForecast);
        return $"<tr><td>{start}</td><td>{temp}</td><td>{condition}</td><td>{forecast}</td></tr>";
    }));

    return $$"""
<section class="city">
  <h2>{{cityName}}</h2>
  <p class="coords">Coordinates: {{coordinates}}</p>
  <p class="sun">Sunrise (est): {{sunrise}} | Sunset (est): {{sunset}}</p>
  <table>
    <thead>
      <tr><th>Time</th><th>Temp</th><th>Bucket</th><th>Forecast</th></tr>
    </thead>
    <tbody>
      {{rows}}
    </tbody>
  </table>
</section>
""";
}

static City ToDomainCity(CitySettings citySettings) =>
    new(GetDisplayName(citySettings), citySettings.Latitude, citySettings.Longitude);

static string GetDisplayName(CitySettings citySettings) =>
    $"{citySettings.CityName}, {citySettings.StateName}";
