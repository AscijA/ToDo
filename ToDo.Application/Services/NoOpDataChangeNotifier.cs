using ToDo.Application.Interfaces.Services;

namespace ToDo.Application.Services;

public sealed class NoOpDataChangeNotifier : IDataChangeNotifier {
    public void NotifyChanged(params Guid[] entityIds) {
    }

    public void NotifyDeleted(params Guid[] entityIds) {
    }
}
