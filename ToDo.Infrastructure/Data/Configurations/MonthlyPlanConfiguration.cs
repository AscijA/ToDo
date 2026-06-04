using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Plans;

namespace ToDo.Infrastructure.Data.Configurations;

public class MonthlyPlanConfiguration : IEntityTypeConfiguration<MonthlyPlan> {
    public void Configure(EntityTypeBuilder<MonthlyPlan> builder) {
        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.Date)
            .IsRequired();

        builder.HasIndex(mp => mp.Date).IsUnique();

        builder.HasMany(mp => mp.Occurrences)
            .WithOne(o => o.MonthlyPlan)
            .HasForeignKey(o => o.MonthlyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
