namespace Algorithm;

using System.Text.Json.Serialization;

public interface Params
{
    public record Appointment(
        [property: JsonPropertyName("location")]
        ParamLocation Location,
        [property: JsonPropertyName("arrival_window")]
        ArrivalWindow? ArrivalWindow,
        [property: JsonPropertyName("business_unit_id")]
        string BusinessUnitId,
        [property: JsonPropertyName("eligible_technicians")]
        IReadOnlyList<EligibleTechnician> EligibleTechnicians,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("start")] DateTimeOffset Start,
        [property: JsonPropertyName("end")] DateTimeOffset End,
        [property: JsonPropertyName("technician_ids")]
        IReadOnlyList<string> TechnicianIds,
        [property: JsonPropertyName("priority")]
        int Priority,
        [property: JsonPropertyName("rescuhedule_count")]
        int RescuheduleCount,
        [property: JsonPropertyName("optimize_for")] string OptimizeForScore1orRoute
    );

    public record ArrivalWindow(
        [property: JsonPropertyName("start")] DateTimeOffset? Start,
        [property: JsonPropertyName("end")] DateTimeOffset? End
    );

    public record EligibleTechnician(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("score")] double Score
    );

    public record Matrix(
        [property: JsonPropertyName("duration")]
        Dictionary<string, Dictionary<string, int>> Duration,
        [property: JsonPropertyName("distance")]
        Dictionary<string, Dictionary<string, int>> Distance
    );

    public record LunchBreak(
        [property: JsonPropertyName("after")] DateTimeOffset After,
        [property: JsonPropertyName("before")] DateTimeOffset Before,
        [property: JsonPropertyName("duration_in_minutes")] int DurationInMins
    );
    public record ParamLocation(
        [property: JsonPropertyName("coordinate")]
        string Coordinate,
        [property: JsonPropertyName("zone")] string? Zone
    );

    public record Options(
        [property: JsonPropertyName("office")] ParamLocation Office,
        [property: JsonPropertyName("distance_limit_between_jobs")]
        int DistanceLimitBetweenJobs,
        [property: JsonPropertyName("start_day_at_office")]
        bool StartDayAtOffice,
        [property: JsonPropertyName("start_point_after_unavailability")]
        string StartPointAfterUnavailability,
        [property: JsonPropertyName("respect_scheduled_times")]
        bool RespectScheduledTimes,
        [property: JsonPropertyName("call_grouping")]
        bool CallGrouping,
        [property: JsonPropertyName("lunch_break")]
        LunchBreak? LunchBreak,
        [property: JsonPropertyName("disable_drive_time_inclusion")]
        bool DisableDriveTimeInclusion,
        [property: JsonPropertyName("use_service_zones")]
        bool UseServiceZones,
        [property: JsonPropertyName("last_job_close_to_home")]
        bool LastJobCloseToHome,
        [property: JsonPropertyName("assign_priority_jobs_first")]
        bool AssignPriorityJobsFirst,
        [property: JsonPropertyName("first_call_zone")]
        bool FirstCallZone,
        [property: JsonPropertyName("scheduling_headstart")]
        int SchedulingHeadstart,
        [property: JsonPropertyName("capacity_weight")]
        double CapacityWeight,
        [property: JsonPropertyName("distance_weight")]
        double DistanceWeight,
        [property: JsonPropertyName("planning_horizon")]
        ParamStartEnd PlanningHorizon,
        [property: JsonPropertyName("enable_buffer_slot")]
        bool EnableBufferSlot,
        [property: JsonPropertyName("can_reschedule_low_priority_appointment")]
        bool EnableReschedulingLowPriorityAppointments,
        [property: JsonPropertyName("minimize_weighted_completion_time")]
        bool MinimizeWeightedCompletionTime,
        [property: JsonPropertyName("how_many_appointments_can_be_rescheduled")]
        int? MaxNumOfAppointmentsToBeRescheduled,
        [property: JsonPropertyName("how_many_times_appointments_can_be_rescheduled")]
        int? MaxNumOfReschedulingPerAppointment,
        [property: JsonPropertyName("run_time_limit")]
        double? RunTimeLimit
    );

    public record ParamStartEnd(
        [property: JsonPropertyName("start")] DateTimeOffset Start,
        [property: JsonPropertyName("end")] DateTimeOffset End
    );

    public record AlgorithmParams(
        [property: JsonPropertyName("board_id")]
        string BoardId,
        [property: JsonPropertyName("appointments")]
        IReadOnlyList<Appointment> Appointments,
        [property: JsonPropertyName("business_units")]
        IReadOnlyList<BusinessUnit> BusinessUnits,
        [property: JsonPropertyName("zones")] IReadOnlyList<Zone> Zones,
        [property: JsonPropertyName("technicians")]
        IReadOnlyList<Technician> Technicians,
        [property: JsonPropertyName("options")]
        Options Options,
        [property: JsonPropertyName("matrix")] Matrix Matrix
    );

    public record Technician(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("home")] ParamLocation? Home,
        [property: JsonPropertyName("work_time")]
        ParamStartEnd? WorkTime,
        [property: JsonPropertyName("non_availabilities")]
        IReadOnlyList<ParamStartEnd> NonAvailabilities
    );

    public record BusinessUnit(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("technician_ids")]
        IReadOnlyList<string> TechnicianIds,
        [property: JsonPropertyName("buffer_slot_count")]
        int BufferSlotCount,
        [property: JsonPropertyName("buffer_slot_length")]
        int BufferSlotLength,
        [property: JsonPropertyName("disregard_distance_and_zone_limits_for_assignment")]
        bool? DisregardDistanceZoneLimits
    );

    public record Zone(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("can_go_with")]
        IReadOnlyList<string> CanGoWith
    );
}