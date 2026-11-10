using Quartz;

namespace Aion.Util.Quartz;

public static class JobDataMapExtensions
{
    public static T Get<T>(this JobDataMap data) where T : notnull => data.Get<T>(typeof(T).Name);
}