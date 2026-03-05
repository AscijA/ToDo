using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data.Configurations;

public class MonthlyOccurrenceConfiguration : IEntityTypeConfiguration<MonthlyOccurrence> {
    public void Configure(EntityTypeBuilder<MonthlyOccurrence> builder) {
        builder.HasKey(o => o.Id);

        builder.HasOne(o => o.MonthlyPlan)
            .WithMany(p => p.Occurrences)
            .HasForeignKey(o => o.MonthlyPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.TaskDefinition)
            .WithMany(t => t.MonthlyOccurrences)
            .HasForeignKey(o => o.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}