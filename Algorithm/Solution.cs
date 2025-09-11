using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Algorithm.Params;

namespace Algorithm;

public class Solution
{
    public Dictionary<String, List<String>> TechnicianPerTask
    {
        get
        {
            return ServedTasks.Any() ? ServedTasks.ToDictionary(s => s, s => SchedulePerTechnician.Where(t => t.Value.ServedTasks.Contains(s)).Select(t => t.Key).ToList()) : new Dictionary<string, List<string>>();
        }
    }
    public Dictionary<String, String> TechnicianPerBU
    {
        get
        {
            return ServedBUs.Any() ? ServedBUs.ToDictionary(s => s, s => SchedulePerTechnician.Where(t => t.Value.ServedBUs.Contains(s)).Select(t => t.Key).ToList().First()) : new Dictionary<string, string>();
        }
    }
    public Dictionary<String, TechnicianSchedule> SchedulePerTechnician;
    readonly Dictionary<String, String> ReasonPerUnservedTask;
    public Optimizer Optimizer;

    public List<String> UnservedMasterBUs
    {
        get
        {
            return Optimizer.MasterBusinessUnits.Where(b => !isMasterBUserved(b.Key)).Select(b => b.Key).ToList();
        }
    }
    public bool isMasterBUserved(string mBUid)
    {
        if (!Optimizer.BusinessUnits.ContainsKey(String.Format("{0}_1", mBUid)))
            return false;
        var requiredSlotCount = Optimizer.BusinessUnits[String.Format("{0}_1", mBUid)].BufferSlotCount;
        var eligibleTechs = Optimizer.BusinessUnits[String.Format("{0}_1", mBUid)].EligibleTechnicians.ToList();
        var slotDuration = Optimizer.BusinessUnits[String.Format("{0}_1", mBUid)].BufferSlotDurationInMinutes;
        var allocatedSlotCount = 0;
        foreach (var t in eligibleTechs)
        {
            allocatedSlotCount += SchedulePerTechnician[t].ServedBUs.Where(b => Optimizer.BusinessUnits[b].BufferSlotDurationInMinutes >= slotDuration).Select(b => b).Count();
        }
        if (allocatedSlotCount >= requiredSlotCount)
            return true;
        return false;
    }
    public List<String> ServedTasks
    {
        get
        {
            return SchedulePerTechnician.SelectMany(t => t.Value.ServedTasks).DistinctBy(t => t).ToList();
        }
    }
    public Dictionary<String, DateTimeOffset> StartTimePerTask
    {
        get
        {
            return Optimizer.Tasks.Where(t => ServedTasks.Contains(t.Key)).ToDictionary(t => t.Key, t => SchedulePerTechnician[TechnicianPerTask[t.Key].First()].OrderedTasksBUsAndVisitTimes[t.Key]);
        }
    }
    public List<String> UnservedTasks
    {
        get
        {
            return Optimizer.Tasks.Where(t => !ServedTasks.Contains(t.Key)).Select(t => t.Key).ToList();
        }
    }
    public Dictionary<String, List<String>> SuggestionsAsUnservedTaskListPerEligibleTechnician
    {
        get
        {
            return Optimizer.Technicians.ToDictionary(t => t.Key,
                t => UnservedTasks.Where(u => Optimizer.Tasks[u].EligibleTechnicians.Contains(t.Key)).Any() ? UnservedTasks.Where(u => Optimizer.Tasks[u].EligibleTechnicians.Contains(t.Key)).ToList() : new List<String>());
        }
    }

    public List<String> ServedBUs
    {
        get
        {
            return SchedulePerTechnician.SelectMany(t => t.Value.ServedBUs).ToList();
        }
    }
    public List<String> UnservedBUs
    {
        get
        {
            return Optimizer.BusinessUnits.Where(t => !ServedBUs.Contains(t.Key) && !isMasterBUserved(t.Value.MasterBusinessUnitID)).Select(t => t.Key).ToList();
        }
    }

    public double EquityKPI_WorkloadDeviation // smaller is better
    {
        get
        {
            return SchedulePerTechnician.Select(t => t.Value.Workload).Max() - SchedulePerTechnician.Select(t => t.Value.Workload).Min();
        }
    }
    public double EquityKPI_NumOfTechsWithMinWorkload // smaller is better
    {
        get
        {
            double min = SchedulePerTechnician.Select(t => t.Value.Workload).Min();
            return SchedulePerTechnician.Where(t => t.Value.Workload==min).Count();
        }
    }
    public double EquityKPI_TotalWorkloadDeviation // smaller is better
    {
        get
        {
            return SchedulePerTechnician.Select(t => (t.Value.Workload-Optimizer.IdealWorkload) >0? (t.Value.Workload - Optimizer.IdealWorkload):(Optimizer.IdealWorkload- t.Value.Workload)).Sum();
        }
    }

    public double EquityKPI_WorkloadDeviation_Normalized // [0, 1] : 0 is better
    {
        get
        {
            return EquityKPI_WorkloadDeviation/ SchedulePerTechnician.Select(t => t.Value.Workload).Max();
        }
    }
    public double EquityKPI_TotalWorkloadDeviation_Normalized // [0, 1] : 0 is better
    {
        get
        {
            return EquityKPI_TotalWorkloadDeviation/(Optimizer.IdealWorkload*Optimizer.Technicians.Count());
        }
    }
    public double TotalPrioritizedRevenue_Normalized // [0, 1] : 1 is better
    {
        get
        {
            return TotalPrioritizedRevenue / Optimizer.IdealTotalPrioritizedRevenue;
        }
    }

    public double WeightedSum_TPR_and_Workload // [0, 1] : 1 is better
    {
        get
        {
            return (TotalPrioritizedRevenue_Normalized*Optimizer.BestTech1orEquity0 - EquityKPI_TotalWorkloadDeviation_Normalized * (1-Optimizer.BestTech1orEquity0));
        }
    }
    public double TotalTravelTime_Normalized
    {
        get
        {
            return SchedulePerTechnician.Select(t => t.Value.TravellingTime).Sum() / Optimizer.IdealTravelTime;
        }
    }

    public double WeightedSum_TPR_and_Distance // [0, 1] : 1 is better
    {
        get
        {
            return (TotalPrioritizedRevenue_Normalized * (1-Optimizer.DistanceWeight) - TotalTravelTime_Normalized * Optimizer.DistanceWeight);
        }
    }
    public double ObjectiveValue
    {
        get
        {
            if (Optimizer.BestTech1orEquity0==0)
                return TotalProfit - (Optimizer.AssignPriorityJobsFirst ? TotalWeightedStartTime*10 : 0) - UnservedTaskCost - UnservedBUCost - EquityKPI_WorkloadDeviation*10000000 - EquityKPI_NumOfTechsWithMinWorkload*10000000;
            else if (!Optimizer.MinimizeWeightedCompletionTime)
                return TotalProfit - UnservedTaskCost - UnservedBUCost;
            else
                return TotalProfit - (Optimizer.AssignPriorityJobsFirst ? TotalWeightedStartTime*10 : 0) - UnservedTaskCost - UnservedBUCost;
        }
    }
    public double TotalProfit
    {
        get
        {
            return Optimizer.Technicians.Select(t => SchedulePerTechnician[t.Key].Revenue *1000 - SchedulePerTechnician[t.Key].Cost).Sum();
        }
    }
    public double TotalPrioritizedRevenue
    {
        get
        {
            return Optimizer.Technicians.Select(t => SchedulePerTechnician[t.Key].PrioritizedRevenue).Sum();
        }
    }
    public double TotalCost
    {
        get
        {
            return Optimizer.Technicians.Select(t => SchedulePerTechnician[t.Key].Cost).Sum();
        }
    }
    public double UnservedTaskCost
    {
        get
        {
            if (!Optimizer.AssignPriorityJobsFirst)
                return UnservedTasks.Select(t => (Optimizer.Tasks[t].ArrivalWindowEndTime.Add(Optimizer.MaxLateness) < Optimizer.BeginningOfPlanningHorizon) ? (((int)Math.Floor(Optimizer.BeginningOfPlanningHorizon.Subtract(Optimizer.Tasks[t].ArrivalTime).TotalDays)) * Optimizer.UnitLatenessPenalty) : Optimizer.UnservedTaskPenalty).Sum();
            else
                return UnservedTasks.Select(t => (Optimizer.Tasks[t].ArrivalWindowEndTime.Add(Optimizer.MaxLateness) < Optimizer.BeginningOfPlanningHorizon) ? (((int)Math.Floor(Optimizer.BeginningOfPlanningHorizon.Subtract(Optimizer.Tasks[t].ArrivalTime).TotalDays)) * Optimizer.UnitLatenessPenalty)* Optimizer.Tasks[t].Priority : Optimizer.UnservedTaskPenalty * Optimizer.Tasks[t].Priority).Sum();

        }
    }
    public double UnservedBUCost
    {
        get
        {
            return UnservedMasterBUs.Select(bu => Optimizer.UnservedTaskPenalty).Sum();
        }
    }

