using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

/// <summary>
/// The severity shown (and colour-coded) on /Admin/SecurityLog is derived
/// from the event type. These pin down the mapping and that every type and
/// severity has a real label in both languages.
/// </summary>
public class SecurityEventSeverityTests
{
    [Theory]
    [InlineData(SecurityEventType.LoginFailed, SecurityEventSeverity.Low)]
    [InlineData(SecurityEventType.RateLimited, SecurityEventSeverity.Medium)]
    [InlineData(SecurityEventType.AntiforgeryRejected, SecurityEventSeverity.Medium)]
    [InlineData(SecurityEventType.AccountLocked, SecurityEventSeverity.High)]
    [InlineData(SecurityEventType.AccessDenied, SecurityEventSeverity.High)]
    public void Severity_IsTheExpectedLevelForEachType(SecurityEventType type, SecurityEventSeverity expected)
    {
        Assert.Equal(expected, type.Severity());
    }

    [Fact]
    public void EveryType_MapsToAKnownSeverity_AndAReadableLabelInBothLanguages()
    {
        foreach (var type in Enum.GetValues<SecurityEventType>())
        {
            var severity = type.Severity();
            Assert.Contains(severity, new[] { SecurityEventSeverity.Low, SecurityEventSeverity.Medium, SecurityEventSeverity.High });
            Assert.False(string.IsNullOrWhiteSpace(type.DisplayName()));
            Assert.False(string.IsNullOrWhiteSpace(type.DisplayName(english: true)));
        }
    }

    [Theory]
    [InlineData(SecurityEventSeverity.Low, "Lav", "Low", "low")]
    [InlineData(SecurityEventSeverity.Medium, "Middel", "Medium", "medium")]
    [InlineData(SecurityEventSeverity.High, "Høj", "High", "high")]
    public void Severity_HasADanishAndEnglishLabel_AndALowercaseCssName(
        SecurityEventSeverity severity, string danish, string english, string css)
    {
        Assert.Equal(danish, severity.DisplayName());
        Assert.Equal(english, severity.DisplayName(english: true));
        Assert.Equal(css, severity.CssName());
    }
}
