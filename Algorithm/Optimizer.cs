using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Algorithm;

public class Optimizer
{
    public Dictionary<string, Task> Tasks = new();
    public Dictionary<string, Boolean> MasterBusinessUnits = new();
    public Dictionary<string, BusinessUnit> BusinessUnits = new();
    public Dictionary<string, Technician> Technicians = new();
    public Dictionary<string, Zone> Zones = new();
    public Dictionary<string, Dictionary<string, int>> DistanceCosts = new();
    public Dictionary<string, Dictionary<string, int>> TimeCosts = new();

    public Dictionary<string, Dictionary<string, TimeSpan>> TaskDurationsPerTaskPerTechnician = new();
    public Dictionary<string, Dictionary<string, TimeSpan>> TaskDurationsPerTechnicianPerTask = new();

    public Dictionary<string, Dictionary<string, double>> TaskRevenuesPerTaskPerTechnician = new();
    public Dictionary<string, Dictionary<string, double>> TaskRevenuesPerTechnicianPerTask = new();

    public string BasePath {
        get {
            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? throw new ArgumentNullException("Can't find exePath");
            Regex appPathMatcher = new(@"(?<!fil)[A-Za-z]:\\+[\S\s]*?(?=\\+bin)");
            return appPathMatcher.Match(exePath).Value;
        }
    }
    public string Office;

    public DateTimeOffset BeginningOfPlanningHorizon;
    public DateTimeOffset EndOfPlanningHorizon;

    public double UnitLatenessPenalty = 10000;
    public double UnservedTaskPenalty = 100000;

    public TimeSpan MaxLateness = new(3, 0, 0);
    public double EarlinessCost { get; internal set; } // to be included
    public int NumOfParallelThreads =  1;

    //Options:
    public double BestTech1orEquity0 = 1.0; //1:Best Technicians vs. 0: Most Capacity Usage (Equity)
    public double DistanceWeight = 1.0;
    public bool StartDayAtOffice = false;
    public bool DisableDriveTimeInclusion = false;
    public bool CallGrouping = false;
    public bool ZoneBasedDispatchAmongTasksOfATechnician = false; //use service zones
    public bool ZoneBasedDispatchBetweenTechnicianAndAssignedTasks = false; // first_call_zone
    public bool LastJobCloseToHome = true; //new option
    public int DispatchDistance = Int32.MaxValue;
    public int DispatchTimeInMins = Int32.MaxValue;
    public DateTimeOffset LunchBreakAfter = new DateTimeOffset();
    public DateTimeOffset LunchBreakBefore = new DateTimeOffset();
    public int LunchBreakDurationInMins = 0;
    public bool EnableReschedulingLowPriorityAppointments = false;
    public bool MinimizeWeightedCompletionTime = false;
    public int MaxNumOfAppointmentsToBeRescheduled = 0;
    public int MaxNumOfReschedulingPerAppointment = 0;
    public bool AssignPriorityJobsFirst = false;
    public string BoardId;
    public TimeSpan RunTimeLimit;
    public bool IsDebug;
    public bool RespectScheduledTimes;
    public double IdealTotalPrioritizedRevenue = 0;
    public double IdealWorkload = 0;
    public double IdealTravelTime = 0;



