using SteamTracker.Domain.Exceptions; 
using SteamTracker.Integrations.Auth;

namespace SteamTracker.Tests.Auth;

public class SteamOpenIdValidatorTests
{
    private const string ReturnTo = "http://localhost:5000/callback";
    private const string SteamId = "76561198000000000";

    private static Dictionary<string, string> ValidParameters() => new()
    {
        ["openid.ns"] = "http://specs.openid.net/auth/2.0",
        ["openid.mode"] = "id_res",
        ["openid.op_endpoint"] = "https://steamcommunity.com/openid/login",
        ["openid.return_to"] = ReturnTo,
        ["openid.claimed_id"] = $"https://steamcommunity.com/openid/id/{SteamId}"
    };

    [Fact]
    public void ExtractSteamId_ValidParameters_ReturnsSteamId()
    {
        var result = SteamOpenIdValidator.ExtractSteamId(ValidParameters(), ReturnTo);

        Assert.Equal(SteamId, result);
    }

    [Fact]
    public void ExtractSteamId_InvalidClaimedId_Throws()
    {
        var parameters = ValidParameters();
        parameters["openid.claimed_id"] = "https://evil.com/openid/id/76561198000000000";

        Assert.Throws<AuthFailedException>(() =>
            SteamOpenIdValidator.ExtractSteamId(parameters, ReturnTo));
    }

    [Fact]
    public void ExtractSteamId_TamperedReturnTo_Throws()
    {
        var parameters = ValidParameters();
        parameters["openid.return_to"] = "http://localhost:6666/callback";

        Assert.Throws<AuthFailedException>(() =>
            SteamOpenIdValidator.ExtractSteamId(parameters, ReturnTo));
    }

    [Theory]
    [InlineData("openid.ns")]
    [InlineData("openid.mode")]
    [InlineData("openid.op_endpoint")]
    [InlineData("openid.return_to")]
    [InlineData("openid.claimed_id")]
    public void ExtractSteamId_MissingParameter_Throws(string missingKey)
    {
        var parameters = ValidParameters();
        parameters.Remove(missingKey);

        Assert.Throws<AuthFailedException>(() =>
            SteamOpenIdValidator.ExtractSteamId(parameters, ReturnTo));
    }
}