
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data.Configurations;
public class DailyOccurrenceConfiguration : IEntityTypeConfiguration<DailyOccurrence> {

    public void Configure(EntityTypeBuilder<DailyOccurrence> builder) {

        builder.HasKey(o => o.Id);

        builder.Ignore(o => o.IsDone);
        builder.Ignore(o => o.StartTime);
        builder.Ignore(o => o.EndTime);

        builder.Property(o => o.SortOrder)
            .IsRequired();

        builder.HasOne(o => o.DailyPlan)
            .WithMany(p => p.Occurrences)
            .HasForeignKey(o => o.DailyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.TaskDefinition)
            .WithMany(t => t.DailyOccurrences)
            .HasForeignKey(o => o.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}