    public Optimizer(Params.AlgorithmParams algorithmParams)
    {
        BoardId = algorithmParams.BoardId;
        Tasks = new Dictionary<string, Task>();
        BusinessUnits = new Dictionary<string, BusinessUnit>();
        Technicians = new Dictionary<string, Technician>();
        Zones = new Dictionary<string, Zone>();

        //OPTIONS:
        BestTech1orEquity0 = (1 - algorithmParams.Options.CapacityWeight);
        DistanceWeight = algorithmParams.Options.DistanceWeight;
        DispatchDistance = algorithmParams.Options.DistanceLimitBetweenJobs;
        StartDayAtOffice = algorithmParams.Options.StartDayAtOffice;
        DisableDriveTimeInclusion = algorithmParams.Options.DisableDriveTimeInclusion;
        CallGrouping = algorithmParams.Options.CallGrouping;
        ZoneBasedDispatchAmongTasksOfATechnician = algorithmParams.Options.UseServiceZones;
        ZoneBasedDispatchBetweenTechnicianAndAssignedTasks = algorithmParams.Options.FirstCallZone;
        LunchBreakDurationInMins = algorithmParams.Options.LunchBreak?.DurationInMins ?? 0;
        LastJobCloseToHome = algorithmParams.Options.LastJobCloseToHome;
        Office = algorithmParams.Options.Office.Coordinate;
        EnableReschedulingLowPriorityAppointments = algorithmParams.Options.EnableReschedulingLowPriorityAppointments;
        MinimizeWeightedCompletionTime = algorithmParams.Options.MinimizeWeightedCompletionTime;
        MaxNumOfAppointmentsToBeRescheduled = algorithmParams.Options.MaxNumOfAppointmentsToBeRescheduled ?? 0;
        MaxNumOfReschedulingPerAppointment = algorithmParams.Options.MaxNumOfReschedulingPerAppointment ?? 0;
        AssignPriorityJobsFirst = algorithmParams.Options.AssignPriorityJobsFirst;
        LunchBreakAfter = algorithmParams.Options.LunchBreak?.After ?? algorithmParams.Options.PlanningHorizon.End;
        LunchBreakBefore = algorithmParams.Options.LunchBreak?.Before ?? algorithmParams.Options.PlanningHorizon.End;
        RespectScheduledTimes = algorithmParams.Options.RespectScheduledTimes;

        BeginningOfPlanningHorizon = algorithmParams.Options.PlanningHorizon.Start;
        EndOfPlanningHorizon = algorithmParams.Options.PlanningHorizon.End;

        DistanceCosts = algorithmParams.Matrix.Distance;
        TimeCosts = algorithmParams.Matrix.Duration;
        RunTimeLimit = TimeSpan.FromSeconds(algorithmParams.Options.RunTimeLimit.GetValueOrDefault(420));
        IsDebug =  string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IS_PROD"));
        

        foreach (var task in algorithmParams.Appointments)
        {
            Tasks.Add(task.Id, new Task(this,
                task.Id,
                task.Location.Coordinate,
                task.Location.Zone,
                task.Priority,
                (DateTimeOffset)(task.ArrivalWindow?.Start ??  algorithmParams.Options.PlanningHorizon.Start),
                (DateTimeOffset)(task.ArrivalWindow?.End ?? algorithmParams.Options.PlanningHorizon.End),
                0 /* Must be fixed value or computable maybe priority related increment */,
                task.TechnicianIds,
                (task.TechnicianIds.Count > 0 || RespectScheduledTimes) ? task.Start : null,
                task.TechnicianIds.Count > 0 && task.EligibleTechnicians.Where(t => t.Id == task.TechnicianIds.ElementAt(0)).Any() ? task.EligibleTechnicians.Where(t=>t.Id==task.TechnicianIds.ElementAt(0)).Select(t=>t.Score).ElementAt(0) : 0,
                0 /* Must be fixed value or computable maybe priority related increment*/,
                task.BusinessUnitId,
                task.RescuheduleCount,
                task.OptimizeForScore1orRoute == "score" ? 1:0));

            SetTaskTechnicianRelations(task);
        }
        
        foreach (var businessUnit in algorithmParams.BusinessUnits)
        {
            MasterBusinessUnits.Add(businessUnit.Id, businessUnit.DisregardDistanceZoneLimits.HasValue ? businessUnit.DisregardDistanceZoneLimits.Value : false);
            for (int i = 1; i <= businessUnit.BufferSlotCount; i++)
            {
                var buID = String.Format("{0}_{1}", businessUnit.Id, i);
                BusinessUnits.Add(buID, new BusinessUnit(this,
                businessUnit.Id,
                buID,
                businessUnit.BufferSlotCount,
                businessUnit.BufferSlotLength));

                foreach (var eligibleTechnician in businessUnit.TechnicianIds)
                    BusinessUnits[buID].EligibleTechnicians.Add(eligibleTechnician);
            }

        }

        foreach (var technician in algorithmParams.Technicians)
        {
            string startPoint;
            string? startZone;

            if (technician.Home is null || algorithmParams.Options.StartDayAtOffice)
            {
                startPoint = algorithmParams.Options.Office.Coordinate;
                startZone = algorithmParams.Options.Office.Zone;
            }
            else
            {
                startZone = technician.Home.Zone;
                startPoint = technician.Home.Coordinate;
            }

            Technicians.Add(technician.Id, new Technician(
                technician.Id,
                algorithmParams.Options.UseServiceZones && algorithmParams.Options.FirstCallZone ? startZone : null,
                startPoint,
                technician.WorkTime.Start,
                technician.WorkTime.End
            ));
            Technicians[technician.Id].EligibleTasks = Tasks.Where(t=>t.Value.EligibleTechnicians.Contains(technician.Id)).Select(t=>t.Key).ToList();

            foreach (var unavailability in technician.NonAvailabilities)
            {
                if (unavailability.Start == unavailability.End) continue;
                
                Technicians[technician.Id].UnavailableTimePeriods.Add(new UnAvailableTimePeriod(unavailability.Start, unavailability.End, algorithmParams.Options.StartPointAfterUnavailability));
            }
        }

        foreach (var zone in algorithmParams.Zones)
        {
            Zones.Add(zone.Id, new Zone(zone.Id, zone.CanGoWith));

        }
        
        //StreamWriter srOutput = new(Path.Join(BasePath, "Output.txt"));

        //srOutput.Flush();
        //srOutput.Close();
    }

