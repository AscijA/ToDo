using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data.Configurations;

public class WeeklyOccurrenceConfiguration : IEntityTypeConfiguration<WeeklyOccurrence> {
    public void Configure(EntityTypeBuilder<WeeklyOccurrence> builder) {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.DayOfWeek)
            .HasConversion<int?>();

        builder.HasIndex(o => new { o.WeeklyPlanId, o.DayOfWeek, o.TaskDefinitionId });

        builder.HasOne(o => o.WeeklyPlan)
            .WithMany(p => p.Occurrences)
            .HasForeignKey(o => o.WeeklyPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.TaskDefinition)
            .WithMany(t => t.WeeklyOccurrences)
            .HasForeignKey(o => o.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
