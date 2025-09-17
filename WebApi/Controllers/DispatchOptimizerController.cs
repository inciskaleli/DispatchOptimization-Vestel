using Algorithm;
using Microsoft.AspNetCore.Mvc;
using static Algorithm.Params;

namespace WebApi.Controllers;

[ApiController]
[Route("dispatch_optimizer")]
public class DispatchOptimizerController : ControllerBase
{
    private readonly ILogger<DispatchOptimizerController> _logger;

    public DispatchOptimizerController(ILogger<DispatchOptimizerController> logger)
    {
        _logger = logger;
    }

    [RequestSizeLimit(3_000_000_000)]
    [HttpPost(Name = "dispatch_optimizer")]
    public DispatchOptimizerResponse.Response Post(Params.AlgorithmParams algorithmParams)
    {
        _logger.LogInformation($"Begin the optimizer for Dispatch::Board#{algorithmParams.BoardId} at {DateTime.Now} with {algorithmParams.Options.RunTimeLimit} second run time limit.");
        var optimizer = new Optimizer(algorithmParams);
        var finalSolution = optimizer.SolveByALNS();
        _logger.LogInformation($"Finish to optimizer for Dispatch::Board#{algorithmParams.BoardId} at {DateTime.Now}");
        
        var assignments = new List<DispatchOptimizerResponse.ResponseAssignment>();
        var reportedTasks = new List<String>();
        foreach (var technicianId in finalSolution.SchedulePerTechnician.Keys)
        {
            var technicianSchedule = finalSolution.SchedulePerTechnician[technicianId];
            var cost = technicianSchedule.TravellingCost;
            foreach (var taskId in technicianSchedule.OrderedTasksBUsAndVisitTimes.Keys) {
                if (!reportedTasks.Contains(taskId) && optimizer.Tasks.ContainsKey(taskId))
                {
                    var location = optimizer.Tasks.ContainsKey(taskId) ? optimizer.Tasks[taskId].Location : optimizer.BusinessUnits[taskId].MasterBusinessUnitID;
                    var start = technicianSchedule.OrderedTasksBUsAndVisitTimes[taskId];
                    var finish = start.AddMinutes(optimizer.Tasks.ContainsKey(taskId) ? optimizer.Tasks[taskId].TaskDurationInMins : optimizer.BusinessUnits[taskId].BufferSlotDurationInMinutes);
                    var revenue = optimizer.Tasks.ContainsKey(taskId) ? (optimizer.Tasks[taskId].FixedTechnicians.Any() ? 0 : optimizer.TaskRevenuesPerTaskPerTechnician[taskId][technicianId]) : optimizer.BusinessUnits[taskId].ExpectedRevenue;
                    var technicianIds = optimizer.Tasks[taskId].FixedTechnicians.Any() ? optimizer.Tasks[taskId].FixedTechnicians : new List<string>() { technicianId };
                    var assignment = new DispatchOptimizerResponse.ResponseAssignment(taskId, start, finish, (List<string>?)technicianIds, !optimizer.Tasks[taskId].FixedTechnicians.Any() ? "assigned" : "pre_assigned", new DispatchOptimizerResponse.ResponseRoute(technicianSchedule.CumulativeRouteDistanceUptoTask[taskId], technicianSchedule.CumulativeRouteDurationInMinsUptoTask_forResponse[taskId]));
                    assignments.Add(assignment);
                    reportedTasks.Add(taskId);
                }
            }
        }
        
        foreach(var unservedTask in finalSolution.UnservedTasks)
        {
            var assignment = new DispatchOptimizerResponse.ResponseAssignment(unservedTask,null, null, null, "outlier",null);
            assignments.Add(assignment);
        }

        var staffNonAvailabilities = new List<DispatchOptimizerResponse.ResponseStaffNonAvailability>();
        foreach (var technicianId in optimizer.Technicians.Keys)
        {
            var technician = optimizer.Technicians[technicianId];
            var nonAvailabilities = new List<DispatchOptimizerResponse.ResponseNonAvailability>();

            foreach (var unavailableTimePeriod in technician.UnavailableTimePeriods)
            {
                var nonAvailability = new DispatchOptimizerResponse.ResponseNonAvailability(unavailableTimePeriod.startTime, unavailableTimePeriod.endTime, "unavailability", null);

                nonAvailabilities.Add(nonAvailability);
            }

            staffNonAvailabilities.Add(new DispatchOptimizerResponse.ResponseStaffNonAvailability(technicianId, nonAvailabilities));
        }
        
        var staffSummary = new List<DispatchOptimizerResponse.ResponseTechnician>();
        foreach (var technicianId in optimizer.Technicians.Keys)
        {
            /*
            var technicianSchedule = optimizer.FinalSolution.SchedulePerTechnician[technicianId];
            var location = optimizer.Technicians[technicianId].Location;
            var start = optimizer.Technicians[technicianId].StartTime;
            var finish = optimizer.Technicians[technicianId].EndTime;
            var zone = optimizer.Technicians[technicianId].Zone;
            var revenue = optimizer.FinalSolution.SchedulePerTechnician[technicianId].Revenue;
            var travelingCost = optimizer.FinalSolution.SchedulePerTechnician[technicianId].TravellingCost;
            var workload = optimizer.FinalSolution.SchedulePerTechnician[technicianId].Workload;*/
            var lunchBreakStartTime = finalSolution.SchedulePerTechnician[technicianId].OrderedTasksBUsAndVisitTimes.Any()?
                finalSolution.SchedulePerTechnician[technicianId].LunchBreakStartTime : (algorithmParams.Options.LunchBreak?.After ?? algorithmParams.Options.PlanningHorizon.Start);
            var lunchBreakEndTime = lunchBreakStartTime.AddMinutes(optimizer.LunchBreakDurationInMins);
            var summary = new DispatchOptimizerResponse.ResponseTechnician(technicianId, new DispatchOptimizerResponse.ResponseLunchBreak(lunchBreakStartTime, lunchBreakEndTime));
            staffSummary.Add(summary);
        }
        
        var overtime =new List<DispatchOptimizerResponse.ResponseOvertime>();
        foreach(var technicianId in finalSolution.SuggestionsAsUnservedTaskListPerEligibleTechnician)
        {
            var suggestionOvertime = new DispatchOptimizerResponse.ResponseOvertime(technicianId.Key, technicianId.Value.ToList());
            overtime.Add(suggestionOvertime);
        }
        
        var bufferSlots = new List<DispatchOptimizerResponse.ResponseBufferSlot>();
        foreach (var technicianId in finalSolution.SchedulePerTechnician.Keys)
        {
            var technicianSchedule = finalSolution.SchedulePerTechnician[technicianId];
            var cost = technicianSchedule.TravellingCost;
            foreach (var taskId in technicianSchedule.OrderedTasksBUsAndVisitTimes.Keys)
            {
                if (!optimizer.Tasks.ContainsKey(taskId))
                {
                    var start = technicianSchedule.OrderedTasksBUsAndVisitTimes[taskId];
                    var finish = start.AddMinutes(optimizer.Tasks.ContainsKey(taskId) ? optimizer.Tasks[taskId].TaskDurationInMins : optimizer.BusinessUnits[taskId].BufferSlotDurationInMinutes);
                    var bufferSlot = new DispatchOptimizerResponse.ResponseBufferSlot(technicianId, optimizer.BusinessUnits[taskId].MasterBusinessUnitID, start, finish);
                    bufferSlots.Add(bufferSlot);
                }
            }
        }

        var suggestions = new DispatchOptimizerResponse.ResponseSuggestion(overtime, bufferSlots);
        var response = new DispatchOptimizerResponse.Response(
            assignments,
            staffNonAvailabilities,
            staffSummary,
            suggestions,
            new DispatchOptimizerResponse.ResponseObjective(
                finalSolution.ObjectiveValue,
                finalSolution.TotalProfit,
                finalSolution.TotalWeightedStartTime,
                finalSolution.UnservedTaskCost,
                finalSolution.UnservedBUCost,
                finalSolution.EquityKPI_WorkloadDeviation,
                finalSolution.EquityKPI_NumOfTechsWithMinWorkload,
                finalSolution.TotalCost
            )
        );

        return response;
    }
}