namespace Algorithm;

public class Location
{
    public string TaskKey;
    public string TechnicianKey;

    public string LocationKey
    {
        get
        {
            if (TaskKey == "")
                return String.Format("S{0}", TechnicianKey);
            else
                return String.Format("T{0}", TaskKey);
        }
    }
    public bool IsTaskLocation
    {
        get
        {
            return LocationKey.StartsWith("T");
        }
    }
    public bool IsTechnicianHome
    {
        get
        {
            return LocationKey.StartsWith("S") ;
        }
    }
    public Location(string task, string tech)
    {
        TaskKey = task;
        TechnicianKey = tech;
    }
}