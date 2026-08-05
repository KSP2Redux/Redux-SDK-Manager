using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Test;

public class MainWindowViewModelTests
{
    [Test]
    public void Greeting_ReturnsWelcomeMessage()
    {
        var vm = new MainWindowViewModel();

        Assert.That(vm.Greeting, Is.EqualTo("Welcome to Redux SDK Manager"));
    }
}
