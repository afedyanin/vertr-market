using System.Runtime.CompilerServices;

namespace Market.ApiClient.Extensions;

public static class DateTimeHelper
{
    private const long TicksPerMicrosecond = 10;
    private const long MicrosecondsPerMillisecond = 1000;
    private static readonly long UnixEpochTicks = DateTime.UnixEpoch.Ticks;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime MicrosecondsToDateTime(long timestampUs)
    {
        long ticks = UnixEpochTicks + (timestampUs * TicksPerMicrosecond);

        return new DateTime(ticks, DateTimeKind.Utc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DateTimeToMicroseconds(DateTime dateTime)
    {
        long utcTicks = dateTime.ToUniversalTime().Ticks;
        return (utcTicks - UnixEpochTicks) / TicksPerMicrosecond;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long FloorMicrosecondsByMsInterval(long timestampUs, long intervalMs)
    {
        long intervalUs = intervalMs * MicrosecondsPerMillisecond;

        if (intervalUs <= 1)
        {
            return timestampUs;
        }

        // Для положительных timestamps (после 1970 года)
        if (timestampUs >= 0)
        {
            return (timestampUs / intervalUs) * intervalUs;
        }

        // Для отрицательных timestamps (до 1970 года)
        long remainder = timestampUs % intervalUs;
        return remainder == 0 ? timestampUs : timestampUs - (intervalUs + remainder);
    }
}
