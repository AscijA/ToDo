using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Plans;

namespace ToDo.Infrastructure.Data.Configurations;

public class WeeklyPlanConfiguration : IEntityTypeConfiguration<WeeklyPlan> {
    public void Configure(EntityTypeBuilder<WeeklyPlan> builder) {
        builder.HasKey(wp => wp.Id);

        builder.Property(wp => wp.Date)
            .IsRequired();

        builder.HasIndex(wp => wp.Date).IsUnique();

        builder.HasMany(wp => wp.Occurrences)
            .WithOne(o => o.WeeklyPlan)
            .HasForeignKey(o => o.WeeklyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
