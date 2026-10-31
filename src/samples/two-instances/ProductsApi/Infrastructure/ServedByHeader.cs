namespace ProductsApi.Infrastructure;

internal static class ServedByHeader
{
    // Instance:Name names this instance in the X-Served-By header.
    public static void UseServedBy(this IApplicationBuilder app, string name)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Served-By"] = name;
            await next();
        });
    }
}