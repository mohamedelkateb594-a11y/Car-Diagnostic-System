using Diagnostic.Presentation.Mvvm;

namespace Diagnostic.Presentation.Tests;

public class CommandTests
{
    [Fact]
    public async Task AsyncCommand_IsDisabledWhileRunning_AndRaisesCanExecuteChanged()
    {
        var gate = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => gate.Task);
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        var run = command.ExecuteAsync();
        Assert.False(command.CanExecute(null));

        gate.SetResult();
        await run;

        Assert.True(command.CanExecute(null));
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task AsyncCommand_WhenCanExecuteIsFalse_DoesNotRun()
    {
        var ran = false;
        var command = new AsyncRelayCommand(() =>
        {
            ran = true;
            return Task.CompletedTask;
        }, () => false);

        await command.ExecuteAsync();

        Assert.False(ran);
    }

    [Fact]
    public void RelayCommand_RaiseCanExecuteChanged_NotifiesSubscribers()
    {
        var command = new RelayCommand(_ => { }, _ => true);
        var raised = false;
        command.CanExecuteChanged += (_, _) => raised = true;

        command.RaiseCanExecuteChanged();

        Assert.True(raised);
    }
}
