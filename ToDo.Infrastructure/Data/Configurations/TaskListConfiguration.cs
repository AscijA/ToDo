using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities;

namespace ToDo.Infrastructure.Data.Configurations;

public class TaskListConfiguration : IEntityTypeConfiguration<TaskList> {
    public void Configure(EntityTypeBuilder<TaskList> builder) {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Color).IsRequired().HasMaxLength(50);
        builder.Property(l => l.Description).HasMaxLength(1000);
        builder.HasMany(i => i.Items)
            .WithOne(i => i.TaskList)
            .HasForeignKey(i => i.TaskListId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
