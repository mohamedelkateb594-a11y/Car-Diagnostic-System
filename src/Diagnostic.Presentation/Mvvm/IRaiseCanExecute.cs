namespace Diagnostic.Presentation.Mvvm;

/// <summary>Lets a view model tell the UI that CanExecute must be evaluated again.</summary>
public interface IRaiseCanExecute
{
    void RaiseCanExecuteChanged();
}
