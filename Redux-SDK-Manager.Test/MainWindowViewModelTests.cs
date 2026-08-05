using Redux_SDK_Manager.ViewModels;
using Xunit;

namespace Redux_SDK_Manager.Test;

public class MainWindowViewModelTests
{
    [Fact]
    public void Greeting_ReturnsWelcomeMessage()
    {
        var vm = new MainWindowViewModel();

        Assert.Equal("Welcome to Redux SDK Manager", vm.Greeting);
    }
}
