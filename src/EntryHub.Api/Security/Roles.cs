namespace EntryHub.Api.Security;

public static class Roles
{
    public const string Employee = nameof(Employee);

    public const string Facilities = nameof(Facilities);
}

public static class Policies
{
    /// <summary>
    /// Any signed-in person with a known role.
    /// </summary>
    public const string AnyEmployee = nameof(AnyEmployee);

    public const string Facilities = nameof(Facilities);
}