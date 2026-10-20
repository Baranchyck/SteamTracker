namespace SteamTracker.Tests;

public class CiFailCheck
{
    [Fact]
    public void AlwaysFails() => Assert.True(false, "Навмисно впалий тест для перевірки CI");
}
