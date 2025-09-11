using System.Linq;
using System.Threading.Tasks;

namespace Algorithm;
public enum OccupancyType
{
    Task,
    BU,
    Unavailability
}
public class Occupancy
{
    public OccupancyType Type;
    public String ID;
    public DateTimeOffset StartTime;
    public DateTimeOffset EndTime;
    public TimeSpan PreSlack;
    public TimeSpan PostSlack;
    public Boolean isFixed;
    public String EndingLocation;

    public Occupancy(OccupancyType ot, string id, DateTimeOffset st, Boolean isF, string eLoc)
    {
        Type =ot;
        ID =id;
        StartTime =st;
        isFixed =isF;
        PreSlack =new TimeSpan(0);
        PostSlack =new TimeSpan(0);
        EndingLocation = eLoc;
    }
    public Occupancy(OccupancyType ot, string id, DateTimeOffset st, DateTimeOffset et, Boolean isF, string eLoc)
    {
        Type = ot;
        ID = id;
        StartTime = st;
        EndTime = et;
        isFixed = isF;
        PreSlack = new TimeSpan(0);
        PostSlack = new TimeSpan(0);
        EndingLocation = eLoc;
    }

}
public class TechnicianSchedule
{
    public String TechnicianKey;
    public Dictionary<String, DateTimeOffset> OrderedTasksBUsAndVisitTimes;
    public Dictionary<String, DateTimeOffset> OrderedTasksBUsAndDispatchTimes;
    public Dictionary<String, Double> CumulativeRouteDistanceUptoTask;
    public Dictionary<String, Double> CumulativeRouteDurationInMinsUptoTask;
    public Dictionary<String, Double> CumulativeRouteDurationInMinsUptoTask_forResponse;
    public String LastTaskBeforeLunchBreak = "";
    readonly Optimizer Optimizer;
    readonly Solution Solution;
    public DateTimeOffset LunchBreakStartTime = new DateTimeOffset();

    public String LastTaskLocation
    {
        get
        {
            for (int i = (OrderedTasksBUsAndVisitTimes.Count - 1); i >= 0; i--)
            {
                var taskORbu = OrderedTasksBUsAndVisitTimes.ElementAt(i).Key;
                if (Optimizer.Tasks.ContainsKey(taskORbu))
                    return Optimizer.Tasks[taskORbu].Location;

            }
            return Optimizer.Technicians[TechnicianKey].Location;
        }
    }
    public List<String> ServedTasks
    {
        get
        {
            if (!OrderedTasksBUsAndVisitTimes.Any())
                return new List<String>();
            return OrderedTasksBUsAndVisitTimes.Where(t => Optimizer.Tasks.ContainsKey(t.Key)).Select(t => t.Key).ToList();
        }
    }
    
