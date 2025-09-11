namespace Algorithm;

public class Zone
{
    public String ZoneKey;
    public List<String> VisitableZones;

    public Zone(String zoneKey, IReadOnlyList<String> zones)
    {
        ZoneKey = zoneKey;
        VisitableZones = zones.ToList();
    }

    public bool CanVisit(String zoneKey)
    {
        return (ZoneKey==zoneKey || VisitableZones.Contains(zoneKey));
    }
}