    public double TotalStartTime
    {
        get
        {
            return StartTimePerTask.Select(t => (t.Value - Optimizer.BeginningOfPlanningHorizon).TotalMinutes).Sum();
        }
    }
    public double TotalWeightedStartTime
    {
        get
        {
            return StartTimePerTask.Select(t => Optimizer.Tasks[t.Key].Priority * (t.Value - Optimizer.Tasks[t.Key].ArrivalWindowStartTime).TotalMinutes).Sum();
        }
    }
    public double MaxDistanceOfLastJobToHome
    {
        get
        {
            return 0;
            return SchedulePerTechnician.Where(t => t.Value.OrderedTasksBUsAndVisitTimes.Any()).Select(t => Optimizer.DrivingTimeInMinsFromTo(t.Value.LastTaskLocation, Optimizer.Technicians[t.Key].Location)).Max();
        }
    }
    public int NumOfRescheduledTasks
    {
        get
        {
            return Optimizer.Tasks.Where(t => ServedTasks.Contains(t.Key) && t.Value.FixedTechnicians.Any() && t.Value.ArrivalWindowStartTime != StartTimePerTask[t.Key]).Count();
        }
    }
    public Solution(Optimizer optimizer)
    {
        Optimizer = optimizer;
        SchedulePerTechnician = Optimizer.Technicians.ToDictionary(t => t.Key, t => new TechnicianSchedule(optimizer, this, t.Key));
        ReasonPerUnservedTask = new Dictionary<string, string>();
    }

    public Solution(Solution s)
    {
        Optimizer = s.Optimizer;
        SchedulePerTechnician = s.SchedulePerTechnician.ToDictionary(s => s.Key, s => new TechnicianSchedule(s.Value));
        ReasonPerUnservedTask = s.ReasonPerUnservedTask.ToDictionary(s => s.Key, s => s.Value);
    }
    public bool isWorseThan(Solution newSolution)
    {
        if ((newSolution.UnservedTasks.Any() ? newSolution.UnservedTasks.Select(t => Optimizer.Tasks[t].Priority).Max() : 0) < (UnservedTasks.Any() ? UnservedTasks.Select(t => Optimizer.Tasks[t].Priority).Max() : 0))
            return true; // the largest priority of unserved tasks is smaller in new solution
        if ((newSolution.UnservedBUCost + newSolution.UnservedTaskCost) <= (UnservedBUCost + UnservedTaskCost))//new solution serves at least as many tasks as this one does
        {
            if (Optimizer.BestTech1orEquity0 >=1) //if bestTech important! 
            {
                if (Optimizer.MinimizeWeightedCompletionTime || Optimizer.AssignPriorityJobsFirst)
                {
                    if ((newSolution.ServedTasks.Any() ? newSolution.ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) > (ServedTasks.Any() ? ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) )
                        return true;
                    if ((newSolution.ServedTasks.Any() ? newSolution.ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) == (ServedTasks.Any() ? ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0))
                    {
                        if (Optimizer.AssignPriorityJobsFirst) 
                        { 
                            if (newSolution.WeightedSum_TPR_and_Distance > WeightedSum_TPR_and_Distance) 
                                return true; 
                            if(newSolution.WeightedSum_TPR_and_Distance == WeightedSum_TPR_and_Distance)
                            {
                                if (newSolution.TotalWeightedStartTime < TotalWeightedStartTime)
                                    return true;
                                if (newSolution.TotalWeightedStartTime == TotalWeightedStartTime && newSolution.TotalCost < TotalCost)
                                    return true;
                                if (newSolution.TotalWeightedStartTime == TotalWeightedStartTime && newSolution.TotalCost == TotalCost && Optimizer.LastJobCloseToHome && newSolution.MaxDistanceOfLastJobToHome < MaxDistanceOfLastJobToHome)
                                    return true;

                            }
                        }
                        else // Optimizer.MinimizeWeightedCompletionTime
                        {
                            if (newSolution.TotalWeightedStartTime < TotalWeightedStartTime && newSolution.TotalProfit >= 0.95 * TotalProfit)
                                return true;
                            else if (newSolution.TotalWeightedStartTime == TotalWeightedStartTime)
                            {
                                if (Optimizer.LastJobCloseToHome && newSolution.MaxDistanceOfLastJobToHome < MaxDistanceOfLastJobToHome && newSolution.TotalProfit >= 0.95 * TotalProfit)
                                    return true;
                                if (newSolution.TotalProfit > TotalProfit)
                                    return true;
                            }
                            //else if (newSolution.TotalCost < TotalCost*0.95 && newSolution.TotalWeightedStartTime <= 1.05 * TotalWeightedStartTime)
                              //  return true;
                        }


                        /*else if(newSolution.TotalWeightedStartTime > TotalWeightedStartTime && newSolution.TotalWeightedStartTime < 1.1 * TotalWeightedStartTime)
                        {
                            if (newSolution.TotalProfit >= 1.1 * TotalProfit)
                                return true;
                        }*/
                    }

                    /*else if ((newSolution.ServedTasks.Any() ? newSolution.ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) == (ServedTasks.Any() ? ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0))
                    {
                        if (Optimizer.LastJobCloseToHome && newSolution.MaxDistanceOfLastJobToHome < MaxDistanceOfLastJobToHome)
                            return true;
                    }*/
                }
                else if (newSolution.TotalProfit > TotalProfit)
                    return true;

            }
            else // equity is the concern 
            {
                if ((newSolution.ServedTasks.Any() ? newSolution.ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) > (ServedTasks.Any() ? ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0))
                    return true;
                if ((newSolution.ServedTasks.Any() ? newSolution.ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0) == (ServedTasks.Any() ? ServedTasks.Select(t => Optimizer.Tasks[t].Priority).Sum() : 0))
                {
                    if (Optimizer.AssignPriorityJobsFirst)
                    {
                        if (newSolution.WeightedSum_TPR_and_Workload > WeightedSum_TPR_and_Workload)
                            return true;
                        
                    }
                    else // Optimizer.MinimizeWeightedCompletionTime
                    {
                        if (newSolution.TotalWeightedStartTime < TotalWeightedStartTime && newSolution.TotalProfit >= 0.95 * TotalProfit)
                            return true;
                        else if (newSolution.TotalWeightedStartTime == TotalWeightedStartTime)
                        {
                            if (newSolution.EquityKPI_WorkloadDeviation < EquityKPI_WorkloadDeviation )
                                return true;
                            else if (newSolution.EquityKPI_WorkloadDeviation == EquityKPI_WorkloadDeviation && newSolution.EquityKPI_NumOfTechsWithMinWorkload > EquityKPI_NumOfTechsWithMinWorkload)
                                return true;
                            else if ((newSolution.EquityKPI_WorkloadDeviation + newSolution.EquityKPI_NumOfTechsWithMinWorkload) == (EquityKPI_WorkloadDeviation + EquityKPI_NumOfTechsWithMinWorkload))
                            {
                                if (Optimizer.LastJobCloseToHome && newSolution.MaxDistanceOfLastJobToHome < MaxDistanceOfLastJobToHome && newSolution.TotalProfit >= 0.95 * TotalProfit)
                                    return true;
                                if (newSolution.TotalProfit > TotalProfit)
                                    return true;
                                if (newSolution.TotalProfit == TotalProfit && newSolution.TotalPrioritizedRevenue > TotalPrioritizedRevenue)
                                    return true;
                            }
                        }
                    }


                    /*else if(newSolution.TotalWeightedStartTime > TotalWeightedStartTime && newSolution.TotalWeightedStartTime < 1.1 * TotalWeightedStartTime)
                    {
                        if (newSolution.TotalProfit >= 1.1 * TotalProfit)
                            return true;
                    }*/
                }
                
            }
        }
        /*else if (newSolution.UnservedTasks.Select(t => Optimizer.Tasks[t].Priority).Max() == UnservedTasks.Select(t => Optimizer.Tasks[t].Priority).Max())
            if ((Optimizer.MinimizeWeightedCompletionTime || Optimizer.AssignPriorityJobsFirst) && newSolution.TotalWeightedStartTime < TotalWeightedStartTime)
                return true;*/
        return false;                
    }
    public Solution GreedyRepair(Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var toBeInsertedTasks = UnservedTasks.ToList();
        int noOfTasksToBeConsidered = toBeInsertedTasks.Count;
        int counter = 0;
        while (counter < toBeInsertedTasks.Count)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedTasks.Count);
            var taskToBeAssigned = UnservedTasks.ElementAt(r);

