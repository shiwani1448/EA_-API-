namespace Jarvis5.Common.EaFms;

public static class TravelBookingRules
{
    public const string Requested = "Requested", InProgress = "InProgress", Booked = "Booked",
        Cancelled = "Cancelled", NotRequired = "NotRequired";
    public static bool IsType(string? value) => value is "Flight" or "Train" or "RoadCar" or "Hotel" or "LocalTransport";
    public static bool IsStatus(string? value) => value is Requested or InProgress or Booked or Cancelled or NotRequired;
    public static bool CanTransition(string current, string next) => current == next || (current, next) switch
    {
        (Requested, InProgress or Cancelled or NotRequired) => true,
        (InProgress, Booked or Cancelled) => true,
        (Booked, Cancelled) => true,
        _ => false
    };
}
