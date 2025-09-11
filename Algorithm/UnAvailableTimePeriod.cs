namespace Algorithm;

public struct UnAvailableTimePeriod
{
    public DateTimeOffset startTime;
    public DateTimeOffset endTime;
    /*
     * Bu kısım dinamik olarak hesaplanmalı.
     * Eğer son konum seçeneği gelirse o zaman son işin konumundan devam etmesi gerekiyor
     */
    public String endingLocation;
    public readonly TimeSpan DurationInTimeSpan
    {
        get
        {
            return endTime - startTime;
        }
    }

    public UnAvailableTimePeriod(DateTimeOffset st, DateTimeOffset et, String eloc)
    {
        startTime = st;
        endTime = et.Subtract(new TimeSpan(0,1,0));
        endingLocation = eloc;
    }
}