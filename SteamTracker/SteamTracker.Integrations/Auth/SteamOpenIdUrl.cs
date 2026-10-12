namespace SteamTracker.Integrations.Auth;

internal static class SteamOpenIdUrl
{
    public const string LoginEndpoint = "https://steamcommunity.com/openid/login";
    public const string OpenIdNs = "http://specs.openid.net/auth/2.0";
    public const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";
    
    public static string Build(string returnTo, string realm)
    {
        var parameters = new Dictionary<string, string>
        {
            ["openid.ns"] = OpenIdNs,
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnTo,
            ["openid.realm"] = realm,
            ["openid.identity"] = IdentifierSelect,
            ["openid.claimed_id"] = IdentifierSelect
        };

        var query = string.Join("&", parameters.Select(p => 
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        
        return $"{LoginEndpoint}?{query}";
    }
}