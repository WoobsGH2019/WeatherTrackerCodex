public sealed class City
{
    public City(string name, double latitude, double longitude)
    {
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
    }

    public string Name { get; }

    public double Latitude { get; }

    public double Longitude { get; }
}
