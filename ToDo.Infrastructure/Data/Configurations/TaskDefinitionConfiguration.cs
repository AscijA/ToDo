using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities;

namespace ToDo.Infrastructure.Data.Configurations;
public class TaskDefinitionConfiguration : IEntityTypeConfiguration<TaskDefinition> {

    public void Configure(EntityTypeBuilder<TaskDefinition> builder) {

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Bucket)
            .HasConversion<string>();

        builder.HasMany(t => t.DailyOccurrences)
            .WithOne(o => o.TaskDefinition)
            .HasForeignKey(o => o.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.WeeklyOccurrences)
            .WithOne(s => s.TaskDefinition)
            .HasForeignKey(s => s.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.MonthlyOccurrences)
            .WithOne(s => s.TaskDefinition)
            .HasForeignKey(s => s.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
