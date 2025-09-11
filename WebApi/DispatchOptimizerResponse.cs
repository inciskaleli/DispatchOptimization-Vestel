namespace WebApi;

using System.Text.Json.Serialization;

public interface DispatchOptimizerResponse
{
    public record ResponseAssignment(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("start")] DateTimeOffset? Start,
        [property: JsonPropertyName("end")] DateTimeOffset? End,
        [property: JsonPropertyName("technician_ids")]
        List<string>? StaffIds,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("route")] ResponseRoute? Route
    );

    public record ResponseRoute(
        [property: JsonPropertyName("distance")]
        double Distance,
        [property: JsonPropertyName("duration")]
        double Duration
    );

    public record ResponseNonAvailability(
        [property: JsonPropertyName("start")] DateTimeOffset? Start,
        [property: JsonPropertyName("finish")] DateTimeOffset? Finish,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string? Title
    );

    public record Response(
        [property: JsonPropertyName("assignments")]
        List<ResponseAssignment> Assignments,
        [property: JsonPropertyName("nonavailibilities")]
        List<ResponseStaffNonAvailability> NonAvailabilities,
        [property: JsonPropertyName("technicians")]
        List<ResponseTechnician> Technicians,
        [property: JsonPropertyName("suggestions")]
        ResponseSuggestion Suggestions
    );

    public record ResponseStaffNonAvailability(
        [property: JsonPropertyName("technician_id")]
        string StaffId,
        [property: JsonPropertyName("non_availabilities")]
        List<ResponseNonAvailability> NonAvailabilities
    );

    public record ResponseLunchBreak(
        [property: JsonPropertyName("start")] DateTimeOffset? Start,
        [property: JsonPropertyName("end")] DateTimeOffset? Finish
    );

    public record ResponseTechnician(
        [property: JsonPropertyName("id")] string StaffId,
        [property: JsonPropertyName("lunch_break")]
        ResponseLunchBreak? LunchBreak
    );

    public record ResponseSuggestion(
        [property: JsonPropertyName("overtime")]
        List<ResponseOvertime> Overtime,
        [property: JsonPropertyName("buffer_slots")]
        List<ResponseBufferSlot> BufferSlots
    );

    public record ResponseOvertime(
        [property: JsonPropertyName("technician_id")]
        string StaffId,
        [property: JsonPropertyName("appointment_ids")]
        List<string> AppointmentIds
    );

    public record ResponseBufferSlot(
        [property: JsonPropertyName("technician_id")]
        string StaffId,
        [property: JsonPropertyName("business_unit_id")]
        string BusinessUnitId,
        [property: JsonPropertyName("start")] DateTimeOffset Start,
        [property: JsonPropertyName("end")] DateTimeOffset End
    );
}