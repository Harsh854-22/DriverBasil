using System.Text.RegularExpressions;

namespace SecureDeviceControl.Domain.Security;

public static partial class PinPolicy
{
    public const int PinLength = 6;

    public static bool IsValid(string? pin)
    {
        return pin is not null && SixDigitPinRegex().IsMatch(pin);
    }

    [GeneratedRegex("^\\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex SixDigitPinRegex();
}