    public Solution SolveByConstructiveHeuristic(StreamWriter srOutput, CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        Solution s = new(this);

        Dictionary<string, double> sortedTasks = Tasks.OrderBy(o => o.Value.Score).ToDictionary(o => o.Key, o => o.Value.Score);


        foreach (var t in sortedTasks)
        {
            s.GreedyServeTask(t.Key, null, null, cancellationToken);
        }
        stopwatch.Stop();
        //srOutput.WriteLine("CONSTRUCTIVE HEURISTIC'S SOLUTION:");
        //srOutput.WriteLine("Ellapsed Milliseconds:{0}", stopwatch.ElapsedMilliseconds);
        if (IsDebug) {
            Console.WriteLine("CONSTRUCTIVE HEURISTIC'S SOLUTION:");
            Console.WriteLine("Ellapsed Milliseconds:{0}", stopwatch.ElapsedMilliseconds);
        }
        
        //s.PrintToFile(srOutput);
        return s;
    }

    public Solution SolveByConstructiveHeuristic(CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        Solution s = new(this);

        Dictionary<string, Double> sortedTasks = Tasks.OrderBy(o => o.Value.Score).ToDictionary(o => o.Key, o => o.Value.Score);

        Dictionary<string, Double> sortedBusinessUnits = BusinessUnits.OrderBy(o => o.Value.ExpectedRevenue).ToDictionary(o => o.Key, o => o.Value.ExpectedRevenue);

        Dictionary<string, Double> sortedTasksAndBUs = sortedTasks.Union(sortedBusinessUnits).OrderBy(o => o.Value).ToDictionary(o => o.Key, o => o.Value);

        foreach (var t in sortedTasksAndBUs)
        {
            if (Tasks.ContainsKey(t.Key))
            {
                if (!Tasks[t.Key].FixedTechnicians.Any())
                {
                    s.GreedyServeTask(t.Key, null, null, cancellationToken);
                }
                else
                {
                    foreach (var tech in Tasks[t.Key].FixedTechnicians)
                    {
                        s.SchedulePerTechnician[tech].Assign(t.Key, cancellationToken);
                    }
                }
            }
            else if(s.UnservedBUs.Contains(t.Key)) 
                s.GreedyServeBusinessUnit(t.Key, cancellationToken);
        }
        stopwatch.Stop();
        if (IsDebug)
            s.PrintSummaryToConsole();
        return s;
    }

    public Solution SolveByALNS(/*StreamWriter srOutput*/)
    {
        List<string> destroys = new()
        {
            "random",
            "worstTech",
            "worstTask",
            "related",
            "opportunity",
            "equity"
        };
        List<string> repairs = new()
        {
            "greedy",
            "greedyRandomized",
            "random",
            "equity"
        };

        Solution bestFoundSolution = new(this);

        Stopwatch stopwatch = Stopwatch.StartNew();
        
        ConcurrentDictionary<int, Solution> SolutionsOfParallelRuns = new();
        var limitOfParalelTask = IsDebug ?1 : (Environment.ProcessorCount - 1);
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(RunTimeLimit);

        var cancellationToken = cts.Token;

        ParallelOptions parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = limitOfParalelTask
        };
        IdealTravelTime = TimeCosts.SelectMany(t=>t.Value.Values).ToList().Average()*(Tasks.Count+Technicians.Count)/60;

