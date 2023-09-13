using System.Text;
using QRCode.Abstracts;
using QRCode.Extensions;

namespace QRCode.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void GenerateQRCode()
    {
        var text = new StringBuilder()
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.")
            .AppendLine("The text which should be encoded.");
        var qrCodeData = QRCodeGenerator.CreateQrCode(text.ToString(), EccLevel.H);
        var qrCode = new QRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);
        qrCodeImage.SaveToImage("20230912.png");
    }

    [Test]
    public void DecodeQRCode()
    {
        var result = QRCodeGenerator.DecodeQrCode("12-09-2023-16-50.png".LoadImage());
    }
}