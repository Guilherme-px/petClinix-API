namespace PetClinix.BuildingBlocks.Domain;

public static class ClinicClock
{
    private static readonly string[] TimeZoneIds = ["America/Sao_Paulo", "E. South America"];

    public static DateTime Now()
    {
        foreach (var tzId in TimeZoneIds)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return DateTime.Now;
    }
}