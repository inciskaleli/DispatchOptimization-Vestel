namespace Algorithm;

public class Technician
{
    public string TechnicianID;
    public string Location;
    public DateTimeOffset StartTime;
    public DateTimeOffset EndTime;
    public string? Zone;
    public List<UnAvailableTimePeriod> UnavailableTimePeriods;
    public List<String> EligibleTasks;
    public TimeSpan CapacityInTermsOfTimeSpan
    {
        get
        {
            return EndTime - StartTime - new TimeSpan(0, (int)UnavailableTimePeriods.Select(u => u.DurationInTimeSpan.TotalMinutes).Sum(), 0);
        }
    }
    public Technician(string key, string? zone, string loc, DateTimeOffset p_start_time, DateTimeOffset p_end_time)
    {
        TechnicianID = key;
        StartTime = p_start_time;
        EndTime = p_end_time;
        Location = loc;
        UnavailableTimePeriods = new List<UnAvailableTimePeriod>();
        Zone = zone;
        EligibleTasks = new List<string>();
    }
}