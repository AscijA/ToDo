using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data.Configurations;

public class MonthlyTaskStateConfiguration : IEntityTypeConfiguration<MonthlyTaskState> {
    public void Configure(EntityTypeBuilder<MonthlyTaskState> builder) {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MonthKey).IsRequired().HasMaxLength(7);
        builder.Ignore(m => m.IsCompleted);

        builder.HasIndex(m => new { m.TaskDefinitionId, m.MonthKey }).IsUnique();
    }
}