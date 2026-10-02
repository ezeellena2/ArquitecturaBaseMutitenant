namespace ArquitecturaBaseMultitenant.Domain.ValueObjects.TaxIdValidators;

/// <summary>Comprueba formato, prefijo admitido y dígito verificador CUIT/CUIL de Argentina.</summary>
public static class ArgentineCuitValidator
{
    private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string digits)
    {
        if (digits.Length != 11 || !digits.All(character => character is >= '0' and <= '9')
            || digits[..2] is not ("20" or "23" or "24" or "27" or "30" or "33" or "34"))
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < Weights.Length; index++)
            sum += (digits[index] - '0') * Weights[index];

        var checkDigit = 11 - sum % 11;
        if (checkDigit == 11) checkDigit = 0;
        if (checkDigit == 10) return false;
        return digits[^1] - '0' == checkDigit;
    }
}
