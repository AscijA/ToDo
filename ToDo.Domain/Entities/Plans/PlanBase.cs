namespace ToDo.Domain.Entities.Plans;

public abstract class PlanBase : EntityBase {
    public DateOnly Date { get; set; }

}