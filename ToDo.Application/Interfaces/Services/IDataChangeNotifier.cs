namespace ToDo.Application.Interfaces.Services;

public interface IDataChangeNotifier {
    void NotifyChanged(params Guid[] entityIds);
    void NotifyDeleted(params Guid[] entityIds);
}
