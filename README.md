# WeatherTrackerCodex
Codex Weather Tracking App
=======
# Weather Tracker 

Simple .NET 10 console app that calls the free Weather.gov API and prints:

- Hourly weather the following cities
    - Greenville, SC (`34.8526, -82.3940`)
    - Redmond, WA (`47.6770, -122.1180`)
    - New Braunfels, TX (`47.6770, -122.1180)`)
    - Washington, DC (`38.8979, -77.0365`)
- Temperature (F)
- Condition bucket: Sunny / Cloudy / Raining / Snowing
- Estimated sunrise and sunset times (derived from daily day/night period transitions)

## Prereqs

- .NET SDK 10.x

## Run

```bash
dotnet run
```

## Notes

- No API key required.
- Data source flow:
  - `/points/{lat},{lon}`
  - `forecastHourly` URL from points response
  - `forecast` URL from points response
- Weather.gov does not provide explicit sunrise/sunset in these forecast endpoints, so the app labels them as estimates.
