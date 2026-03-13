using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Entities;

internal sealed class OrderBookEntityConfiguration : IEntityTypeConfiguration<OrderBookDbo>
{
    public void Configure(EntityTypeBuilder<OrderBookDbo> builder)
    {
        builder.ToTable("order_books");

        builder.HasKey(e => e.Id)
            .HasName("order_books_pkey");

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