            if (!Optimizer.Tasks[taskToBeAssigned].FixedTechnicians.Any())
            {
                GreedyServeTask(taskToBeAssigned, changingTasks, changingTechs, cancellationToken);
            }
            else
            {
                foreach (var tech in Optimizer.Tasks[taskToBeAssigned].FixedTechnicians)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    SchedulePerTechnician[tech].Assign(taskToBeAssigned, cancellationToken);
                }
            }
            toBeInsertedTasks.Remove(taskToBeAssigned);
            counter++;
        }
        var toBeInsertedBUs = UnservedBUs.ToList();
        counter = 0;
        while (counter++ < toBeInsertedBUs.Count && UnservedBUs.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedBUs.Count);
            var buToBeAssigned = UnservedBUs.ElementAt(r);
            GreedyServeBusinessUnit(buToBeAssigned, cancellationToken);
            toBeInsertedBUs.Remove(buToBeAssigned);
        }
        return this;
    }
    public Solution GreedyRandomizedRepair(Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var toBeInsertedTasks = UnservedTasks.ToList();
        int noOfTasksToBeConsidered = toBeInsertedTasks.Count;
        int counter = 0;
        while (counter++ < toBeInsertedTasks.Count)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedTasks.Count);
            var taskToBeAssigned = UnservedTasks.ElementAt(r);
            if (!Optimizer.Tasks[taskToBeAssigned].FixedTechnicians.Any())
            {
                GreedyRandomizedServeTask(taskToBeAssigned, changingTasks, changingTechs, cancellationToken);
            }
            else
            {
                foreach (var tech in Optimizer.Tasks[taskToBeAssigned].FixedTechnicians)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    SchedulePerTechnician[tech].Assign(taskToBeAssigned, cancellationToken);
                }
            }
            toBeInsertedTasks.Remove(taskToBeAssigned);
        }
        var toBeInsertedBUs = UnservedBUs.ToList();
        counter = 0;
        while (counter++ < toBeInsertedBUs.Count && UnservedBUs.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedBUs.Count);
            var buToBeAssigned = UnservedBUs.ElementAt(r);
            GreedyRandomizedServeBusinessUnit(buToBeAssigned, cancellationToken);
            toBeInsertedBUs.Remove(buToBeAssigned);
        }
        return this;
    }

    public Solution RandomRepair(Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var toBeInsertedTasks = UnservedTasks.ToList();
        int noOfTasksToBeConsidered = toBeInsertedTasks.Count;
        int counter = 0;
        while (counter++ < toBeInsertedTasks.Count)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedTasks.Count);
            var taskToBeAssigned = UnservedTasks.ElementAt(r);
            if (!Optimizer.Tasks[taskToBeAssigned].FixedTechnicians.Any())
            {
                RandomServeTask(taskToBeAssigned, changingTasks, changingTechs, cancellationToken);
            }
            else
            {
                foreach (var tech in Optimizer.Tasks[taskToBeAssigned].FixedTechnicians)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);

                    SchedulePerTechnician[tech].Assign(taskToBeAssigned, cancellationToken);
                }
            }
            toBeInsertedTasks.Remove(taskToBeAssigned);
            
        }
        var toBeInsertedBUs = UnservedBUs.ToList();
        counter = 0;
        while (counter++ < toBeInsertedBUs.Count && UnservedBUs.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedBUs.Count);
            var buToBeAssigned = UnservedBUs.ElementAt(r);
            RamdomServeBusinessUnit(buToBeAssigned, cancellationToken);
            toBeInsertedBUs.Remove(buToBeAssigned);
        }
        return this;
    }

    public Solution EquityRepair(Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var toBeInsertedTasks = UnservedTasks.ToList();
        int noOfTasksToBeConsidered = toBeInsertedTasks.Count;
        int counter = 0;
        while (counter < toBeInsertedTasks.Count)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedTasks.Count);
            var taskToBeAssigned = UnservedTasks.ElementAt(r);
            if (!Optimizer.Tasks[taskToBeAssigned].FixedTechnicians.Any())
            {
                FairServeTask(taskToBeAssigned, changingTasks, changingTechs, cancellationToken);
            }
            else
            {
                foreach (var tech in Optimizer.Tasks[taskToBeAssigned].FixedTechnicians)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    SchedulePerTechnician[tech].Assign(taskToBeAssigned, cancellationToken);
                }
            }
            toBeInsertedTasks.Remove(taskToBeAssigned);
        }
        var toBeInsertedBUs = UnservedBUs.ToList();
        counter = 0;
        while (counter++ < toBeInsertedBUs.Count && UnservedBUs.Any())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(UnservedBUs.Count);
            var buToBeAssigned = UnservedBUs.ElementAt(r);
            FairServeBusinessUnit(buToBeAssigned, cancellationToken);
            toBeInsertedBUs.Remove(buToBeAssigned);
        }
        return this;
    }

    public void GreedyServeTask(String taskKey, List<string>? changingTasks, List<string>? changingTechs, CancellationToken cancellationToken)
    {

        // Tech-Task skill check:
        Dictionary<string, double> EligibleTechnicians = new();
        if (Optimizer.BestTech1orEquity0 >0)//OPTION: CAPACITY WEIGHT: Best Technicians
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => Optimizer.TaskRevenuesPerTaskPerTechnician[taskKey][t] / Optimizer.Tasks[taskKey].MaximumRevenue /*+ ADD Equity + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }
        else//OPTION: CAPACITY WEIGHT:
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => SchedulePerTechnician[t].Workload /*+ Add Revenue + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }

        if (!EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of skill compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of skill compatibility.";
            return;
        }

        // Tech-Task zone check:
        if (Optimizer.MasterBusinessUnits[Optimizer.Tasks[taskKey].BusinessUnitID] == false && Optimizer.ZoneBasedDispatchBetweenTechnicianAndAssignedTasks)
        {
            EligibleTechnicians = EligibleTechnicians.Where(t => (Optimizer.Tasks[taskKey].Zone == "" || Optimizer.Technicians[t.Key].Zone == "" || Optimizer.Tasks[taskKey].Zone == null || Optimizer.Technicians[t.Key].Zone == null || !Optimizer.Zones.ContainsKey(Optimizer.Technicians[t.Key].Zone) || Optimizer.Zones[Optimizer.Technicians[t.Key].Zone].CanVisit(Optimizer.Tasks[taskKey].Zone))).ToDictionary(t => t.Key, t => t.Value);
            if (!EligibleTechnicians.Any())
            {
                if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                    ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of zone compatibility.");
                else
                    ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of zone compatibility.";
                return;
            }
        }

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(taskKey, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0 )
                {
                    var currentProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? SchedulePerTechnician[techKey].Revenue : 0) - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? copyTechSchedule.Revenue : 0) - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload: SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        var selectedTechnician = Optimizer.BestTech1orEquity0 ==0 ? TechScore.OrderBy(t => t.Value).ElementAt(0).Key
            : TechScore.OrderByDescending(t => t.Value).ElementAt(0).Key;
        SchedulePerTechnician[selectedTechnician].Assign(taskKey, cancellationToken);
        if (changingTasks!=null && !changingTasks.Contains(taskKey)) changingTasks.Add(taskKey);
        if (changingTechs != null && !changingTechs.Contains(selectedTechnician)) changingTechs.Add(selectedTechnician);
    }
    public void GreedyServeBusinessUnit(String businessUnitId, CancellationToken cancellationToken)
    {
        if (!Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, " BU: No eligible technician in the input.");
            else
                ReasonPerUnservedTask[businessUnitId] = " BU: No eligible technician in the input.";
            return;
        }
        var EligibleTechnicians = Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.OrderByDescending(s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s)).ToDictionary(s => s, s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s));

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(businessUnitId, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0)
                {
                    var currentProfit = SchedulePerTechnician[techKey].Revenue  - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = copyTechSchedule.Revenue  - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[businessUnitId] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        var selectedTechnician = Optimizer.BestTech1orEquity0 ==0 ? TechScore.OrderBy(t => t.Value).ElementAt(0).Key
            : TechScore.OrderByDescending(t => t.Value).ElementAt(0).Key;
        SchedulePerTechnician[selectedTechnician].Assign(businessUnitId, cancellationToken);
    }
    public void GreedyRandomizedServeTask(String taskKey, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        // Tech-Task skill check:
        Dictionary<string, double> EligibleTechnicians = new();
        if (Optimizer.BestTech1orEquity0>0)//OPTION: CAPACITY WEIGHT: Best Technicians
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => Optimizer.TaskRevenuesPerTaskPerTechnician[taskKey][t] / Optimizer.Tasks[taskKey].MaximumRevenue /*+ ADD Equity + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }
        else//OPTION: CAPACITY WEIGHT:
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => SchedulePerTechnician[t].Workload /*+ Add Revenue + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }

        if (!EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of skill compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of skill compatibility.";
            return;
        }

        // Tech-Task zone check:
        if (Optimizer.MasterBusinessUnits[Optimizer.Tasks[taskKey].BusinessUnitID] == false && Optimizer.ZoneBasedDispatchBetweenTechnicianAndAssignedTasks)
        {
            EligibleTechnicians = EligibleTechnicians.Where(t => (Optimizer.Tasks[taskKey].Zone == "" || Optimizer.Technicians[t.Key].Zone == "" || Optimizer.Tasks[taskKey].Zone == null || Optimizer.Technicians[t.Key].Zone == null || !Optimizer.Zones.ContainsKey(Optimizer.Technicians[t.Key].Zone) || Optimizer.Zones[Optimizer.Technicians[t.Key].Zone].CanVisit(Optimizer.Tasks[taskKey].Zone))).ToDictionary(t => t.Key, t => t.Value);
            if (!EligibleTechnicians.Any())
            {
                if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                    ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of zone compatibility.");
                else
                    ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of zone compatibility.";
                return;
            }
        }


        EligibleTechnicians = EligibleTechnicians.OrderByDescending(s => s.Value).ToDictionary(s => s.Key, s => s.Value);

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(taskKey, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0)
                {
                    var currentProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? SchedulePerTechnician[techKey].Revenue : 0) - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? copyTechSchedule.Revenue : 0) - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        var selectedTechnician = Optimizer.BestTech1orEquity0 > 0 ? RouletteWheelSelection(TechScore, true, cancellationToken) /*best tech - higher profit is better*/ : RouletteWheelSelection(TechScore, false, cancellationToken);
        SchedulePerTechnician[selectedTechnician].Assign(taskKey, cancellationToken);
        if (!changingTasks.Contains(taskKey)) changingTasks.Add(taskKey);
        if (!changingTechs.Contains(selectedTechnician)) changingTechs.Add(selectedTechnician);
    }

    public void GreedyRandomizedServeBusinessUnit(String businessUnitId, CancellationToken cancellationToken)
    {
        if (!Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, " BU: No eligible technician in the input.");
            else
                ReasonPerUnservedTask[businessUnitId] = " BU: No eligible technician in the input.";
            return;
        }
        var EligibleTechnicians = Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.OrderByDescending(s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s)).ToDictionary(s => s, s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s));


        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(businessUnitId, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0)
                {
                    var currentProfit = SchedulePerTechnician[techKey].Revenue - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = copyTechSchedule.Revenue - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[businessUnitId] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        var selectedTechnician = Optimizer.BestTech1orEquity0 > 0 ? RouletteWheelSelection(TechScore, true, cancellationToken) : RouletteWheelSelection(TechScore, false, cancellationToken);
        SchedulePerTechnician[selectedTechnician].Assign(businessUnitId, cancellationToken);
    }

    public void RandomServeTask(String taskKey, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        // Tech-Task skill check:
        Dictionary<string, double> EligibleTechnicians = new();
        if (Optimizer.BestTech1orEquity0 > 0)//OPTION: CAPACITY WEIGHT: Best Technicians
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => Optimizer.TaskRevenuesPerTaskPerTechnician[taskKey][t] / Optimizer.Tasks[taskKey].MaximumRevenue /*+ ADD Equity + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }
        else//OPTION: CAPACITY WEIGHT:
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => SchedulePerTechnician[t].Workload /*+ Add Revenue + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }

        if (!EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of skill compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of skill compatibility.";
            return;
        }

        // Tech-Task zone check:
        if (Optimizer.MasterBusinessUnits[Optimizer.Tasks[taskKey].BusinessUnitID] == false && Optimizer.ZoneBasedDispatchBetweenTechnicianAndAssignedTasks)
        {
            EligibleTechnicians = EligibleTechnicians.Where(t => (Optimizer.Tasks[taskKey].Zone == "" || Optimizer.Technicians[t.Key].Zone == "" || Optimizer.Tasks[taskKey].Zone == null || Optimizer.Technicians[t.Key].Zone == null || !Optimizer.Zones.ContainsKey(Optimizer.Technicians[t.Key].Zone) || Optimizer.Zones[Optimizer.Technicians[t.Key].Zone].CanVisit(Optimizer.Tasks[taskKey].Zone))).ToDictionary(t => t.Key, t => t.Value);
            if (!EligibleTechnicians.Any())
            {
                if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                    ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of zone compatibility.");
                else
                    ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of zone compatibility.";
                return;
            }
        }

        EligibleTechnicians = EligibleTechnicians.OrderByDescending(s => s.Value).ToDictionary(s => s.Key, s => s.Value);

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(taskKey, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0)
                {
                    var currentProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? SchedulePerTechnician[techKey].Revenue : 0) - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = ((Optimizer.Tasks[taskKey].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTechnicianPerTask[techKey][taskKey] < -1) ? copyTechSchedule.Revenue : 0) - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        int seed = unchecked(DateTimeOffset.Now.Millisecond);
        Random rand = new(seed);
        int r = rand.Next(TechScore.Count);
        var selectedTechnician = TechScore.ElementAt(r).Key;
        SchedulePerTechnician[selectedTechnician].Assign(taskKey, cancellationToken);
        if (!changingTasks.Contains(taskKey)) changingTasks.Add(taskKey);
        if (!changingTechs.Contains(selectedTechnician)) changingTechs.Add(selectedTechnician);
    }


    public void RamdomServeBusinessUnit(String businessUnitId, CancellationToken cancellationToken)
    {
        if (!Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, " BU: No eligible technician in the input.");
            else
                ReasonPerUnservedTask[businessUnitId] = " BU: No eligible technician in the input.";
            return;
        }
        var EligibleTechnicians = Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.OrderByDescending(s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s)).ToDictionary(s => s, s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s));

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(businessUnitId, cancellationToken))
            {
                if (Optimizer.BestTech1orEquity0 > 0)
                {
                    var currentProfit = SchedulePerTechnician[techKey].Revenue - SchedulePerTechnician[techKey].Cost;
                    var updatedProfit = copyTechSchedule.Revenue - copyTechSchedule.Cost;
                    TechScore.Add(techKey, updatedProfit - currentProfit);
                }
                else
                {
                    var updatedEquityKPI = (copyTechSchedule.Workload > SchedulePerTechnician.Select(t => t.Value.Workload).Max() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Max()) -
                        (copyTechSchedule.Workload < SchedulePerTechnician.Select(t => t.Value.Workload).Min() ? copyTechSchedule.Workload : SchedulePerTechnician.Select(t => t.Value.Workload).Min());
                    TechScore.Add(techKey, updatedEquityKPI - EquityKPI_WorkloadDeviation);
                }
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[businessUnitId] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        int seed = unchecked(DateTimeOffset.Now.Millisecond);
        Random rand = new(seed);
        int r = rand.Next(TechScore.Count);
        var selectedTechnician = TechScore.ElementAt(r).Key;
        SchedulePerTechnician[selectedTechnician].Assign(businessUnitId, cancellationToken);
    }
    public void FairServeTask(String taskKey, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        // Tech-Task skill check:
        Dictionary<string, double> EligibleTechnicians = new();
        if (Optimizer.BestTech1orEquity0 > 0)//OPTION: CAPACITY WEIGHT: Best Technicians
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => Optimizer.TaskRevenuesPerTaskPerTechnician[taskKey][t] / Optimizer.Tasks[taskKey].MaximumRevenue /*+ ADD Equity + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }
        else//OPTION: CAPACITY WEIGHT:
        {
            EligibleTechnicians = Optimizer.Tasks[taskKey].EligibleTechnicians.ToDictionary(t => t, t => SchedulePerTechnician[t].Workload /*+ Add Revenue + ADD a DISTANCE metric + ADD TW Eligibility (?) + ADD Overqualifiance (?)*/);
        }

        if (!EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of skill compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of skill compatibility.";
            return;
        }

        // Tech-Task zone check:
        if (Optimizer.MasterBusinessUnits[Optimizer.Tasks[taskKey].BusinessUnitID] == false && Optimizer.ZoneBasedDispatchBetweenTechnicianAndAssignedTasks)
        {
            EligibleTechnicians = EligibleTechnicians.Where(t => (Optimizer.Tasks[taskKey].Zone == "" || Optimizer.Technicians[t.Key].Zone == "" || Optimizer.Tasks[taskKey].Zone == null || Optimizer.Technicians[t.Key].Zone == null || !Optimizer.Zones.ContainsKey(Optimizer.Technicians[t.Key].Zone) || Optimizer.Zones[Optimizer.Technicians[t.Key].Zone].CanVisit(Optimizer.Tasks[taskKey].Zone))).ToDictionary(t => t.Key, t => t.Value);
            if (!EligibleTechnicians.Any())
            {
                if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                    ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of zone compatibility.");
                else
                    ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of zone compatibility.";
                return;
            }
        }

        EligibleTechnicians = EligibleTechnicians.OrderByDescending(s => s.Value).ToDictionary(s => s.Key, s => s.Value);

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(taskKey, cancellationToken))
            {
                var currentWorkload = SchedulePerTechnician[techKey].ServedTasks.Count;
                var updatedWorkLoad = copyTechSchedule.ServedTasks.Count;
                TechScore.Add(techKey, currentWorkload==0?(updatedWorkLoad - currentWorkload)*3: (updatedWorkLoad - currentWorkload));
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(taskKey))
                ReasonPerUnservedTask.Add(taskKey, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[taskKey] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        var selectedTechnician = RouletteWheelSelection(TechScore, true, cancellationToken) ;
        SchedulePerTechnician[selectedTechnician].Assign(taskKey, cancellationToken);
        if (!changingTasks.Contains(taskKey)) changingTasks.Add(taskKey);
        if (!changingTechs.Contains(selectedTechnician)) changingTechs.Add(selectedTechnician);
    }
    public void FairServeBusinessUnit(String businessUnitId, CancellationToken cancellationToken)
    {
        if (!Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, " BU: No eligible technician in the input.");
            else
                ReasonPerUnservedTask[businessUnitId] = " BU: No eligible technician in the input.";
            return;
        }
        var EligibleTechnicians = Optimizer.BusinessUnits[businessUnitId].EligibleTechnicians.OrderByDescending(s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s)).ToDictionary(s => s, s => Optimizer.BusinessUnits[businessUnitId].ExpectedRevenuePerEligibleTechnician(s));

        Dictionary<String, Double> TechScore = new();
        for (int t = 0; t < EligibleTechnicians.Count; t++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var techKey = EligibleTechnicians.ElementAt(t).Key;
            var copyTechSchedule = new TechnicianSchedule(SchedulePerTechnician[techKey]);
            if (copyTechSchedule.Assign(businessUnitId, cancellationToken))
            {
                var currentWorkload = SchedulePerTechnician[techKey].Workload;
                var updatedWorkLoad = copyTechSchedule.Workload;
                TechScore.Add(techKey, updatedWorkLoad - currentWorkload);
            }
        }
        if (!TechScore.Any())
        {
            if (!ReasonPerUnservedTask.ContainsKey(businessUnitId))
                ReasonPerUnservedTask.Add(businessUnitId, "No eligible technician in terms of time or zone compatibility.");
            else
                ReasonPerUnservedTask[businessUnitId] = "No eligible technician in terms of time or zone compatibility.";
            return;
        }
        
        var selectedTechnician = RouletteWheelSelection(TechScore, true, cancellationToken);
        SchedulePerTechnician[selectedTechnician].Assign(businessUnitId, cancellationToken);
    }

    public Solution RandomTaskRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        List<string> toBeRemovedTasks = new();
        int counter = 0;
        while (counter < (degreeOfDestruction * numberOfServedTask))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(ServedNonFixedTaks.Count + ServedBUs.Count);
            String randomTask = (r < ServedNonFixedTaks.Count) ? ServedNonFixedTaks.ElementAt(r) : ServedBUs.ElementAt(r - ServedNonFixedTaks.Count);
            counter++;
            foreach (var tech in Optimizer.Technicians.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (SchedulePerTechnician[tech].Remove(randomTask, cancellationToken))
                {
                    if (!ReasonPerUnservedTask.ContainsKey(randomTask))
                        ReasonPerUnservedTask.Add(randomTask, "Removed in RandomTaskRemoval destroy operation of ALNS but couldn't be assigned later.");
                    else
                        ReasonPerUnservedTask[randomTask] = "Removed in RandomTaskRemoval destroy operation of ALNS but couldn't be assigned later.";
                    if (!changingTasks.Contains(randomTask)) changingTasks.Add(randomTask);
                    if (!changingTechs.Contains(tech)) changingTechs.Add(tech);
                }
            }
            
        }
        
        return this;
    }

    public Solution WorstTaskRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        List<string> toBeRemovedTasks = new();
        int counter = 0;
        Dictionary<String, Double> TaskScore = ServedNonFixedTaks.Where(t => !Optimizer.Tasks[t].FixedTechnicians.Any()).ToDictionary(t => t,
            t => Optimizer.TaskRevenuesPerTaskPerTechnician[t][TechnicianPerTask[t].First()]);
        Dictionary<String, Double> BUScore = ServedBUs.ToDictionary(t => t, t => Optimizer.BusinessUnits[t].ExpectedRevenue);
        Dictionary<String, Double> TaskBUScores = TaskScore.Concat(BUScore).ToDictionary(t => t.Key, t => t.Value);
        while (counter < (degreeOfDestruction * numberOfServedTask))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            string r = RouletteWheelSelection(TaskBUScores, true, cancellationToken);//rand.Next(ServedTasks.Count + ServedBUs.Count);
            String randomTask = r;
            counter++;
            foreach (var tech in Optimizer.Technicians.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (SchedulePerTechnician[tech].Remove(randomTask, cancellationToken))
                {
                    if (!ReasonPerUnservedTask.ContainsKey(randomTask))
                        ReasonPerUnservedTask.Add(randomTask, "Removed in RandomTaskRemoval destroy operation of ALNS but couldn't be assigned later.");
                    else
                        ReasonPerUnservedTask[randomTask] = "Removed in RandomTaskRemoval destroy operation of ALNS but couldn't be assigned later.";
                    if (!changingTasks.Contains(randomTask)) changingTasks.Add(randomTask);
                    if (!changingTechs.Contains(tech)) changingTechs.Add(tech);
                }
            }
        }
        
        return this;
    }
    public Solution RelatedTaskRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        List<string> toBeRemovedTasks = new();
        int counter = 0;
        while (counter < (degreeOfDestruction * numberOfServedTask))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            int r = rand.Next(ServedNonFixedTaks.Count + ServedBUs.Count);
            String randomTask = (r < ServedNonFixedTaks.Count) ? ServedNonFixedTaks.ElementAt(r) : ServedBUs.ElementAt(r - ServedNonFixedTaks.Count);
            counter++; 
            foreach (var tech in Optimizer.Technicians.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (SchedulePerTechnician[tech].Remove(randomTask, cancellationToken))
                {
                    if (!ReasonPerUnservedTask.ContainsKey(randomTask))
                        ReasonPerUnservedTask.Add(randomTask, "Removed in RelatedTaskRemoval destroy operation of ALNS but couldn't be assigned later.");
                    else
                        ReasonPerUnservedTask[randomTask] = "Removed in RelatedTaskRemoval destroy operation of ALNS but couldn't be assigned later.";
                    if (!changingTasks.Contains(randomTask)) changingTasks.Add(randomTask);
                    if (!changingTechs.Contains(tech)) changingTechs.Add(tech);
                }
            }
        }
        return this;
    }

    public Solution OpportunityTaskRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        List<string> toBeRemovedTasks = new();
        int counter = 0;
        var eligibleTechsOfUnservedTasks = UnservedTasks.SelectMany(t => Optimizer.Tasks[t].EligibleTechnicians).ToList();
        var nonFixedServedTasksAssignedToEligibleTechsOfUnservedTasks = ServedNonFixedTaks.Where(t =>eligibleTechsOfUnservedTasks.Contains(TechnicianPerTask[t].First())).ToList();
        if (nonFixedServedTasksAssignedToEligibleTechsOfUnservedTasks.Any())
        {
            while (counter < (degreeOfDestruction * numberOfServedTask))
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                int r = rand.Next(nonFixedServedTasksAssignedToEligibleTechsOfUnservedTasks.Count);
                var randomTask = nonFixedServedTasksAssignedToEligibleTechsOfUnservedTasks.ElementAt(r);
                counter++;
                foreach (var tech in Optimizer.Technicians.Keys)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    if (SchedulePerTechnician[tech].Remove(randomTask, cancellationToken))
                    {
                        if (!ReasonPerUnservedTask.ContainsKey(randomTask))
                            ReasonPerUnservedTask.Add(randomTask, "Removed in OpportunityTaskRemoval destroy operation of ALNS but couldn't be assigned later.");
                        else
                            ReasonPerUnservedTask[randomTask] = "Removed in OpportunityTaskRemoval destroy operation of ALNS but couldn't be assigned later.";
                        if (!changingTasks.Contains(randomTask)) changingTasks.Add(randomTask);
                        if (!changingTechs.Contains(tech)) changingTechs.Add(tech);
                    }
                }
            }
        }
        return this;
    }

    public Solution EquityTargetedTaskRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        int counter = 0;
        // double avgNumServedTaskPerTech = SchedulePerTechnician.Select(t => t.Value.ServedTasks.Count).Average();
        var eligibleTasksofIdleTechs = SchedulePerTechnician.Where(t => t.Value.ServedTasks.Count==0 && t.Value.WorkingTime>new TimeSpan() ).SelectMany(t=>Optimizer.Technicians[t.Key].EligibleTasks).Where(t=> ServedNonFixedTaks.Contains(t)).Distinct().ToList();
        if (eligibleTasksofIdleTechs.Any())
        {
            while (counter < (degreeOfDestruction * numberOfServedTask))
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                int r = rand.Next(eligibleTasksofIdleTechs.Count);
                String randomTask = eligibleTasksofIdleTechs.ElementAt(r);
                counter++;
                foreach (var tech in Optimizer.Technicians.Keys)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    if (SchedulePerTechnician[tech].Remove(randomTask, cancellationToken))
                    {
                        if (!ReasonPerUnservedTask.ContainsKey(randomTask))
                            ReasonPerUnservedTask.Add(randomTask, "Removed in EquityTargetedTaskRemoval destroy operation of ALNS but couldn't be assigned later.");
                        else
                            ReasonPerUnservedTask[randomTask] = "Removed in EquityTargetedTaskRemoval destroy operation of ALNS but couldn't be assigned later.";
                        if (!changingTasks.Contains(randomTask)) changingTasks.Add(randomTask);
                        if (!changingTechs.Contains(tech)) changingTechs.Add(tech);
                    }
                }
            }
        }
        return this;
    }

    public Solution WorstTechRemoval(double degreeOfDestruction, Random rand, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        Dictionary<String, Double> TechOccupancy = SchedulePerTechnician.Where(t => t.Value.WorkingTime.TotalMinutes > 0 && t.Value.Workload>0).ToDictionary(t => t.Key, t => t.Value.Workload*100);
        var ServedNonFixedTaks = ServedTasks.Where(t => Optimizer.Tasks.ContainsKey(t) && !Optimizer.Tasks[t].FixedTechnicians.Any()).Select(t => t).ToList();
        int numberOfServedTask = ServedNonFixedTaks.Count;
        int counter = 0;
        if (TechOccupancy.Any())
        {
            while (counter < (degreeOfDestruction * numberOfServedTask))
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                String randomTech = RouletteWheelSelection(TechOccupancy, false, cancellationToken);
                var tasksOfrandomTech = SchedulePerTechnician[randomTech].ServedTasks.Where(t => ServedNonFixedTaks.Contains(t)).ToList();
                counter++;
                foreach (var servedTask in tasksOfrandomTech)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    counter++;
                    if (SchedulePerTechnician[randomTech].Remove(servedTask, cancellationToken))
                    {
                        if (!ReasonPerUnservedTask.ContainsKey(servedTask))
                            ReasonPerUnservedTask.Add(servedTask, "Removed in WorstTechRemoval destroy operation of ALNS but couldn't be assigned later.");
                        else
                            ReasonPerUnservedTask[servedTask] = "Removed in WorstTechRemoval destroy operation of ALNS but couldn't be assigned later.";
                        if (!changingTasks.Contains(servedTask)) changingTasks.Add(servedTask);
                        if (!changingTechs.Contains(randomTech)) changingTechs.Add(randomTech);
                    }
                }

            }
        }
        return this;
    }
    public void PrintBriefSummaryToConsole()
    {
        Console.WriteLine("Solution Brief Summary: ");
        //Console.WriteLine("Objective: " + ObjectiveValue);
        Console.WriteLine("Total Profit: " + TotalProfit);
        Console.WriteLine("Total Prioritized Revenue: " + TotalPrioritizedRevenue);
        Console.WriteLine("Total Cost: " + TotalCost);
        Console.WriteLine("Total Number of Unserved Tasks: " + UnservedTasks.Count());
        Console.WriteLine("Total Unserved BUs: " + UnservedBUs.Count());
        Console.WriteLine("MaxDistanceOfLastJobToHome: " + MaxDistanceOfLastJobToHome);
    }
    public void PrintSummaryToConsole()
    {
        Console.WriteLine("-----------------------------------------------");
        Console.WriteLine("Solution Summary: ");
        //Console.WriteLine("Objective: " + ObjectiveValue);
        Console.WriteLine("Total Profit: " + TotalProfit);
        Console.WriteLine("Total Prioritized Revenue: " + TotalPrioritizedRevenue);
        Console.WriteLine("Total Cost: " + TotalCost);
        Console.WriteLine("Total Unserved Task Penalty: " + UnservedTaskCost);
        Console.WriteLine("Total Unserved BU Penalty: " + UnservedBUCost);
        Console.WriteLine("Total Weighted Start Time: " + TotalWeightedStartTime);
        Console.WriteLine("Unserved Tasks: ");
        foreach (var t1 in UnservedTasks)
        {
            Console.WriteLine("Unserved Tasks: "+t1);
        }
        Console.WriteLine("-----------------------------------------------");
    }
    public void PrintToConsole()
    {
        Console.WriteLine("Total Profit: " + TotalProfit);
        Console.WriteLine("Total Prioritized Revenue: " + TotalPrioritizedRevenue);
        Console.WriteLine("Total Cost: " + TotalCost);
        Console.WriteLine("Total Unserved Task Penalty: " + UnservedTaskCost);
        Console.Write("TechnicianKey \t StartTime \t EndTime \t Location \t Zone \t Revenue \t TravellingCost \t Workload  \t LunchBreakStartTime \t LunchBreakEndTime\t Tasks\n");
        foreach (var ts in SchedulePerTechnician)
        {
            ts.Value.PrintToConsole();
        }

        Console.Write("UnservedTaskKey \t Priority \t ELigible Techs (and Scores) \t Arrival Start Time \t Avg Duration (min) \n \n");
        foreach (var task in UnservedTasks)
        {
            Console.Write(task);
            Console.Write("\t" + Optimizer.Tasks[task].Priority);
            Console.Write("\t");
            Console.Write("\t" + Optimizer.Tasks[task].ArrivalWindowStartTime);
            Console.Write("\t" + Optimizer.Tasks[task].AverageTaskDurationInMinutes);
            foreach (var ts in Optimizer.Tasks[task].EligibleTechnicians)
                Console.Write( " " + ts.Substring(0,4) + "(" +  Optimizer.TaskRevenuesPerTaskPerTechnician[task][ts] + ")");
            Console.Write("\n");
        }

        Console.Write("ServedTaskKey \t Priority \t Fixed? \t Assigned Tech \t VisitTime \t Duration (min)\t Revenue \t OptimizeForScore \n");
        foreach (var ts in SchedulePerTechnician)
        {
            foreach (var task in ts.Value.ServedTasks)
            {
                Console.Write(task); 
                Console.Write("\t" + Optimizer.Tasks[task].Priority);
                Console.Write("\t" + (Optimizer.Tasks[task].FixedTechnicians.Any() ? "Y" : "N"));
                Console.Write("\t" + ts.Key.Substring(0, 4));
                Console.Write("\t" + ts.Value.OrderedTasksBUsAndVisitTimes[task]);
                Console.Write("\t" + (Optimizer.Tasks[task].FixedTechnicians.Any() ?
                    Optimizer.Tasks[task].FixedDurationInMins : Optimizer.TaskDurationsPerTaskPerTechnician[task][ts.Key].TotalMinutes));
                Console.Write("\t" + (Optimizer.Tasks[task].FixedTechnicians.Any() ?
                     Optimizer.Tasks[task].FixedRevenue : 
                    ((Optimizer.Tasks[task].OptimizeForScore1orRoute0 || Optimizer.TaskRevenuesPerTaskPerTechnician[task][ts.Key] < -10) ?  Optimizer.TaskRevenuesPerTaskPerTechnician[task][ts.Key] : 0)) );
                Console.Write("\t" + (Optimizer.Tasks[task].OptimizeForScore1orRoute0 ? "Y" : "N"));
                Console.Write("\n");
            }
        }
        Console.Write("Tech \t Start \t End \t Served Tasks (OptimizeForScore1orRoute0)\n");
        foreach (var ts in SchedulePerTechnician)
        {
            Console.Write("\t" + ts.Key);
            Console.Write("\t" + Optimizer.Technicians[ts.Key].StartTime);
            Console.Write("\t" + Optimizer.Technicians[ts.Key].EndTime);
            foreach (var task in ts.Value.ServedTasks)
            {
                Console.Write("\t" + task);
                Console.Write("(" + Optimizer.Tasks[task].OptimizeForScore1orRoute0);
                Console.Write(")\t");
            }
            Console.Write("\n");
        }
    }

    public void PrintToFile(StreamWriter srOutput)
    {
        srOutput.WriteLine("Total Profit: " + TotalProfit);
        srOutput.WriteLine("Total Unserved Task Penalty: " + UnservedTaskCost);
        srOutput.Write("TechnicianKey \t StartTime \t EndTime \t Location \t Zone \t Revenue \t TravellingCost \t Workload  \t LunchBreakStartTime \t LunchBreakEndTime \t Tasks\n");
        foreach (var ts in SchedulePerTechnician)
        {
            ts.Value.PrintToFile(srOutput);
        }

        srOutput.Write("UnservedTaskKey \t StartTime \t EndTime \t Location \t Zone\n");
        foreach (var ts in UnservedTasks)
        {
            Optimizer.Tasks[ts].PrintToFile(srOutput, ReasonPerUnservedTask.ContainsKey(ts) ? ReasonPerUnservedTask[ts] : "");
        }
    }

    private static string RouletteWheelSelection(Dictionary<string, double> weights, bool higherIsBetter, CancellationToken cancellationToken)
    {
        double weightsMax = weights.Select(w => w.Value).ToList().Max();
        Dictionary<string, double> probabilities = higherIsBetter ?
            weights.ToDictionary(w => w.Key, w => w.Value) :
            weights.ToDictionary(w => w.Key, w => (weightsMax - w.Value));
        double weightsSum = probabilities.Select(w => w.Value).ToList().Sum();
        probabilities = probabilities.ToDictionary(w => w.Key, w => w.Value / weightsSum);

        int seed = unchecked(DateTimeOffset.Now.Millisecond);
        Random rand = new(seed);
        double r = rand.NextDouble()*probabilities.Select(p=>p.Value).Sum();

        double ll = 0;
        foreach (var m in probabilities)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (r <= (ll + m.Value))
                return m.Key;
            ll += m.Value;
        }
        return probabilities.ElementAt(probabilities.Count - 1).Key;
    }
    public Solution Reschedule(CancellationToken cancellationToken)
    {
        Solution neighbor = new(this);
        foreach (var techSched in neighbor.SchedulePerTechnician) {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            techSched.Value.Reschedule(cancellationToken);
        }

        return neighbor;
    }
    public Solution LocalSearch_Swap_FI(CancellationToken cancellationToken, List<string> LS_NoChangeInTask, List<string> LS_NoChangeInTechnician)
    {
        for (int i = 0; i < SchedulePerTechnician.Count; i++)
        {
            var s1 = SchedulePerTechnician.ElementAt(i).Key;
            foreach (var t1 in SchedulePerTechnician.ElementAt(i).Value.OrderedTasksBUsAndVisitTimes.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (Optimizer.Tasks.ContainsKey(t1) && (Optimizer.Tasks[t1].FixedTechnicians.Any() || Optimizer.Tasks[t1].EligibleTechnicians.Count <= 1))
                    continue;
                
                if (Optimizer.BusinessUnits.ContainsKey(t1) && Optimizer.BusinessUnits[t1].EligibleTechnicians.Count <= 1)
                    continue;
                
                for (int j = i + 1; j < SchedulePerTechnician.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    var s2 = SchedulePerTechnician.ElementAt(j).Key;
                    foreach (var t2 in SchedulePerTechnician.ElementAt(j).Value.OrderedTasksBUsAndVisitTimes.Keys)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException(cancellationToken);
                        
                        if (LS_NoChangeInTechnician.Contains(s1) && LS_NoChangeInTechnician.Contains(s2) && LS_NoChangeInTask.Contains(t1) && LS_NoChangeInTask.Contains(t2))
                            continue;

                        if (Optimizer.Tasks.ContainsKey(t2) && (Optimizer.Tasks[t2].FixedTechnicians.Any() || Optimizer.Tasks[t2].EligibleTechnicians.Count <= 1))
                            continue;
                        if (Optimizer.BusinessUnits.ContainsKey(t2) && Optimizer.BusinessUnits[t2].EligibleTechnicians.Count <= 1)
                            continue;
                        if ((Optimizer.Tasks.ContainsKey(t1) && !Optimizer.Tasks[t1].EligibleTechnicians.Contains(s2)) ||
                            (Optimizer.BusinessUnits.ContainsKey(t1) && !Optimizer.BusinessUnits[t1].EligibleTechnicians.Contains(s2)) ||
                            (Optimizer.Tasks.ContainsKey(t2) && !Optimizer.Tasks[t2].EligibleTechnicians.Contains(s1)) ||
                            (Optimizer.BusinessUnits.ContainsKey(t2) && !Optimizer.BusinessUnits[t2].EligibleTechnicians.Contains(s1)))
                            continue;
                        Solution neighbor = new(this);
                        neighbor.SchedulePerTechnician[s1].Remove(t1, cancellationToken);
                        neighbor.SchedulePerTechnician[s2].Remove(t2, cancellationToken);
                        
                        if (!neighbor.SchedulePerTechnician[s1].Assign(t2, cancellationToken))
                            continue;
                        
                        if (!neighbor.SchedulePerTechnician[s2].Assign(t1, cancellationToken))
                            continue;
                        //if(neighbor.TechnicianPerTask.Where(t=>t.Value.Count()>1).Any())
                        //  Console.WriteLine("problem!");
                        if (this.isWorseThan(neighbor))
                        {

                            LS_NoChangeInTask.Remove(t1);
                            LS_NoChangeInTask.Remove(t2);
                            LS_NoChangeInTechnician.Remove(s1);
                            LS_NoChangeInTechnician.Remove(s2);
                            return neighbor;
                        }
                    }
                }
            }
        }
        return this;
    }

    public Solution LocalSearch_Swap_BI(CancellationToken cancellationToken)
    {
        Solution bestLocalNeighbor = new(this);
        for (int i = 0; i < SchedulePerTechnician.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var s1 = SchedulePerTechnician.ElementAt(i).Key;
            foreach (var t1 in SchedulePerTechnician.ElementAt(i).Value.OrderedTasksBUsAndVisitTimes.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (Optimizer.Tasks[t1].FixedTechnicians.Any() || Optimizer.Tasks[t1].EligibleTechnicians.Count <= 1)
                    continue;
                
                for (int j = i + 1; j < SchedulePerTechnician.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    var s2 = SchedulePerTechnician.ElementAt(j).Key;
                    foreach (var t2 in SchedulePerTechnician.ElementAt(j).Value.OrderedTasksBUsAndVisitTimes.Keys)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException(cancellationToken);

                        if (Optimizer.Tasks[t2].FixedTechnicians.Any() || !Optimizer.Technicians[s1].EligibleTasks.Contains(t2))
                            continue;
                        if (!Optimizer.Tasks[t1].EligibleTechnicians.Contains(s2) || !Optimizer.Tasks[t2].EligibleTechnicians.Contains(s1))
                            continue;
                        Solution neighbor = new(this);
                        neighbor.SchedulePerTechnician[s1].Remove(t1, cancellationToken);
                        neighbor.SchedulePerTechnician[s2].Remove(t2, cancellationToken);
                        if (!neighbor.SchedulePerTechnician[s1].Assign(t2, cancellationToken))
                            continue;
                        if (!neighbor.SchedulePerTechnician[s2].Assign(t1, cancellationToken))
                            continue;
                        //if(neighbor.TechnicianPerTask.Where(t=>t.Value.Count()>1).Any())
                        //  Console.WriteLine("problem!");
                        if (this.isWorseThan(neighbor)) 
                            bestLocalNeighbor = new Solution(neighbor);
                    }
                }
            }
        }
        return bestLocalNeighbor;
    }

    public Solution LocalSearch_RemoveNInsert(CancellationToken cancellationToken, List<string> LS_NoChangeInTask, List<string> LS_NoChangeInTechnician)
    {
        Solution bestLocalNeighbor = new(this);
        string bestTask = "";
        string bestTech1 = "";
        string bestTech2 = "";
        for (int i = 0; i < SchedulePerTechnician.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var s1 = SchedulePerTechnician.ElementAt(i).Key;
            foreach (var t1 in SchedulePerTechnician.ElementAt(i).Value.OrderedTasksBUsAndVisitTimes.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (Optimizer.Tasks.ContainsKey(t1) && (Optimizer.Tasks[t1].FixedTechnicians.Any() || Optimizer.Tasks[t1].EligibleTechnicians.Count <= 1))
                    continue;
                if (Optimizer.BusinessUnits.ContainsKey(t1) && Optimizer.BusinessUnits[t1].EligibleTechnicians.Count <= 1)
                    continue; 
                for (int j = 0; j < SchedulePerTechnician.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                      throw new OperationCanceledException(cancellationToken);

                    if (j != i)
                    {
                        var s2 = SchedulePerTechnician.ElementAt(j).Key;
                        if (LS_NoChangeInTechnician.Contains(s1) && LS_NoChangeInTechnician.Contains(s2) && LS_NoChangeInTask.Contains(t1))
                            continue;
                        if ((Optimizer.Tasks.ContainsKey(t1) && !Optimizer.Tasks[t1].EligibleTechnicians.Contains(s2)) ||
                            (Optimizer.BusinessUnits.ContainsKey(t1) && !Optimizer.BusinessUnits[t1].EligibleTechnicians.Contains(s2)))
                            continue;
                        Solution neighbor = new(this);
                        neighbor.SchedulePerTechnician[s1].Remove(t1, cancellationToken);
                        if (!neighbor.SchedulePerTechnician[s2].Assign(t1, cancellationToken))
                            continue;
                        //if (neighbor.TechnicianPerTask.Where(t => t.Value.Count() > 1).Any())
                        //    Console.WriteLine("problem!");
                        if (bestLocalNeighbor.isWorseThan(neighbor))
                        {
                            bestLocalNeighbor = new Solution(neighbor);
                            bestTask = t1;
                            bestTech1 = s1;
                            bestTech2 = s2;
                        }
                    }
                }
            }
        }
        if (bestTask != "")
        {
            LS_NoChangeInTask.Remove(bestTask);
            LS_NoChangeInTechnician.Remove(bestTech1);
            LS_NoChangeInTechnician.Remove(bestTech2 );
        }
        return bestLocalNeighbor;
    }

    public Solution LocalSearch_Insert(CancellationToken cancellationToken, List<string> LS_NoChangeInTask, List<string> LS_NoChangeInTechnician)
    {
        Solution bestLocalNeighbor = new(this);
        string bestTask = "";
        string bestTech1 = "";
        foreach (var t1 in UnservedTasks)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            
            foreach (var s2 in SchedulePerTechnician)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                
                if (LS_NoChangeInTechnician.Contains(s2.Key) && LS_NoChangeInTask.Contains(t1))
                    continue;

                Solution neighbor = new(this);
                if ((Optimizer.Tasks.ContainsKey(t1) && !Optimizer.Tasks[t1].EligibleTechnicians.Contains(s2.Key)) ||
                            (Optimizer.BusinessUnits.ContainsKey(t1) && !Optimizer.BusinessUnits[t1].EligibleTechnicians.Contains(s2.Key)))
                    continue;
                if (!neighbor.SchedulePerTechnician[s2.Key].Assign(t1, cancellationToken))
                    continue;

                if (bestLocalNeighbor.isWorseThan(neighbor))
                {
                    bestLocalNeighbor = new Solution(neighbor);
                    bestTask = t1;
                    bestTech1 = s2.Key;
                }
            }
        }
        if (bestTask != "")
        {
            LS_NoChangeInTask.Remove(bestTask);
            LS_NoChangeInTechnician.Remove(bestTech1);
        }
        return bestLocalNeighbor;
    }
}