        try {
            Parallel.ForEach(Enumerable.Range(0, limitOfParalelTask), parallelOptions, (index, state) => {
                //while (!cancellationToken.IsCancellationRequested) 
                {
                    if (IsDebug)
                        Console.WriteLine($"{DateTime.Now} | Starting {index} for Constructive");
                    var initialSolution = SolveByConstructiveHeuristic(cancellationToken);
                    var dictTask = Tasks.Where(t => initialSolution.ServedTasks.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value.FixedTechnicians.Any() ? t.Value.FixedDurationInMins : t.Value.TaskDurationInMins);
                    var dictTech = initialSolution.SchedulePerTechnician.ToDictionary(t=>t.Key, t => t.Value.WorkingTime.TotalMinutes);
                    IdealWorkload = (double)(Tasks.Where(t=>initialSolution.ServedTasks.Contains(t.Key)).Select(t => t.Value.FixedTechnicians.Any() ? t.Value.FixedDurationInMins : t.Value.TaskDurationInMins).Sum() / initialSolution.SchedulePerTechnician.Select(t=>t.Value.WorkingTime.TotalMinutes).Sum());

                    if (IsDebug)
                        Console.WriteLine($"{DateTime.Now} | Starting {index} for ALNS");
                    ALNSFramework alns = new(1000, 100, 5, 6, 5, destroys, repairs, 70, 50, 20, 0.6, 0.9, initialSolution.ObjectiveValue, 0.99, 0.01, 0.4, BoardId);
                    Solution alnsSolution = alns.Run(initialSolution/*, srOutput*/, cancellationToken, IsDebug, index);
                    SolutionsOfParallelRuns.TryAdd(index, alnsSolution);
                    if (IsDebug)
                        Console.WriteLine($"{DateTime.Now} | Completed {index} for ALNS");
                }

                state.Stop();
            });
        } catch (OperationCanceledException) {
            Console.WriteLine($"Deadline Exceed for Dispatch::Board#{BoardId} at {DateTime.Now}");
        }
        bestFoundSolution = SolutionsOfParallelRuns.ElementAt(0).Value;
        double BestTotalPrioritizedRevenue = bestFoundSolution.TotalPrioritizedRevenue;
        double BestWorkload = bestFoundSolution.EquityKPI_WorkloadDeviation;
        for (int i = 1; i < SolutionsOfParallelRuns.Count; i++)
            if (bestFoundSolution.isWorseThan(SolutionsOfParallelRuns.ElementAt(i).Value))
            {
                bestFoundSolution = SolutionsOfParallelRuns.ElementAt(i).Value;
                if (BestTotalPrioritizedRevenue > bestFoundSolution.TotalPrioritizedRevenue)
                    BestTotalPrioritizedRevenue = bestFoundSolution.TotalPrioritizedRevenue;
                if (BestWorkload > bestFoundSolution.EquityKPI_WorkloadDeviation)
                    BestWorkload = bestFoundSolution.EquityKPI_WorkloadDeviation;
            }
        //bestFoundSolution = SolutionsOfParallelRuns.GroupBy(s => s.Value.UnservedTasks.Count()).MinBy(g => g.Key).MaxBy(s => s.Value.TotalProfit).Value;
        if (IsDebug) {
            Console.WriteLine("ALNS SOLUTIONS:");
            foreach(var s in SolutionsOfParallelRuns)
                s.Value.PrintBriefSummaryToConsole();
        }
        
        stopwatch.Stop();

        //srOutput.WriteLine("RETURNED SOLUTION:");
        //srOutput.WriteLine("Ellapsed Milliseconds:{0}", stopwatch.ElapsedMilliseconds);
        if (IsDebug) {
            Console.WriteLine("RETURNED SOLUTION:");
            Console.WriteLine("Ellapsed Milliseconds:{0}", stopwatch.ElapsedMilliseconds);
            //bestFoundSolution.PrintToFile(srOutput);
            bestFoundSolution.PrintToConsole();
        }

