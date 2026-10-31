namespace Balancer.Routing;

internal static class RoutesFile
{
    // Balancer:RoutesFile picks which of the balancer.*.json files the proxy loads.
    public static void AddRoutesFile(this ConfigurationManager configuration)
    {
        var routesFile = configuration["Balancer:RoutesFile"] ?? "balancer.round-robin.json";
        configuration.AddJsonFile(routesFile, optional: false, reloadOnChange: true);
    }
}