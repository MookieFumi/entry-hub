using EntryHub.Api.Features;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Tests.Support;

using Microsoft.Extensions.DependencyInjection;

namespace EntryHub.Api.Tests.Features;

public class HandlerRegistrationTests
{
    [Fact]
    public void Finds_the_handlers_of_every_slice()
    {
        var handlers = HandlerRegistration.FindHandlers();

        Assert.Contains(typeof(CreateRoomHandler), handlers);
        Assert.All(handlers, handler => Assert.EndsWith("Handler", handler.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_handler_resolves_from_the_container()
    {
        // Building the handler checks that its own dependencies are registered too. No query reaches the database.
        using var factory = new ApiFactory(TestDatabase.Unreachable);
        using var scope = factory.Services.CreateScope();

        Assert.All(HandlerRegistration.FindHandlers(), handler => Assert.NotNull(scope.ServiceProvider.GetService(handler)));
    }
}