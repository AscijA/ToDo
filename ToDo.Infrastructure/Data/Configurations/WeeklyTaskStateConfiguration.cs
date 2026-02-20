using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data.Configurations;

public class WeeklyTaskStateConfiguration : IEntityTypeConfiguration<WeeklyTaskState> {
    public void Configure(EntityTypeBuilder<WeeklyTaskState> builder) {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.WeekKey).IsRequired().HasMaxLength(10);
        builder.Ignore(w => w.IsCompleted);

        builder.HasIndex(w => new { w.TaskDefinitionId, w.WeekKey }).IsUnique();
    }
}
