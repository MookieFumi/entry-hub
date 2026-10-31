namespace Gateway.Routing;

internal static class RoutesFile
{
    // The routes live in their own file so the migration is a change to that file. The proxy reloads it without
    // restarting.
    public static void AddRoutesFile(this ConfigurationManager configuration)
    {
        var routesFile = configuration["Gateway:RoutesFile"] ?? "routes.step1.json";
        configuration.AddJsonFile(routesFile, optional: false, reloadOnChange: true);
    }
}