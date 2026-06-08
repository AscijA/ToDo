using ToDo.Application.Interfaces.Services;

namespace ToDo.Application.Services;

public sealed class NoOpDataChangeNotifier : IDataChangeNotifier {
    public void NotifyChanged() {
    }
}
