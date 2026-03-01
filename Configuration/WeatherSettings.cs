namespace WeatherTracker.Configuration;

public sealed class WeatherSettings
{
    public const string SectionName = "Weather";

    public int HoursToDisplay { get; init; } = 24;

    public List<CitySettings> Cities { get; init; } = [];
}

public sealed class CitySettings
{
    public string CityName { get; init; } = string.Empty;

    public string StateName { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}
