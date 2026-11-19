// ReSharper disable once CheckNamespace
namespace Xunit;

partial class Assert
{
    public static T NotNull<T>(T? value) where T : class
    {
        // note: Existing non-generic assertion.
        NotNull((object?)value);
        return value!;
    }
}