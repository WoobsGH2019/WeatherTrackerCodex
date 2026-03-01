# Weather Tracker

Simple .NET 10 web app that calls the free Weather.gov API and shows hourly weather for configured cities.

Default cities:

- Greenville, SC (`34.8526, -82.3940`)
- Redmond, WA (`47.6770, -122.1180`)
- New Braunfels, TX (`29.7046, -98.1039`)
- Washington, DC (`38.8979, -77.0365`)

The page includes:

- 24-hour forecast rows per city
- Temperature (F)
- Condition bucket: Sunny / Cloudy / Raining / Snowing / Other
- Estimated sunrise and sunset times (derived from daily day/night period transitions)

## Prereqs

- .NET SDK 10.x

## Run

```bash
dotnet run
```

Then open the URL shown in console output (typically `http://localhost:5000` or `https://localhost:5001`).

Endpoints:

- `/` renders an HTML weather dashboard.
- `/api/weather` returns the same report as JSON.
- `/api/weather/{cityName}` returns one city report as JSON.
  - Example: `/api/weather/Greenville`

## Configuration

Update `appsettings.json`:

```json
{
  "Weather": {
    "HoursToDisplay": 24,
    "Cities": [
      {
        "CityName": "Greenville",
        "StateName": "SC",
        "Latitude": 34.8526,
        "Longitude": -82.394
      }
    ]
  }
}
```

- `Weather:HoursToDisplay` controls how many hourly rows to return/render per city.
- `Weather:Cities` controls the tracked locations without code changes.

## Notes

- No API key required.
- Data source flow:
  - `/points/{lat},{lon}`
  - `forecastHourly` URL from points response
  - `forecast` URL from points response
- Weather.gov does not provide explicit sunrise/sunset in these forecast endpoints, so the app labels them as estimates.
