using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

public static class BlocksEndpoints
{
    public static void Map(RouteGroupBuilder rooms)
    {
        rooms.MapGet("/{roomId:guid}/blocks", ListAsync).WithName("ListRoomBlocks")
            .WithSummary("Lists the time ranges in which a room cannot be booked, optionally within a window.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapPost("/{roomId:guid}/blocks", CreateAsync).WithName("CreateRoomBlock")
            .WithSummary("Blocks a room for a time range, such as cleaning or repairs. Facilities only. Refused with 409 while confirmed bookings are in the range; the response lists them in 'bookingIds'.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        rooms.MapDelete("/{roomId:guid}/blocks/{blockId:guid}", DeleteAsync).WithName("DeleteRoomBlock")
            .WithSummary("Lifts a block. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<IReadOnlyList<RoomBlockResponse>>, ProblemHttpResult>> ListAsync(
        Guid roomId,
        [AsParameters] ListBlocksQuery query,
        ListBlocksHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(roomId, query, cancellationToken) is { } blocks
            ? TypedResults.Ok(blocks)
            : TypedResults.Problem(title: "Room not found", statusCode: StatusCodes.Status404NotFound);

    private static async Task<Results<Created<RoomBlockResponse>, ProblemHttpResult>> CreateAsync(
        Guid roomId,
        CreateBlockRequest request,
        CreateBlockHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(roomId, request, cancellationToken);

        return result switch
        {
            CreateBlockResult.Created created => TypedResults.Created($"/api/v1/rooms/{roomId}/blocks/{created.Block.Id}", created.Block),
            CreateBlockResult.BookingsInTheWay inTheWay => TypedResults.Problem(
                title: "Bookings in the way",
                detail: $"{inTheWay.BookingIds.Count} confirmed booking(s) overlap the range. Move or cancel them first.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["bookingIds"] = inTheWay.BookingIds }),
            _ => TypedResults.Problem(title: "Room not found", statusCode: StatusCodes.Status404NotFound),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid roomId,
        Guid blockId,
        DeleteBlockHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(roomId, blockId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Problem(title: "Block not found", statusCode: StatusCodes.Status404NotFound);
}