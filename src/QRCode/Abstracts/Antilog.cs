namespace QRCode.Abstracts;

public struct Antilog
{
    public Antilog(int exponentAlpha, int integerValue)
    {
        ExponentAlpha = exponentAlpha;
        IntegerValue = integerValue;
    }

    public int ExponentAlpha { get; }
    public int IntegerValue { get; }
}