        return bestFoundSolution;
    }

    public double DrivingTimeInMinsFromTo(string loc1, string loc2, bool reporting=false)
    {
        if (DisableDriveTimeInclusion && !reporting)
            return 0;

        if (TimeCosts.ContainsKey(loc1) && TimeCosts[loc1].ContainsKey(loc2))
            return TimeCosts[loc1][loc2] / 60;

        return 5_256_000; // Double.MaxValue;
    }

    public double TravelingCostFromTo(string loc1, string loc2)
    {
        if (loc1 == loc2)
            return 0;

        if (DistanceCosts.ContainsKey(loc1) && DistanceCosts[loc1].ContainsKey(loc2))
            return (DistanceCosts[loc1][loc2]); // / 1609)*(0.3);

        return double.MaxValue;
    }

    private void SetTaskTechnicianRelations(Params.Appointment task)
    {
        if (!TaskDurationsPerTaskPerTechnician.ContainsKey(task.Id))
            TaskDurationsPerTaskPerTechnician.Add(task.Id, new Dictionary<string, TimeSpan>());

        TimeSpan duration = task.End - task.Start;

        Tasks[task.Id].TaskDurationInMins = duration.TotalMinutes;

        if (task.TechnicianIds.Any())
        {
            Tasks[task.Id].FixedDurationInMins = duration.TotalMinutes;
            Tasks[task.Id].ArrivalWindowEndTime = Tasks[task.Id].ArrivalWindowEndTime;
        }

        if (task.TechnicianIds.Any() || task.EligibleTechnicians == null || task.EligibleTechnicians.Count == 0) 
            return;

        var maxScoreRange = 1.99;
        var minScoreRange = 1.00;
        var maxScore      = task.EligibleTechnicians.Max(t => t.Score);
        var minScore      = task.EligibleTechnicians.Min(t => t.Score);
        var scoreRange    = maxScore - minScore;
        
        foreach (var eligibleTechnician in task.EligibleTechnicians)
        {
            var normalizedScore = scoreRange == 0 ? maxScoreRange : (minScoreRange + (eligibleTechnician.Score - minScore) * (maxScoreRange - minScoreRange) / scoreRange);

            if (!TaskDurationsPerTaskPerTechnician[task.Id].ContainsKey(eligibleTechnician.Id))
                TaskDurationsPerTaskPerTechnician[task.Id].Add(eligibleTechnician.Id, duration);

            if (!TaskDurationsPerTechnicianPerTask.ContainsKey(eligibleTechnician.Id))
                TaskDurationsPerTechnicianPerTask.Add(eligibleTechnician.Id, new Dictionary<string, TimeSpan>());

            if (!TaskDurationsPerTechnicianPerTask[eligibleTechnician.Id].ContainsKey(task.Id))
                TaskDurationsPerTechnicianPerTask[eligibleTechnician.Id].Add(task.Id, duration);

            if (!TaskRevenuesPerTaskPerTechnician.ContainsKey(task.Id))
                TaskRevenuesPerTaskPerTechnician.Add(task.Id, new Dictionary<string, double>());
            if (!TaskRevenuesPerTaskPerTechnician[task.Id].ContainsKey(eligibleTechnician.Id))
                TaskRevenuesPerTaskPerTechnician[task.Id].Add(eligibleTechnician.Id, normalizedScore);
            if (!TaskRevenuesPerTechnicianPerTask.ContainsKey(eligibleTechnician.Id))
                TaskRevenuesPerTechnicianPerTask.Add(eligibleTechnician.Id, new Dictionary<string, double>());
            if (!TaskRevenuesPerTechnicianPerTask[eligibleTechnician.Id].ContainsKey(task.Id))
                TaskRevenuesPerTechnicianPerTask[eligibleTechnician.Id].Add(task.Id, normalizedScore);
        }
        IdealTotalPrioritizedRevenue = Tasks.Where(t => t.Value.FixedTechnicians.Any() == false).Select(t => TaskRevenuesPerTaskPerTechnician.ContainsKey(t.Key) ? t.Value.Priority * TaskRevenuesPerTaskPerTechnician[t.Key].Select(r=>r.Value).Max() : 0).Sum();
    }
}