using SecureDeviceControl.Domain.Security;

namespace SecureDeviceControl.Domain.Tests;

public sealed class PinPolicyTests
{
    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    public void IsValidAcceptsSixDigitPins(string pin)
    {
        Assert.True(PinPolicy.IsValid(pin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    [InlineData("123 56")]
    public void IsValidRejectsPinsOutsideTheSixDigitPolicy(string pin)
    {
        Assert.False(PinPolicy.IsValid(pin));
    }
}
