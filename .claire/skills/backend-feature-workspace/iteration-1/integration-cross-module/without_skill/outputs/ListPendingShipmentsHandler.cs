namespace Shipping.Shipments.Features.ListPendingShipments;

public record ListPendingShipmentsQuery : IQuery<ListPendingShipmentsResult>;

public record ListPendingShipmentsResult(IReadOnlyList<ShipmentDto> Shipments);

internal class ListPendingShipmentsHandler(ShippingDbContext dbContext)
    : IQueryHandler<ListPendingShipmentsQuery, ListPendingShipmentsResult>
{
    public async Task<ListPendingShipmentsResult> Handle(
        ListPendingShipmentsQuery query,
        CancellationToken cancellationToken)
    {
        var shipments = await dbContext.Shipments
            .AsNoTracking()
            .Where(s => s.Status == ShipmentStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .ProjectToType<ShipmentDto>()
            .ToListAsync(cancellationToken);

        return new ListPendingShipmentsResult(shipments);
    }
}
