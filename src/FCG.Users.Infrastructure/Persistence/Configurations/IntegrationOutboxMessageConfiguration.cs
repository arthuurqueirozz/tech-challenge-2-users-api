using FCG.Users.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Users.Infrastructure.Persistence.Configurations;

public sealed class IntegrationOutboxMessageConfiguration
    : IEntityTypeConfiguration<IntegrationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationOutboxMessage> builder)
    {
        builder.ToTable("IntegrationOutboxMessages");

        builder.HasKey(message => message.Id);

        builder
            .Property(message => message.MessageType)
            .HasMaxLength(256)
            .IsRequired();

        builder
            .Property(message => message.Payload)
            .IsRequired();

        builder
            .Property(message => message.LastError)
            .HasMaxLength(2_048);

        builder.HasIndex(message => message.ProcessedAtUtc);
        builder.HasIndex(message => message.NextAttemptAtUtc);
    }
}
