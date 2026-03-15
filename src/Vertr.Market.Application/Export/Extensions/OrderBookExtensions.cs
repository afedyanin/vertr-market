using Vertr.Common.Contracts;
using Vertr.Market.Application.Export.Models;

namespace Vertr.Market.Application.Export.Extensions;

internal static class OrderBookExtensions
{
    extension(OrderBook orderBook)
    {
        public OrderBookDto ToDto()
        {
            var dto = new OrderBookDto();

            var depth = orderBook.Depth;

            return dto;
        }
    }
}
