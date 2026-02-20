using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using ToDo.Domain.Entities;

namespace ToDo.Infrastructure.Data.Configurations;
public class DailyPlanConfiguration : IEntityTypeConfiguration<DailyPlan> {

    public void Configure(EntityTypeBuilder<DailyPlan> builder) { 
    
        builder.HasKey(dp => dp.Id);

        builder.HasIndex(dp => dp.Date).IsUnique();

        builder.HasMany(dp => dp.Occurrences)
            .WithOne(o => o.DailyPlan)
            .HasForeignKey(o => o.DailyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
