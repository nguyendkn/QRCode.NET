# QRCoder

## Info

QRCode is a simple library, written in C#.NET, which enables you to create QR codes. It hasn't any dependencies to other libraries and is available as .NET Core PCL version on NuGet.

Feel free to grab-up/fork the project and make it better!

## Installation

Either checkout this Github repository or install QRCode via NuGet Package Manager. If you want to use NuGet just search for "QRCode" or run the following command in the NuGet Package Manager console:
```bash
PM> Install-Package nguyendk.QRCode
```

## Usage

You only need four lines of code, to generate and view your first QR code.

```csharp
var qrCodeData = QRCodeGenerator.CreateQrCode("The text which should be encoded.", EccLevel.Q);
var qrCode = new QRCode(qrCodeData);
var qrCodeImage = qrCode.GetGraphic(20);
qrCodeImage.SaveToImage(Guid.NewGuid().ToString("N") + ".png");
```
