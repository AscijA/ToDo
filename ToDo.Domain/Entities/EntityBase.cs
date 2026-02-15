using System;
using System.Collections.Generic;
using System.Text;

namespace ToDo.Domain.Entities;

public class EntityBase {

    public Guid Id { get; set; } = Guid.NewGuid();

}
