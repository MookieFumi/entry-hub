namespace EntryHub.Api.Features;

/// <summary>
/// Registers slice handlers by convention, so a new slice needs no extra line in DI.
/// </summary>
/// <remarks>
/// A handler is any concrete class in a namespace below <c>EntryHub.Api.Features</c> whose name ends in
/// <c>Handler</c>. Each one is scoped, the same lifetime as the <c>DbContext</c> it usually depends on.
/// </remarks>
public static class HandlerRegistration
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        foreach (var handler in FindHandlers())
        {
            services.AddScoped(handler);
        }

        return services;
    }

    /// <summary>
    /// Finds every type the convention treats as a handler.
    /// </summary>
    public static IReadOnlyList<Type> FindHandlers()
    {
        var featuresNamespace = typeof(HandlerRegistration).Namespace + ".";

        return [.. typeof(HandlerRegistration).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal)
                && type.Namespace?.StartsWith(featuresNamespace, StringComparison.Ordinal) == true)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];
    }
}