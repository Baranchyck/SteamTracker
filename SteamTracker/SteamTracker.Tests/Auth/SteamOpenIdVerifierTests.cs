using SteamTracker.Integrations.Auth;

namespace SteamTracker.Tests.Auth;

public class SteamOpenIdVerifierTests
{
    [Theory]
    [InlineData("ns:http://specs.openid.net/auth/2.0\nis_valid:true\n", true)]
    [InlineData("ns:http://specs.openid.net/auth/2.0\r\nis_valid:true\r\n", true)]
    [InlineData("ns:http://specs.openid.net/auth/2.0\nis_valid:false\n", false)]
    [InlineData("", false)]
    [InlineData("garbage", false)]
    public void IsValid_ParsesSteamResponse(string body, bool expected)
    {
        Assert.Equal(expected, SteamOpenIdVerifier.IsValid(body));
    }
}