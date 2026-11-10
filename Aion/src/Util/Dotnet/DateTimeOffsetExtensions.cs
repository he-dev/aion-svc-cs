using System;

namespace Aion.Util.Dotnet;

internal static class DateTimeOffsetExtensions
{
    // util: Adds time-zone offset to DateTimeOffset.
    // note: Quartz.net requires UTC for non-cron APIs, but requests specify the local time without the offset.
    // note: Uses local-time-zone if none is specified.
    public static DateTimeOffset UseTimeZoneOffsetOrLocal(this DateTimeOffset value, TimeZoneInfo? timeZone = null)
    {
        return new DateTimeOffset(value.DateTime, (timeZone ?? TimeZoneInfo.Local).GetUtcOffset(DateTimeOffset.Now));
    }
}