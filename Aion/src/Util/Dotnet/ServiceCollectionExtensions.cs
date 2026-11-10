using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Util.Dotnet;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureFromSection<T>
    (
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null
    ) where T : class
    {
        sectionName ??= typeof(T).Name;
        sectionName = Regex.Replace(sectionName, "Options$", "");
        return services.Configure<T>(configuration.GetSection(sectionName));
    }
}