    public List<String> ServedBUs
    {
        get
        {
            if (!OrderedTasksBUsAndVisitTimes.Any())
                return new List<String>();
            return OrderedTasksBUsAndVisitTimes.Where(t => Optimizer.BusinessUnits.ContainsKey(t.Key)).Select(t => t.Key).ToList();
        }
    }
    public double Cost
    {
        get
        {
            return TravellingCost /*+ LatenessCost + EarlinessCost*/;
        }
    }
    public TimeSpan WorkingTime
    {
        get
        {
            return (Optimizer.Technicians[TechnicianKey].EndTime - (Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon))- TotalUnavailableTime;
        }
    }
    public TimeSpan TotalUnavailableTime
    {
        get
        {
            var startTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
            var endTime = Optimizer.EndOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].EndTime ? Optimizer.EndOfPlanningHorizon: Optimizer.Technicians[TechnicianKey].EndTime;
            var relavantUAs = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => startTime <= u.endTime && endTime >= u.startTime ).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => startTime <= u.endTime && endTime >= u.startTime).Select(u=>((u.endTime>endTime?endTime:u.endTime)-(u.startTime<startTime?startTime:u.startTime))).ToList() : new List<TimeSpan>();
            TimeSpan result = new TimeSpan();
            foreach (var ua in relavantUAs)
                result.Add(ua);
            return result;
        }
    }
    public double TravellingCost
    {
        get
        {
            double result = 0d;
            var currentLocation = Optimizer.Technicians[TechnicianKey].Location;
            double cumulativeRouteDurationInMins = 0d;
            double cumulativeRouteDurationInMins_forResponse = 0d;
            double cumulativeRouteDistance = 0d;
            CumulativeRouteDistanceUptoTask = new Dictionary<string, double>();
            CumulativeRouteDurationInMinsUptoTask = new Dictionary<string, double>();
            CumulativeRouteDurationInMinsUptoTask_forResponse = new Dictionary<string, double>();

            for (int i = 0; i < OrderedTasksBUsAndVisitTimes.Count; i++)
            {
                var task = OrderedTasksBUsAndVisitTimes.ElementAt(i).Key;
                result += Optimizer.Tasks.ContainsKey(task) ? Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[task].Location) : 0;
                cumulativeRouteDistance += Optimizer.Tasks.ContainsKey(task) ? Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[task].Location) : 0;
                cumulativeRouteDurationInMins += Optimizer.Tasks.ContainsKey(task) ? (i == 0 ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[task].Location)) : 0;
                cumulativeRouteDurationInMins_forResponse += Optimizer.Tasks.ContainsKey(task) ? (i == 0 ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[task].Location, true)) : 0;
                currentLocation = Optimizer.Tasks.ContainsKey(task) ? Optimizer.Tasks[task].Location : currentLocation;
                CumulativeRouteDistanceUptoTask.Add(task, cumulativeRouteDistance);
                CumulativeRouteDurationInMinsUptoTask.Add(task, cumulativeRouteDurationInMins);
                CumulativeRouteDurationInMinsUptoTask_forResponse.Add(task, cumulativeRouteDurationInMins_forResponse);
            }
            result += Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Technicians[TechnicianKey].Location);

            return result;
        }
    }
    public double TravellingTime
    {
        get
        {
            double result = 0d;
            var currentLocation = Optimizer.Technicians[TechnicianKey].Location;
            double cumulativeRouteDurationInMins = 0d;
            double cumulativeRouteDurationInMins_forResponse = 0d;
            double cumulativeRouteDistance = 0d;
            CumulativeRouteDistanceUptoTask = new Dictionary<string, double>();
            CumulativeRouteDurationInMinsUptoTask = new Dictionary<string, double>();
            CumulativeRouteDurationInMinsUptoTask_forResponse = new Dictionary<string, double>();

            for (int i = 0; i < OrderedTasksBUsAndVisitTimes.Count; i++)
            {
                var task = OrderedTasksBUsAndVisitTimes.ElementAt(i).Key;
                //result += Optimizer.Tasks.ContainsKey(task) ? Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[task].Location) : 0;
                cumulativeRouteDistance += Optimizer.Tasks.ContainsKey(task) ? Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[task].Location) : 0;
                cumulativeRouteDurationInMins += Optimizer.Tasks.ContainsKey(task) ? (i == 0 ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[task].Location)) : 0;
                cumulativeRouteDurationInMins_forResponse += Optimizer.Tasks.ContainsKey(task) ? (i == 0 ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[task].Location, true)) : 0;
                currentLocation = Optimizer.Tasks.ContainsKey(task) ? Optimizer.Tasks[task].Location : currentLocation;
                CumulativeRouteDistanceUptoTask.Add(task, cumulativeRouteDistance);
                CumulativeRouteDurationInMinsUptoTask.Add(task, cumulativeRouteDurationInMins);
                CumulativeRouteDurationInMinsUptoTask_forResponse.Add(task, cumulativeRouteDurationInMins_forResponse);
            }
            result += CumulativeRouteDurationInMinsUptoTask_forResponse.Select(t=>t.Value).Sum();

            return result;
        }
    }
    public double LatenessCost
    {
        get
        {
            double result = 0d;

            for (int i = 0; i < OrderedTasksBUsAndVisitTimes.Count; i++)
            {
                if ((OrderedTasksBUsAndVisitTimes.ElementAt(i).Value - Optimizer.Tasks[OrderedTasksBUsAndVisitTimes.ElementAt(i).Key].ArrivalWindowStartTime) > new TimeSpan(0, 0, 0))
                    result += (OrderedTasksBUsAndVisitTimes.ElementAt(i).Value - Optimizer.Tasks[OrderedTasksBUsAndVisitTimes.ElementAt(i).Key].ArrivalWindowStartTime).TotalMinutes;

            }

            return result * Optimizer.UnitLatenessPenalty;
        }
    }

    public double EarlinessCost
    {
        get
        {
            double result = 0d;

            for (int i = 0; i < OrderedTasksBUsAndVisitTimes.Count; i++)
            {
                var task = OrderedTasksBUsAndVisitTimes.ElementAt(i).Key;
                var time = OrderedTasksBUsAndVisitTimes.ElementAt(i).Value;
                if (Optimizer.Tasks.ContainsKey(task) && (Optimizer.Tasks[task].ArrivalWindowStartTime - time) > new TimeSpan(0, 0, 0))
                    result += (Optimizer.Tasks[task].ArrivalWindowStartTime - time).TotalMinutes;

            }

            return result * Optimizer.EarlinessCost;
        }
    }

    public double Revenue
    {
        get
        {
            if (Optimizer.BestTech1orEquity0 > 0) //besttech
                return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? 
                (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? 
                    Optimizer.Tasks[t.Key].FixedRevenue 
                    : 
                    (Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 ? 
                        Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey]
                        : 
                        (Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] < -10 ? (-1*Optimizer.UnservedTaskPenalty/2) : 0))) 
                : 
                Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
            else
                return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? 
                (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? 
                    Optimizer.Tasks[t.Key].FixedRevenue 
                    : 
                    ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] >=0) ? 
                        1000 /*positve score based equity*/ 
                        : 
                        ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] ==-1) ? 
                            0 /* "not nice2have" when equity*/ 
                            :
                            (Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] <-1 ?  (-1*Optimizer.UnservedTaskPenalty/2) /* worst assignment but ok, not to unserve */ : 0)))) 
                : 
                500).Sum();
            //if (!Optimizer.AssignPriorityJobsFirst && !Optimizer.MinimizeWeightedCompletionTime)
            //return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedRevenue : ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] < -10) ? Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] : 0)) : Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
            //else
            //  return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && (Optimizer.Tasks[t.Key].FixedTechnicians.Any() || Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] >= -1)) ? 1000 : 0) : Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
        }
    }

    public double PrioritizedRevenue
    {
        get
        {
            //if (Optimizer.BestTech1orEquity0) //besttech
                return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ?
                (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ?
                    0//Optimizer.Tasks[t.Key].Priority*Optimizer.Tasks[t.Key].FixedRevenue
                    :
                    (Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 ?
                        Optimizer.Tasks[t.Key].Priority * Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey]
                        :
                        (Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] < -10 ? (-1 * Optimizer.UnservedTaskPenalty / 2) : 0)))
                :
                Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
            /*else
                return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ?
                (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ?
                    Optimizer.Tasks[t.Key].Priority * Optimizer.Tasks[t.Key].FixedRevenue
                    :
                    ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] >= 0) ?
                        Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] --positve score based equity
                        :
                        ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] == -1) ?
                            0 -- "not nice2have" when equity
                            :
                            (Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] < -1 ? (-1 * Optimizer.UnservedTaskPenalty / 2) -- worst assignment but ok, not to unserve  : 0))))
                :
                500).Sum();*/
            //if (!Optimizer.AssignPriorityJobsFirst && !Optimizer.MinimizeWeightedCompletionTime)
            //return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedRevenue : ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] < -10) ? Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] : 0)) : Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
            //else
            //  return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? ((Optimizer.Tasks[t.Key].OptimizeForScore1orRoute0 && (Optimizer.Tasks[t.Key].FixedTechnicians.Any() || Optimizer.TaskRevenuesPerTaskPerTechnician[t.Key][TechnicianKey] >= -1)) ? 1000 : 0) : Optimizer.BusinessUnits[t.Key].ExpectedRevenue).Sum();
        }
    }

    public double Workload //For equity
    {
        get
        {
            return OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks.ContainsKey(t.Key) ? (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes) : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes).Sum() / WorkingTime.TotalMinutes;
        }
    }
    public TechnicianSchedule(Optimizer o, Solution solution, string tKey)
    {
        Optimizer = o;
        Solution = solution;
        TechnicianKey = tKey;
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        OrderedTasksBUsAndDispatchTimes = new Dictionary<string, DateTimeOffset>();
        CumulativeRouteDistanceUptoTask = new Dictionary<string, double>();
        CumulativeRouteDurationInMinsUptoTask = new Dictionary<string, double>();
        LunchBreakStartTime = o.BeginningOfPlanningHorizon;
    }

    public TechnicianSchedule(TechnicianSchedule copy)
    {
        Optimizer = copy.Optimizer;
        Solution = copy.Solution;
        TechnicianKey = copy.TechnicianKey;
        OrderedTasksBUsAndVisitTimes = copy.OrderedTasksBUsAndVisitTimes.ToDictionary(t => t.Key, t => t.Value);
        OrderedTasksBUsAndDispatchTimes = copy.OrderedTasksBUsAndDispatchTimes.ToDictionary(t => t.Key, t => t.Value);
        LunchBreakStartTime = copy.LunchBreakStartTime;
        LastTaskBeforeLunchBreak = copy.LastTaskBeforeLunchBreak;
        CumulativeRouteDistanceUptoTask = copy.CumulativeRouteDistanceUptoTask.ToDictionary(t => t.Key, t => t.Value);
        CumulativeRouteDurationInMinsUptoTask = copy.CumulativeRouteDurationInMinsUptoTask.ToDictionary(t => t.Key, t => t.Value);
    }
    public bool Assign(String taskKey, CancellationToken cancellationToken)
    {
        List<String> ListOfTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Key).ToList();
        ListOfTasks.Add(taskKey);
        if (Optimizer.RespectScheduledTimes)
            GenerateFixedSchedule(ListOfTasks, cancellationToken);
        else
        {
            Reschedule(ListOfTasks, cancellationToken);
            var newTechSchedule = new TechnicianSchedule(this);
            newTechSchedule.RescheduleWithClosestEarliestStart(ListOfTasks, cancellationToken);
            double weightedSumOfServedTasks_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
            double weightedSumOfServedTasks_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
            double weightedSumOfServedTaskStartTimes_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
            double weightedSumOfServedTaskStartTimes_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();

            if (weightedSumOfServedTasks_new >= weightedSumOfServedTasks_current ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && (newTechSchedule.Revenue - newTechSchedule.Cost) >= (Revenue - Cost)) ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && weightedSumOfServedTaskStartTimes_new <= weightedSumOfServedTaskStartTimes_current))
            {
                OrderedTasksBUsAndVisitTimes = newTechSchedule.OrderedTasksBUsAndVisitTimes.ToDictionary(t => t.Key, t => t.Value);
                LunchBreakStartTime = newTechSchedule.LunchBreakStartTime;
                LastTaskBeforeLunchBreak = newTechSchedule.LastTaskBeforeLunchBreak;
            }
            if (Optimizer.DistanceWeight > 0)
            {
                newTechSchedule = new TechnicianSchedule(this);
                weightedSumOfServedTasks_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                weightedSumOfServedTaskStartTimes_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
                newTechSchedule.RescheduleWithNearestNeighbor(ListOfTasks, cancellationToken);
                weightedSumOfServedTasks_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                weightedSumOfServedTaskStartTimes_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
                if (weightedSumOfServedTasks_new >= weightedSumOfServedTasks_current ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && (newTechSchedule.Revenue - newTechSchedule.Cost) >= (Revenue - Cost)) ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && weightedSumOfServedTaskStartTimes_new <= weightedSumOfServedTaskStartTimes_current))
                {
                    OrderedTasksBUsAndVisitTimes = newTechSchedule.OrderedTasksBUsAndVisitTimes.ToDictionary(t => t.Key, t => t.Value);
                    LunchBreakStartTime = newTechSchedule.LunchBreakStartTime;
                    LastTaskBeforeLunchBreak = newTechSchedule.LastTaskBeforeLunchBreak;
                }
            }
            //RescheduleWithClosestEarliestStart(ListOfTasks, cancellationToken);
            if (Optimizer.AssignPriorityJobsFirst || Optimizer.MinimizeWeightedCompletionTime)
            {
                newTechSchedule = new TechnicianSchedule(this);
                weightedSumOfServedTasks_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                weightedSumOfServedTaskStartTimes_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
                newTechSchedule.RescheduleWithLargestPriorityFirst(ListOfTasks, cancellationToken);
                weightedSumOfServedTasks_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                weightedSumOfServedTaskStartTimes_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
                if (weightedSumOfServedTasks_new >= weightedSumOfServedTasks_current ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && (newTechSchedule.Revenue - newTechSchedule.Cost) >= (Revenue - Cost)) ||
                (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && weightedSumOfServedTaskStartTimes_new <= weightedSumOfServedTaskStartTimes_current))
                {
                    OrderedTasksBUsAndVisitTimes = newTechSchedule.OrderedTasksBUsAndVisitTimes.ToDictionary(t => t.Key, t => t.Value);
                    LunchBreakStartTime = newTechSchedule.LunchBreakStartTime;
                    LastTaskBeforeLunchBreak = newTechSchedule.LastTaskBeforeLunchBreak;
                }
                //RescheduleWithLargestPriorityFirst(ListOfTasks, cancellationToken);
            }
            for (int i = 0; i < ListOfTasks.Count; i++)
            {
                List<String> OrderedListOfTasks = ListOfTasks.ToList();
                OrderedListOfTasks.RemoveAt(ListOfTasks.Count - 1);
                OrderedListOfTasks.Insert(i, taskKey);
                newTechSchedule = new TechnicianSchedule(this);
                if (newTechSchedule.isFeasible(OrderedListOfTasks))
                {
                    weightedSumOfServedTasks_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                    weightedSumOfServedTasks_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority).Sum();
                    weightedSumOfServedTaskStartTimes_new = newTechSchedule.OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
                    weightedSumOfServedTaskStartTimes_current = OrderedTasksBUsAndVisitTimes.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();

                    if (weightedSumOfServedTasks_new >= weightedSumOfServedTasks_current ||
                    (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && (newTechSchedule.Revenue - newTechSchedule.Cost) >= (Revenue - Cost)) ||
                    (weightedSumOfServedTasks_new == weightedSumOfServedTasks_current && weightedSumOfServedTaskStartTimes_new <= weightedSumOfServedTaskStartTimes_current))
                    {
                        OrderedTasksBUsAndVisitTimes = newTechSchedule.OrderedTasksBUsAndVisitTimes.ToDictionary(t => t.Key, t => t.Value);
                        LunchBreakStartTime = newTechSchedule.LunchBreakStartTime;
                        LastTaskBeforeLunchBreak = newTechSchedule.LastTaskBeforeLunchBreak;
                    }
                }
            }

        }
        return OrderedTasksBUsAndVisitTimes.ContainsKey(taskKey);
    }
    public void GenerateFixedSchedule(List<String> ListOfTasks, CancellationToken cancellationToken)
    {
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => ListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        var PrioritizedListOfTasks = ListOfTasks.OrderByDescending(t=>Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].Priority : 1).ToList();

        var OrderedListOfTasks = ListOfFixedTasks.OrderBy(t=> Optimizer.Tasks[t.Key].FixedStartTime).Select(t=>t.Key).ToList();
        foreach (var task in PrioritizedListOfTasks)
        {
            if (OrderedListOfTasks.Contains(task))
                continue;
            var newOrderedListOfTasks = OrderedListOfTasks.ToList();
            newOrderedListOfTasks.Add(task);
            newOrderedListOfTasks = newOrderedListOfTasks.OrderBy(t => Optimizer.Tasks[t].FixedStartTime).Select(t => t).ToList();
            if(isFeasibleFixedSchedule(newOrderedListOfTasks))
                OrderedListOfTasks = OrderedTasksBUsAndVisitTimes.Select(t=>t.Key).ToList();
        }
        OrderedTasksBUsAndVisitTimes = OrderedListOfTasks.ToDictionary(t => t, t => Optimizer.Tasks[t].FixedStartTime.GetValueOrDefault());
    }

    public bool isFeasibleFixedSchedule(List<String> OrderedListOfTasks)

    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        OrderedTasksBUsAndDispatchTimes = new Dictionary<string, DateTimeOffset>();
        if (!OrderedListOfTasks.Any())
            return true;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter == Optimizer.EndOfPlanningHorizon ? true : false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => OrderedListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        OrderedListOfTasks = OrderedListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                    currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime = ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = (OrderedListOfTasks.Any() && (Optimizer.Tasks[OrderedListOfTasks.ElementAt(0)].FixedStartTime <= nextUnavailabilityStartTime)) ? OrderedListOfTasks.ElementAt(0) : "";
            if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask))
            {
                if (Optimizer.Tasks[closestTask].ArrivalWindowEndTime < currentTime)
                    return false;
                if (currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) > Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                    return false; /*1. if this is a task, then it should be visitable within its time window */
                if (currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes) > nextUnavailabilityStartTime ||
                    Optimizer.Tasks[closestTask].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes) > nextUnavailabilityStartTime)
                    return false;/*2. if this is a task, then it should be completable before the next unavailability */
                if ((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) > Optimizer.DispatchTimeInMins ||
                    Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    return false; /*3. if this is a task, then it should be within the dispatch distance and time */
            }
            else if (Optimizer.BusinessUnits.ContainsKey(closestTask))
            {
                if (currentTime.AddMinutes(Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
                    return false; /*1. if this is a business unit slot, then it should be completable before the next unavailability */

            }
            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                OrderedTasksBUsAndDispatchTimes.Add(closestTask, currentTime);
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                if (currentTime > Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault())
                    return false;
                else
                    currentTime = Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault();
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                OrderedListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if (currentTime < Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;
            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!OrderedListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            OrderedListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (OrderedListOfTasks.Any())
            return false;
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
        return true;
    }
    public bool isFeasible(List<String> OrderedListOfTasks)
    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        OrderedTasksBUsAndDispatchTimes = new Dictionary<string, DateTimeOffset>();
        if (!OrderedListOfTasks.Any())
            return true;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter == Optimizer.EndOfPlanningHorizon ? true : false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => OrderedListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        OrderedListOfTasks = OrderedListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                    currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime = ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = OrderedListOfTasks.Any() ? OrderedListOfTasks.ElementAt(0) : "";
            if(Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask))
            {
                if (Optimizer.Tasks[closestTask].ArrivalWindowEndTime < currentTime)
                    return false;
                if (currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) > Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                    return false; /*1. if this is a task, then it should be visitable within its time window */
                if (currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes) > nextUnavailabilityStartTime ||
                    Optimizer.Tasks[closestTask].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes) > nextUnavailabilityStartTime )
                    return false;/*2. if this is a task, then it should be completable before the next unavailability */
                if ((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) > Optimizer.DispatchTimeInMins  ||
                    Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    return false; /*3. if this is a task, then it should be within the dispatch distance and time */
            }
            else if (Optimizer.BusinessUnits.ContainsKey(closestTask))
            {
                if(currentTime.AddMinutes(Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
                    return false; /*1. if this is a business unit slot, then it should be completable before the next unavailability */

            }
           
            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                OrderedTasksBUsAndDispatchTimes.Add(closestTask, currentTime);
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                OrderedListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if (currentTime < Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;
            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!OrderedListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            OrderedListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (OrderedListOfTasks.Any())
            return false;
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
        return true;
    }

    public void Reschedule(List<String> ListOfTasks, CancellationToken cancellationToken)
    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        OrderedTasksBUsAndDispatchTimes = new Dictionary<string, DateTimeOffset>();
        if (!ListOfTasks.Any())
            return;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime? Optimizer.Technicians[TechnicianKey].StartTime: Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter==Optimizer.EndOfPlanningHorizon? true: false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => ListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        ListOfTasks = ListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                   currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime =   ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = "";
            ListOfTasks = ListOfTasks.Where(t => !Optimizer.Tasks.ContainsKey(t) /* it is BU slot*/ || (Optimizer.Tasks[t].ArrivalWindowEndTime > currentTime)).ToList();
            if (ListOfTasks.DistinctBy(t => t).Count() != ListOfTasks.Count())
                Console.WriteLine("problem!");
            var LatestStartTimes = ListOfTasks.Where(t =>
            ( /*1. if this is a task, then it should be visitable within its time window */ Optimizer.Tasks.ContainsKey(t) &&
            (//Optimizer.Tasks[t].ArrivalWindowStartTime <= currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) &&
            currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) < Optimizer.Tasks[t].ArrivalWindowEndTime)
            &&
            /*2. if this is a task, then it should be completable before the next unavailability */ 
            currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            Optimizer.Tasks[t].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            /*3. if this is a task, then it should be within the dispatch distance and time */
            (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) <= Optimizer.DispatchTimeInMins &&
            Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[t].Location) <= Optimizer.DispatchDistance)
            ||
            /*1. if this is a business unit slot, then it should be completable before the next unavailability */
            (Optimizer.BusinessUnits.ContainsKey(t) &&
            currentTime.AddMinutes(Optimizer.BusinessUnits[t].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
            //||
            /*2. if this is a business unit slot, another alternative condition exists (I forgot what it is) */
            //Optimizer.MasterBusinessUnits[Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].BusinessUnitID: Optimizer.BusinessUnits[t].BusinessUnitID] /*disabled*/
            ).ToDictionary(t => t, t => (Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].ArrivalWindowEndTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowEndTime).Max()));

            if (LatestStartTimes.Any())
            {
                //1.earliest arrival window end time, 2. earliest arrival window start time
                LatestStartTimes = LatestStartTimes.OrderBy(t => (t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)).ThenBy(t => ((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) <= currentTime) ? 0 : Math.Abs(((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) - currentTime).TotalMinutes)).ToDictionary(t => t.Key, t => t.Value);
                closestTask = LatestStartTimes.ElementAt(0).Key;
                //closestTask = LatestStartTimes.Where(t => ((t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)) == closestLatestStartTime).Select(t => t.Key).ElementAt(0);
            }


            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                OrderedTasksBUsAndDispatchTimes.Add(closestTask, currentTime);
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                ListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if(currentTime< Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;

            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!ListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            ListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
    }

    public void RescheduleWithNearestNeighbor(List<String> ListOfTasks, CancellationToken cancellationToken)
    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        OrderedTasksBUsAndDispatchTimes = new Dictionary<string, DateTimeOffset>();
        if (!ListOfTasks.Any())
            return;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter == Optimizer.EndOfPlanningHorizon ? true : false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => ListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        ListOfTasks = ListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                    currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime = ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = "";
            ListOfTasks = ListOfTasks.Where(t => !Optimizer.Tasks.ContainsKey(t) /* it is BU slot*/ || (Optimizer.Tasks[t].ArrivalWindowEndTime > currentTime)).ToList();
            if (ListOfTasks.DistinctBy(t => t).Count() != ListOfTasks.Count())
                Console.WriteLine("problem!");
            var LatestStartTimes = ListOfTasks.Where(t =>
            ( /*1. if this is a task, then it should be visitable within its time window */ Optimizer.Tasks.ContainsKey(t) &&
            (//Optimizer.Tasks[t].ArrivalWindowStartTime <= currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) &&
            currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) < Optimizer.Tasks[t].ArrivalWindowEndTime)
            &&
            /*2. if this is a task, then it should be completable before the next unavailability */
            currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            Optimizer.Tasks[t].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            /*3. if this is a task, then it should be within the dispatch distance and time */
            (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) <= Optimizer.DispatchTimeInMins &&
            Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[t].Location) <= Optimizer.DispatchDistance)
            ||
            /*1. if this is a business unit slot, then it should be completable before the next unavailability */
            (Optimizer.BusinessUnits.ContainsKey(t) &&
            currentTime.AddMinutes(Optimizer.BusinessUnits[t].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
            //||
            /*2. if this is a business unit slot, another alternative condition exists (I forgot what it is) */
            //Optimizer.MasterBusinessUnits[Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].BusinessUnitID: Optimizer.BusinessUnits[t].BusinessUnitID] /*disabled*/
            ).ToDictionary(t => t, t => (Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].ArrivalWindowEndTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowEndTime).Max()));

            if (LatestStartTimes.Any())
            {
                //var closestDistance = LatestStartTimes.Select(t => (Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location) : 0)).Min();
                LatestStartTimes = LatestStartTimes.OrderBy(t => (Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location) : 0)).ThenBy(t => ((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) <= currentTime) ? 0 : Math.Abs(((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) - currentTime).TotalMinutes)).ToDictionary(t => t.Key, t => t.Value);
                closestTask = LatestStartTimes.ElementAt(0).Key;

            }


            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                OrderedTasksBUsAndDispatchTimes.Add(closestTask, currentTime);
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                ListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if (currentTime < Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;

            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!ListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            ListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
    }

    public void RescheduleWithClosestEarliestStart(List<String> ListOfTasks, CancellationToken cancellationToken)
    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        if (!ListOfTasks.Any())
            return;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter == Optimizer.EndOfPlanningHorizon ? true : false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => ListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        ListOfTasks = ListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                    currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime = ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = "";
            ListOfTasks = ListOfTasks.Where(t => !Optimizer.Tasks.ContainsKey(t) /* it is BU slot*/ || (Optimizer.Tasks[t].ArrivalWindowEndTime > currentTime)).ToList();
            if (ListOfTasks.DistinctBy(t => t).Count() != ListOfTasks.Count())
                Console.WriteLine("problem!");
            var LatestStartTimes = ListOfTasks.Where(t =>
            ( /*1. if this is a task, then it should be visitable within its time window */ Optimizer.Tasks.ContainsKey(t) &&
            (//Optimizer.Tasks[t].ArrivalWindowStartTime <= currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) &&
            currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) < Optimizer.Tasks[t].ArrivalWindowEndTime)
            &&
            /*2. if this is a task, then it should be completable before the next unavailability */
            currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            Optimizer.Tasks[t].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            /*3. if this is a task, then it should be within the dispatch distance and time */
            (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) <= Optimizer.DispatchTimeInMins &&
            Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[t].Location) <= Optimizer.DispatchDistance)
            ||
            /*1. if this is a business unit slot, then it should be completable before the next unavailability */
            (Optimizer.BusinessUnits.ContainsKey(t) &&
            currentTime.AddMinutes(Optimizer.BusinessUnits[t].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
            //||
            /*2. if this is a business unit slot, another alternative condition exists (I forgot what it is) */
            //Optimizer.MasterBusinessUnits[Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].BusinessUnitID: Optimizer.BusinessUnits[t].BusinessUnitID] /*disabled*/
            ).ToDictionary(t => t, t => (Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].ArrivalWindowEndTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowEndTime).Max()));



            if (LatestStartTimes.Any())
            {

                //1.earliest arrival window start time, 2. earliest arrival window end time
                LatestStartTimes = LatestStartTimes.OrderBy(t => ((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) <= currentTime) ? 0 : Math.Abs(((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) - currentTime).TotalMinutes)).
                    ThenBy(t => (t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)).ToDictionary(t => t.Key, t => t.Value);
                closestTask = LatestStartTimes.ElementAt(0).Key;

                //var closestEarliestStartTime = EarliestStartTimes.Select(t => (t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)).Min();
                //closestTask = EarliestStartTimes.Where(t => ((t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)) == closestEarliestStartTime).Select(t => t.Key).ElementAt(0);
            }


            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                ListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if (currentTime < Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;
            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime && 
                    !lunchBreakScheduled)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!ListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            ListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
    }

    public void RescheduleWithLargestPriorityFirst(List<String> ListOfTasks, CancellationToken cancellationToken)
    {
        OrderedTasksBUsAndVisitTimes = new Dictionary<string, DateTimeOffset>();
        if (!ListOfTasks.Any())
            return;
        var startingLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentLocation = startingLocation;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        currentTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).Any() ?
            Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => currentTime >= u.startTime && currentTime <= u.endTime).OrderByDescending(u => u.endTime).ElementAt(0).endTime : currentTime;
        double elapsedTimeInMins = 0;// (Optimizer.Technicians[TechnicianKey].StartTime - Optimizer.Now).TotalMinutes;
        double availableTimeInMins = (Optimizer.Technicians[TechnicianKey].EndTime - Optimizer.Technicians[TechnicianKey].StartTime).TotalMinutes;
        bool lunchBreakScheduled = Optimizer.LunchBreakAfter == Optimizer.EndOfPlanningHorizon ? true : false;
        var ListOfFixedTasks = Optimizer.Tasks.Where(t => ListOfTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any()).ToDictionary(t => t.Key, t => t.Value);
        ListOfTasks = ListOfTasks.Where(t => !ListOfFixedTasks.ContainsKey(t)).Select(t => t).ToList();
        bool firstTask = true;
        while (currentTime < Optimizer.Technicians[TechnicianKey].EndTime)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            DateTimeOffset nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            DateTimeOffset nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
            String nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                "";
            if (Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Any())
            {
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime > nextUnavailabilityEndTime).Select(u => u.endTime).Max();
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => u.startTime >= nextUnavailabilityStartTime && u.startTime <= nextUnavailabilityEndTime && u.endTime == nextUnavailabilityEndTime).ElementAt(0).endingLocation;
            }
            if (nextUnavailabilityStartTime <= currentTime)
            {
                if (currentTime == nextUnavailabilityEndTime)
                    currentTime = currentTime.AddMinutes(1); //to handle instances with unavailabilities having identical start and end times
                else
                    currentTime = nextUnavailabilityEndTime;
                nextUnavailabilityStartTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).startTime :
                Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndTime = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endTime :
                    Optimizer.Technicians[TechnicianKey].EndTime;
                nextUnavailabilityEndingLocation = Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime)) && u.startTime < Optimizer.Technicians[TechnicianKey].EndTime).Any() ?
                    Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Where(u => ((u.startTime >= currentTime) || (currentTime > u.startTime && currentTime <= u.endTime))).OrderBy(u => u.startTime).ElementAt(0).endingLocation :
                    "";
            }
            String reasonOfNextUnavailability = "unav";
            if (ListOfFixedTasks.Any() && ListOfFixedTasks.Select(t => t.Value.FixedStartTime).Min() < nextUnavailabilityStartTime)
            {
                nextUnavailabilityStartTime = ListOfFixedTasks.Select(t => t.Value.FixedStartTime.GetValueOrDefault()).Min();
                var fixedTask = ListOfFixedTasks.Where(t => t.Value.FixedStartTime == nextUnavailabilityStartTime).OrderBy(t => t.Value.FixedStartTime.GetValueOrDefault().AddMinutes(t.Value.FixedDurationInMins)).Select(t => t.Key).ElementAt(0);
                nextUnavailabilityEndTime = nextUnavailabilityStartTime.AddMinutes(Optimizer.Tasks[fixedTask].FixedDurationInMins);
                nextUnavailabilityEndingLocation = Optimizer.Tasks[fixedTask].Location;
                reasonOfNextUnavailability = fixedTask;
            }
            string closestTask = "";
            ListOfTasks = ListOfTasks.Where(t => !Optimizer.Tasks.ContainsKey(t) /* it is BU slot*/ || (Optimizer.Tasks[t].ArrivalWindowEndTime > currentTime)).ToList();
            if (ListOfTasks.DistinctBy(t => t).Count() != ListOfTasks.Count())
                Console.WriteLine("problem!");
            var LatestStartTimes = ListOfTasks.Where(t =>
            ( /*1. if this is a task, then it should be visitable within its time window */ Optimizer.Tasks.ContainsKey(t) &&
            (//Optimizer.Tasks[t].ArrivalWindowStartTime <= currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) &&
            currentTime.AddMinutes(firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) < Optimizer.Tasks[t].ArrivalWindowEndTime)
            &&
            /*2. if this is a task, then it should be completable before the next unavailability */
            currentTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            Optimizer.Tasks[t].ArrivalWindowStartTime.AddMinutes((firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) + Optimizer.TaskDurationsPerTaskPerTechnician[t][TechnicianKey].TotalMinutes) <= nextUnavailabilityStartTime &&
            /*3. if this is a task, then it should be within the dispatch distance and time */
            (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t].Location)) <= Optimizer.DispatchTimeInMins &&
            Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[t].Location) <= Optimizer.DispatchDistance)
            ||
            /*1. if this is a business unit slot, then it should be completable before the next unavailability */
            (Optimizer.BusinessUnits.ContainsKey(t) &&
            currentTime.AddMinutes(Optimizer.BusinessUnits[t].BufferSlotDurationInMinutes) <= nextUnavailabilityStartTime)
            //||
            /*2. if this is a business unit slot, another alternative condition exists (I forgot what it is) */
            //Optimizer.MasterBusinessUnits[Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].BusinessUnitID: Optimizer.BusinessUnits[t].BusinessUnitID] /*disabled*/
            ).ToDictionary(t => t, t => (Optimizer.Tasks.ContainsKey(t) ? Optimizer.Tasks[t].ArrivalWindowEndTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowEndTime).Max()));

            if (LatestStartTimes.Any())
            {

                //1.priority, 2. earliest arrival window start time, 3. earliest arrival window end time
                LatestStartTimes = LatestStartTimes.OrderByDescending(t => (Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].Priority : 0)).
                    ThenBy(t => ((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) <= currentTime) ? 0 : Math.Abs(((Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].ArrivalWindowStartTime : Optimizer.Tasks.Select(t => t.Value.ArrivalWindowStartTime).Max()) - currentTime).TotalMinutes)).
                    ThenBy(t => (t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)).ToDictionary(t => t.Key, t => t.Value);
                closestTask = LatestStartTimes.ElementAt(0).Key;

                //closestTask = LatestStartTimes.Where(t => ((t.Value <= currentTime) ? 0 : Math.Abs((t.Value - currentTime).TotalMinutes)) == closestLatestStartTime).Select(t => t.Key).ElementAt(0);

            }


            if (closestTask == "" && reasonOfNextUnavailability != "unav")
            {
                closestTask = reasonOfNextUnavailability; //fixedTask
                if (OrderedTasksBUsAndVisitTimes.Any()) // check feasibility of assignment of the fixed task regarding the previousTask
                {
                    var previousTask = OrderedTasksBUsAndVisitTimes.LastOrDefault().Key;
                    if (/*1. if previousTask is a task, then it should be visitable within closestTask's time window */ Optimizer.Tasks.ContainsKey(previousTask) &&
                        (currentTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) >= Optimizer.Tasks[closestTask].ArrivalWindowEndTime)
                        &&
                        /*2 if previousTask is a task, then it should be within the dispatch distance and time */
                        Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchTimeInMins &&
                        Optimizer.TravelingCostFromTo(currentLocation, Optimizer.Tasks[closestTask].Location) > Optimizer.DispatchDistance)
                    { //if any one of above rules is not satisfied then remove the previousTask fron the schedule
                        OrderedTasksBUsAndVisitTimes.Remove(previousTask);
                        currentTime = OrderedTasksBUsAndDispatchTimes[previousTask];
                        OrderedTasksBUsAndDispatchTimes.Remove(previousTask);
                        continue;
                    }
                }
            }
            if (closestTask != "")
            {
                var potentialLunchBreakTime = currentTime;
                currentTime = (reasonOfNextUnavailability != closestTask /* not a fixedTask*/) ?
                    currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(closestTask) ? (firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) : 0) :
                    Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault(); // task start time
                if (Optimizer.Tasks.ContainsKey(closestTask) && currentTime < Optimizer.Tasks[closestTask].ArrivalWindowStartTime)
                    currentTime = Optimizer.Tasks[closestTask].ArrivalWindowStartTime;
                if (Optimizer.Tasks.ContainsKey(closestTask) && !ListOfFixedTasks.ContainsKey(closestTask) /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                    currentTime = Optimizer.BeginningOfPlanningHorizon;
                OrderedTasksBUsAndVisitTimes.Add(closestTask, currentTime);
                ListOfTasks.Remove(closestTask);
                if (ListOfFixedTasks.ContainsKey(closestTask))
                    ListOfFixedTasks.Remove(closestTask);
                currentLocation = Optimizer.Tasks.ContainsKey(closestTask) ? Optimizer.Tasks[closestTask].Location : currentLocation;
                var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(closestTask) ?
                    (Optimizer.Tasks[closestTask].FixedTechnicians.Any() ? Optimizer.Tasks[closestTask].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[closestTask][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[closestTask].BufferSlotDurationInMinutes);
                currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                var latestEndTimeOfScheduledTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Value.AddMinutes((Optimizer.Tasks.ContainsKey(t.Key) ?
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? Optimizer.Tasks[t.Key].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].TotalMinutes)
                     : Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes))).Max();
                if (currentTime < latestEndTimeOfScheduledTasks)
                    currentTime = latestEndTimeOfScheduledTasks;
                if (currentTime >= Optimizer.LunchBreakAfter && !lunchBreakScheduled)
                {
                    if (!firstTask &&
                        ((potentialLunchBreakTime >= Optimizer.LunchBreakAfter && currentTime >= Optimizer.LunchBreakBefore &&
                    potentialLunchBreakTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= Optimizer.Tasks[closestTask].ArrivalWindowEndTime) ||
                    (potentialLunchBreakTime < Optimizer.LunchBreakAfter && OrderedTasksBUsAndVisitTimes[closestTask] > Optimizer.LunchBreakAfter &&
                    Optimizer.LunchBreakAfter.AddMinutes(Optimizer.LunchBreakDurationInMins) <= OrderedTasksBUsAndVisitTimes[closestTask])
                    ))
                    {
                        LunchBreakStartTime = (Optimizer.LunchBreakAfter > potentialLunchBreakTime) ? Optimizer.LunchBreakAfter : potentialLunchBreakTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation && OrderedTasksBUsAndVisitTimes.Any())
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;

                        if (Optimizer.Tasks.ContainsKey(closestTask) && potentialLunchBreakTime >= Optimizer.LunchBreakAfter &&
                            potentialLunchBreakTime.AddMinutes(Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[closestTask].Location)) == OrderedTasksBUsAndVisitTimes[closestTask] &&
                            !Optimizer.Tasks[closestTask].FixedTechnicians.Any())
                        {
                            foreach (var tasksToBePosponed in OrderedTasksBUsAndVisitTimes.Where(t => t.Value == potentialLunchBreakTime && !Optimizer.Tasks[t.Key].FixedTechnicians.Any()).Select(t => t.Key))
                                OrderedTasksBUsAndVisitTimes[tasksToBePosponed] = OrderedTasksBUsAndVisitTimes[tasksToBePosponed].AddMinutes(Optimizer.LunchBreakDurationInMins);
                        }
                    }
                    else
                    {
                        LunchBreakStartTime = currentTime;
                        currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                        lunchBreakScheduled = true;

                        if (currentLocation != startingLocation)
                            LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                    }
                }
                if (firstTask) firstTask = false;
                if (currentTime < Optimizer.Technicians[TechnicianKey].StartTime)//when there is a fix task before the start time of the technician
                    currentTime = Optimizer.Technicians[TechnicianKey].StartTime;
            }
            else
            {
                if (currentTime >= Optimizer.LunchBreakAfter &&
                    currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins) <= nextUnavailabilityStartTime && 
                    !lunchBreakScheduled)
                {
                    LunchBreakStartTime = currentTime;
                    currentTime = currentTime.AddMinutes(Optimizer.LunchBreakDurationInMins);
                    lunchBreakScheduled = true;

                    if (OrderedTasksBUsAndVisitTimes.Any() && currentLocation != startingLocation)
                        LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key;
                }
                if (currentTime < nextUnavailabilityEndTime)//to handle instances with unavailabilities having the same start and end time
                    currentTime = nextUnavailabilityEndTime;
                currentLocation = nextUnavailabilityEndingLocation == "last_job" ? currentLocation : (nextUnavailabilityEndingLocation == "home" ? Optimizer.Technicians[TechnicianKey].Location : Optimizer.Office);
            }
            if (!ListOfTasks.Any() && !ListOfFixedTasks.Any())
                break;
        }

        while (ListOfFixedTasks.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            string closestTask = ListOfFixedTasks.OrderBy(t => t.Value.FixedStartTime).Select(t => t.Key).ElementAt(0);
            OrderedTasksBUsAndVisitTimes.Add(closestTask, Optimizer.Tasks[closestTask].FixedStartTime.GetValueOrDefault() /* task start time*/);
            ListOfTasks.Remove(closestTask);
            if (ListOfFixedTasks.ContainsKey(closestTask))
                ListOfFixedTasks.Remove(closestTask);
        }
        if (!lunchBreakScheduled)
        {
            if (currentTime <= Optimizer.LunchBreakAfter && currentLocation != startingLocation)
            {
                LunchBreakStartTime = Optimizer.LunchBreakAfter;
                lunchBreakScheduled = true;
                LastTaskBeforeLunchBreak = OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).Any() ? OrderedTasksBUsAndVisitTimes.Where(t => t.Value <= LunchBreakStartTime).MaxBy(t => t.Value).Key : "-1";
            }
        }
    }

    public void Reschedule(CancellationToken cancellationToken)
    {
        bool toBeProcessed = true;
        int reschedulingIndex = 0;
        var currentLocation = Optimizer.Technicians[TechnicianKey].Location;
        var currentTime = Optimizer.BeginningOfPlanningHorizon < Optimizer.Technicians[TechnicianKey].StartTime ? Optimizer.Technicians[TechnicianKey].StartTime : Optimizer.BeginningOfPlanningHorizon;
        bool firstTask = true;
        while (toBeProcessed)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            toBeProcessed = false;
            List<Occupancy> ListOfOccupancies = OrderedTasksBUsAndVisitTimes.Select(t => new Occupancy(Optimizer.Tasks.ContainsKey(t.Key) ? OccupancyType.Task : OccupancyType.BU, t.Key, t.Value,
                Optimizer.Tasks.ContainsKey(t.Key) && Optimizer.Tasks[t.Key].FixedTechnicians.Any(), Optimizer.Tasks.ContainsKey(t.Key) ? Optimizer.Tasks[t.Key].Location : "")).ToList();

            ListOfOccupancies = ListOfOccupancies.Union(Optimizer.Technicians[TechnicianKey].UnavailableTimePeriods.Select(u => new Occupancy(OccupancyType.Unavailability,
                "Unavailability", u.startTime, u.endTime, true, u.endingLocation))).ToList();

            ListOfOccupancies = ListOfOccupancies.OrderBy(u => u.StartTime).ThenBy(u => u.EndTime).ToList();

            #region calculate slacks
            currentLocation = (reschedulingIndex > 0) ?
                                ListOfOccupancies[reschedulingIndex - 1].EndingLocation :
                                currentLocation;
            
            
            for (int i = reschedulingIndex; i < ListOfOccupancies.Count; i++) {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (ListOfOccupancies[i].Type == OccupancyType.Unavailability)
                {
                    currentTime = ListOfOccupancies[i].EndTime;
                }
                else
                { // Task or BU
                    if (i >= reschedulingIndex)
                    {
                        currentTime = (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) && Optimizer.Tasks[ListOfOccupancies[i].ID].FixedTechnicians.Any() /* fixedTask*/) ?
                            Optimizer.Tasks[ListOfOccupancies[i].ID].FixedStartTime.GetValueOrDefault() /* fixed task start time */ :
                            currentTime.AddMinutes(Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ? (firstTask?0:Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[ListOfOccupancies[i].ID].Location)) : 0);
                        if (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) && currentTime < Optimizer.Tasks[ListOfOccupancies[i].ID].ArrivalWindowStartTime)
                            currentTime = Optimizer.Tasks[ListOfOccupancies[i].ID].ArrivalWindowStartTime;
                        if (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) && !Optimizer.Tasks[ListOfOccupancies[i].ID].FixedTechnicians.Any() /*new task*/ && currentTime < Optimizer.BeginningOfPlanningHorizon)
                            currentTime = Optimizer.BeginningOfPlanningHorizon;
                        OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID] = currentTime;
                        ListOfOccupancies[i].StartTime = currentTime;
                        ListOfOccupancies[i].PreSlack = new TimeSpan(0); //tasks are always scheduled as early as possible
                        if (ListOfOccupancies[i].Type == OccupancyType.BU)
                            ListOfOccupancies[i].EndingLocation = currentLocation;
                        if (i - 1 >= reschedulingIndex && ListOfOccupancies[i - 1].Type != OccupancyType.Unavailability && (!Optimizer.Tasks.ContainsKey(ListOfOccupancies[i - 1].ID) /*BU or*/ || !Optimizer.Tasks[ListOfOccupancies[i - 1].ID].FixedTechnicians.Any() /* not a fixed task*/)) // post slack calculation is feasible
                        {
                            var travelingTimeFromPrevToCurrent = firstTask ? 0 : Optimizer.DrivingTimeInMinsFromTo(currentLocation,
                                Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ? Optimizer.Tasks[ListOfOccupancies[i].ID].Location : currentLocation);
                            var processingTimeOfPrev = (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i - 1].ID) ?
                            (Optimizer.Tasks[ListOfOccupancies[i - 1].ID].FixedTechnicians.Any() ? Optimizer.Tasks[ListOfOccupancies[i - 1].ID].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[ListOfOccupancies[i - 1].ID][TechnicianKey].TotalMinutes)
                             : Optimizer.BusinessUnits[ListOfOccupancies[i - 1].ID].BufferSlotDurationInMinutes);
                            var latestArrivalTime = ListOfOccupancies[i].StartTime.AddMinutes(-1*(travelingTimeFromPrevToCurrent + processingTimeOfPrev));
                            if (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i - 1].ID) && latestArrivalTime > Optimizer.Tasks[ListOfOccupancies[i - 1].ID].ArrivalWindowEndTime)
                                latestArrivalTime = Optimizer.Tasks[ListOfOccupancies[i - 1].ID].ArrivalWindowEndTime;
                            ListOfOccupancies[i - 1].PostSlack = latestArrivalTime - OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i - 1].ID];
                        }
                        currentLocation = Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ? Optimizer.Tasks[ListOfOccupancies[i].ID].Location : currentLocation;
                        var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ?
                            (Optimizer.Tasks[ListOfOccupancies[i].ID].FixedTechnicians.Any() ? Optimizer.Tasks[ListOfOccupancies[i].ID].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[ListOfOccupancies[i].ID][TechnicianKey].TotalMinutes)
                             : Optimizer.BusinessUnits[ListOfOccupancies[i].ID].BufferSlotDurationInMinutes);
                        currentTime = currentTime.AddMinutes(ProcessingTimeSpan);
                        if (firstTask) firstTask = false;
                        ListOfOccupancies[i].EndTime = currentTime;
                    }
                }
            }
            #endregion

            for (int i = reschedulingIndex; i < ListOfOccupancies.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (ListOfOccupancies[i].Type == OccupancyType.Unavailability)
                    continue;

                if (ListOfOccupancies[i].PreSlack == new TimeSpan(0) && ListOfOccupancies[i].PostSlack == new TimeSpan(0))
                    continue;

                if (i < (ListOfOccupancies.Count - 1) && (ListOfOccupancies[i + 1].isFixed || ListOfOccupancies[i + 1].PreSlack == new TimeSpan(0))) //  (task or BU) - (occupancy with fixedST) (hemen sonras�nda kayd�ramad���n bir i� var )
                {
                    if ((i == 0 || ListOfOccupancies[i - 1].Type == OccupancyType.Unavailability) && ListOfOccupancies[i].PostSlack > new TimeSpan(0)) // hemen sonras�nda kayd�ramad���n bir i� var bunu �teleyebiliyorsan �tele!
                    {
                        OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID] = OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID].Add(ListOfOccupancies[i].PostSlack);
                        toBeProcessed = true;
                        reschedulingIndex = i+1;
                        var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ?
                            (Optimizer.Tasks[ListOfOccupancies[i].ID].FixedTechnicians.Any() ? Optimizer.Tasks[ListOfOccupancies[i].ID].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[ListOfOccupancies[i].ID][TechnicianKey].TotalMinutes)
                             : Optimizer.BusinessUnits[ListOfOccupancies[i].ID].BufferSlotDurationInMinutes);
                        currentTime = OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID].AddMinutes(ProcessingTimeSpan);
                        break;
                    }
                    else
                    {
                        // do nothing : hemen sonras�nda kayd�ramad���n bir i� var bunu �ne alabiliyorsan bile alma!
                    }
                }
                else // (task or BU) - (occupancy with preSlack) (hemen sonras�nda �ne alabildi�in bir i� var ya da hi� i� yok)
                {
                    if (ListOfOccupancies[i].PreSlack > new TimeSpan(0)) //(hemen sonras�nda �ne alabildi�in bir i� var ya da hi� i� yok, bunu �ne alabiliyorsan al)
                    {
                        OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID] = OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID].Add(-1 * ListOfOccupancies[i].PreSlack);
                        toBeProcessed = true;
                        reschedulingIndex = i+1;
                        var ProcessingTimeSpan = (Optimizer.Tasks.ContainsKey(ListOfOccupancies[i].ID) ?
                            (Optimizer.Tasks[ListOfOccupancies[i].ID].FixedTechnicians.Any() ? Optimizer.Tasks[ListOfOccupancies[i].ID].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[ListOfOccupancies[i].ID][TechnicianKey].TotalMinutes)
                             : Optimizer.BusinessUnits[ListOfOccupancies[i].ID].BufferSlotDurationInMinutes);
                        currentTime = OrderedTasksBUsAndVisitTimes[ListOfOccupancies[i].ID].AddMinutes(ProcessingTimeSpan);
                        break;
                    }
                    else
                    {
                        // do nothing : hemen sonras�nda �ne alabildi�in bir i� var bunu �teleyebiliyorsan bile �teleme!
                    }
                }
            }
        }
    }

    public bool Remove(String taskKey, CancellationToken cancellationToken)
    {
        List<String> ListOfTasks = OrderedTasksBUsAndVisitTimes.Select(t => t.Key).ToList();
        bool result = ListOfTasks.Remove(taskKey);
        Reschedule(ListOfTasks, cancellationToken);
        return result;
    }

    public void PrintToFile(StreamWriter srOutput)
    {
        srOutput.Write(TechnicianKey);
        srOutput.Write("\t" + Optimizer.Technicians[TechnicianKey].StartTime);
        srOutput.Write("\t" + Optimizer.Technicians[TechnicianKey].EndTime);
        srOutput.Write("\t" + Optimizer.Technicians[TechnicianKey].Location);
        srOutput.Write("\t" + Optimizer.Technicians[TechnicianKey].Zone);
        srOutput.Write("\t" + Revenue);
        srOutput.Write("\t" + TravellingCost);
        srOutput.Write("\t" + Workload);
        srOutput.Write("\t" + (LunchBreakStartTime != new DateTimeOffset() ? LunchBreakStartTime : "-"));
        srOutput.Write("\t" + (LunchBreakStartTime != new DateTimeOffset() ? LunchBreakStartTime.AddMinutes(Optimizer.LunchBreakDurationInMins) : "-"));
        var currentLocation = Optimizer.Technicians[TechnicianKey].Location;
        bool firstTask = true;
        foreach (var t in OrderedTasksBUsAndVisitTimes)
        {
            if (Optimizer.Tasks.ContainsKey(t.Key))
            {
                srOutput.Write("\t(TRAVEL:" + (firstTask?0:(int)Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location)) / 60 + ":" + (firstTask?0:(int)Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location)) % 60 + ")");
                srOutput.Write("\t(TASK:" + t.Key + "_VT_" + t.Value.Hour + ":" + t.Value.Minute + "_Dur_" +
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? (Math.Floor(Optimizer.Tasks[t.Key].FixedDurationInMins / 60) + ":" + Optimizer.Tasks[t.Key].FixedDurationInMins % 60) : (Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].Hours + ":" + Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].Minutes)) + ")");
                currentLocation = Optimizer.Tasks[t.Key].Location;
                firstTask = false;
            }
            else
            {
                srOutput.Write("\t(NO TRAVEL");

                srOutput.Write("\t(BUSINESS UNIT:" + t.Key + "_VT_" + t.Value.Hour + ":" + t.Value.Minute + "_Dur_" + (Math.Floor(Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes / 60)) + ":" + Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes % 60 + ")");
                //currentLocation = currentLocation;
            }
        }
        srOutput.Write("\n");

    }
    public void PrintToConsole()
    {
        Console.Write(TechnicianKey);
        Console.Write("\t" + Optimizer.Technicians[TechnicianKey].StartTime);
        Console.Write("\t" + Optimizer.Technicians[TechnicianKey].EndTime);
        Console.Write("\t" + Optimizer.Technicians[TechnicianKey].Location);
        Console.Write("\t" + Optimizer.Technicians[TechnicianKey].Zone);
        Console.Write("\t" + Revenue);
        Console.Write("\t" + TravellingCost);
        Console.Write("\t" + Workload);
        Console.Write("\t" + (LunchBreakStartTime != new DateTimeOffset() ? LunchBreakStartTime : "-"));
        Console.Write("\t" + (LunchBreakStartTime != new DateTimeOffset() ? LunchBreakStartTime.AddMinutes(Optimizer.LunchBreakDurationInMins) : "-"));
        var currentLocation = Optimizer.Technicians[TechnicianKey].Location;
        bool firstTask = true;
        foreach (var t in OrderedTasksBUsAndVisitTimes)
        {
            if (Optimizer.Tasks.ContainsKey(t.Key))
            {
                Console.Write("\t(TRAVEL:" + (firstTask ? 0 : (int)Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location)) / 60 + ":" + (firstTask ? 0 : (int)Optimizer.DrivingTimeInMinsFromTo(currentLocation, Optimizer.Tasks[t.Key].Location)) % 60 + ")");
                Console.Write("\t(TASK:" + t.Key + "_VT_" + t.Value.Hour + ":" + t.Value.Minute + "_Dur_" +
                    (Optimizer.Tasks[t.Key].FixedTechnicians.Any() ? (Math.Floor(Optimizer.Tasks[t.Key].FixedDurationInMins / 60) + ":" + Optimizer.Tasks[t.Key].FixedDurationInMins % 60) : (Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].Hours + ":" + Optimizer.TaskDurationsPerTaskPerTechnician[t.Key][TechnicianKey].Minutes)) + ")");
                currentLocation = Optimizer.Tasks[t.Key].Location;
                firstTask = false;
            }
            else
            {
                Console.Write("\t(NO TRAVEL");

                Console.Write("\t(BUSINESS UNIT:" + t.Key + "_VT_" + t.Value.Hour + ":" + t.Value.Minute + "_Dur_" + (Math.Floor(Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes / 60)) + ":" + Optimizer.BusinessUnits[t.Key].BufferSlotDurationInMinutes % 60 + ")");
                //currentLocation = currentLocation;
            }
        }
        Console.Write("\n");

    }
}