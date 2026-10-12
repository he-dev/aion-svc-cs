using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Meta.Serilog.Enriching;

public class EnrichLogEventWithActivity : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (Activity.Current is { } activity)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(nameof(Activity.TraceId), activity.TraceId));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(nameof(Activity.SpanId), activity.SpanId));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(nameof(Activity.ParentId), activity.ParentId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.Status), activity.Status));
        }
    }
}