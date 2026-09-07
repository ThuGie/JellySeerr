using Jellyfin.Plugin.JellySeerr.Helpers;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

/// <summary>
/// Guards against reintroducing the wrong MediaStatus mapping from stale Seerr OpenAPI
/// (<c>seerr-api.yml</c> still says <c>6 = DELETED</c>; runtime source is <c>media.ts</c>).
/// Snapshot checked 2026-09-07 against
/// https://github.com/seerr-team/seerr/blob/develop/server/constants/media.ts
/// </summary>
public class MediaStatusHelperTests
{
    // Seerr MediaStatus enum (auto-increment from UNKNOWN = 1)
    public static TheoryData<string, int> SeerrMediaTsCanonicalPairs => new()
    {
        { "UNKNOWN", 1 },
        { "PENDING", 2 },
        { "PROCESSING", 3 },
        { "PARTIALLY_AVAILABLE", 4 },
        { "AVAILABLE", 5 },
        { "BLOCKLISTED", 6 },
        { "DELETED", 7 }
    };

    [Theory]
    [MemberData(nameof(SeerrMediaTsCanonicalPairs))]
    public void Canonical_names_match_seerr_media_ts(string name, int expectedCode)
    {
        Assert.Equal(expectedCode, MediaStatusHelper.CanonicalNameToCode[name]);
        Assert.Equal(expectedCode, MediaStatusHelper.TryParseCode(name));
        Assert.Equal(expectedCode.ToString(), MediaStatusHelper.NormalizeToCodeString(name));
    }

    [Fact]
    public void Blocklisted_is_six_deleted_is_seven_not_openapi_swap()
    {
        // OpenAPI regression: seerr-api.yml documents "6 = DELETED" and omits BLOCKLISTED.
        Assert.Equal(6, MediaStatusHelper.Blocklisted);
        Assert.Equal(7, MediaStatusHelper.Deleted);
        Assert.Equal(6, MediaStatusHelper.TryParseCode("BLOCKLISTED"));
        Assert.Equal(7, MediaStatusHelper.TryParseCode("DELETED"));
        Assert.NotEqual(6, MediaStatusHelper.TryParseCode("DELETED"));
        Assert.NotEqual(7, MediaStatusHelper.TryParseCode("BLOCKLISTED"));
    }

    [Theory]
    [InlineData("BLACKLISTED", 6)]
    [InlineData("BLOCKED", 6)]
    [InlineData("blacklisted", 6)]
    public void Aliases_map_to_blocklisted(string alias, int expected)
    {
        Assert.Equal(expected, MediaStatusHelper.TryParseCode(alias));
    }

    [Fact]
    public void Labels_use_blocklisted_not_deleted_for_six()
    {
        Assert.Equal("Blocklisted", MediaStatusHelper.GetLabel(6));
        Assert.Equal("Deleted", MediaStatusHelper.GetLabel(7));
    }

    [Fact]
    public void Canonical_dictionary_has_exactly_seven_seerr_names()
    {
        Assert.Equal(7, MediaStatusHelper.CanonicalNameToCode.Count);
        Assert.DoesNotContain(MediaStatusHelper.CanonicalNameToCode.Keys, k =>
            string.Equals(k, "BLACKLISTED", StringComparison.OrdinalIgnoreCase));
    }
}
