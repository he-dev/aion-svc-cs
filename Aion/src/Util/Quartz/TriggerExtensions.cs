using System;
using System.Collections.Generic;
using Quartz;

namespace Aion.Util.Quartz;

public static class TriggerExtensions
{
    public static IEnumerable<DateTimeOffset> FiresAt(this ITrigger trigger, DateTimeOffset afterTimeUtc)
    {
        while (true)
        {
            if (trigger.GetFireTimeAfter(afterTimeUtc) is { } nextFireTimeUtc)
            {
                yield return nextFireTimeUtc;
                afterTimeUtc = nextFireTimeUtc;
            }
            else
            {
                yield break;
            }
        }
    }
}