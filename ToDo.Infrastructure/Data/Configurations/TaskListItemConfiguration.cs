using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain.Entities;

namespace ToDo.Infrastructure.Data.Configurations;

public class TaskListItemConfiguration : IEntityTypeConfiguration<TaskListItem> {
    public void Configure(EntityTypeBuilder<TaskListItem> builder) {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Text).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.IsDone).IsRequired();
    }
}
