namespace MiniErp.App.BuildingBlocks.Composition;

internal static class ModuleRegistration
{
    public static T Create<T>(Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory();
    }
}
