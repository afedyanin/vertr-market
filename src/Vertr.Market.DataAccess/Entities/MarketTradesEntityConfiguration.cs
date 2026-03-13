using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Entities;

internal class MarketTradesEntityConfiguration : IEntityTypeConfiguration<MarketTradesDbo>
{
    public void Configure(EntityTypeBuilder<MarketTradesDbo> builder)
    {
        builder.ToTable("market_trades");

        builder.HasKey(e => e.Id)
            .HasName("market_trades_pkey");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(e => e.TimeUtc)
            .HasColumnName("time_utc")
            .IsRequired();

        builder.Property(e => e.JsonContent)
            .HasColumnName("json_content")
            .HasColumnType("jsonb");
    }
}
