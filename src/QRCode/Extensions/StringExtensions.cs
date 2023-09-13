using System.Text;
using QRCode.Abstracts;

namespace QRCode.Extensions;

public static class StringExtensions
{
    public static string GetVersionString(int version)
    {
        var generator = "1111100100101";

        var vStr = DecToBin(version, 6);
        var vStrEcc = vStr.PadRight(18, '0').TrimStart('0');
        while (vStrEcc.Length > 12)
        {
            var sb = new StringBuilder();
            generator = generator.PadRight(vStrEcc.Length, '0');
            for (var i = 0; i < vStrEcc.Length; i++)
                sb.Append((Convert.ToInt32(vStrEcc[i]) ^ Convert.ToInt32(generator[i])).ToString());
            vStrEcc = sb.ToString().TrimStart('0');
        }

        vStrEcc = vStrEcc.PadLeft(12, '0');
        vStr += vStrEcc;

        return vStr;
    }

    public static int BinToDec(string binStr)
    {
        return Convert.ToInt32(binStr, 2);
    }

    private static string DecToBin(int decNum)
    {
        return Convert.ToString(decNum, 2);
    }

    public static string DecToBin(int decNum, int padLeftUpTo)
    {
        var binStr = DecToBin(decNum);
        return binStr.PadLeft(padLeftUpTo, '0');
    }

    public static string GetFormatString(EccLevel level, int maskVersion)
    {
        var generator = "10100110111";
        const string fStrMask = "101010000010010";

        var fStr = level switch
        {
            EccLevel.L => "01",
            EccLevel.M => "00",
            EccLevel.Q => "11",
            _ => "10"
        };
        fStr += DecToBin(maskVersion, 3);
        var fStrEcc = fStr.PadRight(15, '0').TrimStart('0');
        while (fStrEcc.Length > 10)
        {
            var sb = new StringBuilder();
            generator = generator.PadRight(fStrEcc.Length, '0');
            for (var i = 0; i < fStrEcc.Length; i++)
                sb.Append((Convert.ToInt32(fStrEcc[i]) ^ Convert.ToInt32(generator[i])).ToString());
            fStrEcc = sb.ToString().TrimStart('0');
        }

        fStrEcc = fStrEcc.PadLeft(10, '0');
        fStr += fStrEcc;

        var sbMask = new StringBuilder();
        for (var i = 0; i < fStr.Length; i++)
            sbMask.Append((Convert.ToInt32(fStr[i]) ^ Convert.ToInt32(fStrMask[i])).ToString());
        return sbMask.ToString();
    }
}