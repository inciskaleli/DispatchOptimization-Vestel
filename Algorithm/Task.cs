namespace Algorithm;

public class Task
{
    public static Double TaskWeight_TimeWindow;
    public static Double TaskWeight_Location;
    public static Double TaskWeight_Skill;
    public static Double TaskWeight_Priority;
    public static Double TaskWeight_Duration;
    public static Double TaskWeight_Revenue;

    public String TaskID;
    public String Location;
    public DateTimeOffset ArrivalWindowStartTime;
    public DateTimeOffset ArrivalWindowEndTime;
    public int Priority;
    public String? Zone;
    public Double LatenessPenalty;
    public Double OutsourcingCost;
    public DateTimeOffset ArrivalTime;
    public String BusinessUnitID;
    public int RescheduleCount;
    public double TaskDurationInMins;
    public Boolean OptimizeForScore1orRoute0;


    public IReadOnlyList<string> FixedTechnicians;
    public Nullable<DateTimeOffset> FixedStartTime;
    public Nullable<DateTimeOffset> FixedEndTime;
    public Double FixedDurationInMins;
    public Double FixedRevenue = 0;
    readonly Optimizer Optimizer;

    public List<String> EligibleTechnicians
    {
        get
        {
            return FixedTechnicians.Any() ?
                FixedTechnicians.Select(t => t).ToList() :
                Optimizer.Technicians.Where(t => Optimizer.TaskDurationsPerTaskPerTechnician[TaskID].ContainsKey(t.Key) && Optimizer.TaskRevenuesPerTaskPerTechnician[TaskID].ContainsKey(t.Key)).Select(t => t.Key).ToList();
        }
    }
    public Double Score
    {
        get
        {
            return TaskWeight_TimeWindow * Score_TimeWindowSlack +
                TaskWeight_Location * Score_Location +
                TaskWeight_Skill * Score_Skill +
                TaskWeight_Priority * Score_Priority +
                TaskWeight_Duration * Score_Duration +
                TaskWeight_Revenue * MaximumRevenue
                ;
        }
    }

    public Double MaximumRevenue
    {
        get
        {
            if (Optimizer.TaskDurationsPerTaskPerTechnician[TaskID].Count == 0)
                return 0;
            else
                return Optimizer.TaskRevenuesPerTaskPerTechnician[TaskID].Select(d => d.Value).Max();
        }
    }
    public Double MaximumDistanceToTechnicians
    {
        get
        {
            if (Optimizer.TaskRevenuesPerTaskPerTechnician[TaskID].Count == 0)
                return 0;
            else
                return Optimizer.TaskRevenuesPerTaskPerTechnician[TaskID].Select(d => 1).Max(); //TO BE DISCUSSED;
        }
    }
    public Double AverageTaskDurationInMinutes
    {
        get
        {
            if (Optimizer.TaskDurationsPerTaskPerTechnician[TaskID].Count == 0)
                return 0;
            else
                return Optimizer.TaskDurationsPerTaskPerTechnician[TaskID].Select(d => d.Value.TotalMinutes).Average();
        }
    }
    public Double Score_TimeWindowSlack // low when slack is low!
    {
        get
        {
            return ((ArrivalWindowEndTime - ArrivalWindowStartTime).TotalMinutes - TaskDurationInMins) / Optimizer.Tasks.Select(t => (t.Value.ArrivalWindowStartTime - t.Value.ArrivalWindowEndTime).TotalMinutes - t.Value.TaskDurationInMins).Max();
        }
    }

    public Double Score_Location
    {
        get
        {
            return 0; //TO BE DISCUSSED!
        }
    }

    public Double Score_Skill // low when the number of technicians that can serve the task is low!
    {
        get
        {
            return (EligibleTechnicians.Any() ? EligibleTechnicians.Count : 1) / Optimizer.Tasks.Select(t => t.Value.EligibleTechnicians.Any() ? t.Value.EligibleTechnicians.Count : 1).Max();
        }
    }

    public Double Score_Priority //lower ones have higher priority
    {
        get
        {
            return Priority / Optimizer.Tasks.Select(t => t.Value.Priority).Max();
        }
    }

    public Double Score_Duration // low for shorter tasks  --> low for longer tasks
    {
        get
        {
            return TaskDurationInMins / Optimizer.Tasks.Select(t => t.Value.AverageTaskDurationInMinutes).Max();
        }
    }

    public Task(Optimizer optimizer, String key, String loc, String? zone, int priority, DateTimeOffset p_start_time, DateTimeOffset p_end_time, double latenessPenalty,
        IReadOnlyList<string> fixedTechnicians, Nullable<DateTimeOffset> f_start_time, double f_revenue, double outsourcingCost, string businessUnitID, int rescheduleCount, int optimizeForScore1orRoute0)
    {
        Optimizer = optimizer;
        TaskID = key;
        ArrivalWindowStartTime = p_start_time;
        ArrivalWindowEndTime = p_end_time;
        // Burada bug var şu an app'de ondan kaynaklı bilerek değiştirdim
        Priority = priority == 1 ? 4096 : priority == 2 ? 256 : priority == 3 ? 16 : priority == 4 ? 2 : 1;
        LatenessPenalty = latenessPenalty;
        OutsourcingCost = outsourcingCost;
        FixedTechnicians = fixedTechnicians;
        FixedStartTime = f_start_time;
        if (fixedTechnicians.Any())
        {
            ArrivalWindowStartTime = (DateTimeOffset)f_start_time;
            FixedRevenue = f_revenue;
        }
        Zone = zone;
        Location = loc;
        BusinessUnitID = businessUnitID;
        RescheduleCount = rescheduleCount;
        OptimizeForScore1orRoute0 = (optimizeForScore1orRoute0 == 1) ? true : false;
    }

    public double AverageDistanceToAGivenSetOfTasks(List<String> taks)
    {
        double result = 0d;
        //TO BE IMPLEMENTED
        return result;
    }
    public void PrintToFile(StreamWriter srOutput, string reason)
    {
        srOutput.Write(TaskID);
        srOutput.Write("\t" + ArrivalWindowStartTime);
        srOutput.Write("\t" + ArrivalWindowEndTime);
        srOutput.Write("\t" + Location);
        srOutput.Write("\t" + Zone);
        srOutput.Write("\t" + reason);
        srOutput.Write("\n");

    }

    public void PrintToConsole(string reason)
    {
        Console.Write(TaskID);
        Console.Write("\t" + ArrivalWindowStartTime);
        Console.Write("\t" + ArrivalWindowEndTime);
        Console.Write("\t" + Location);
        Console.Write("\t" + Zone);
        Console.Write("\t" + reason);
        Console.Write("\n");

    }
}