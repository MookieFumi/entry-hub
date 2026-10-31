namespace NewApi.Infrastructure;

internal static class ServedByHeader
{
    public static void UseServedBy(this IApplicationBuilder app, string name)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Served-By"] = name;
            await next();
        });
    }
}