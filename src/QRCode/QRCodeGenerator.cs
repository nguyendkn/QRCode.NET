using System.Net.Mime;
using System.Text;
using QRCode.Abstracts;
using QRCode.Exceptions;
using QRCode.Extensions;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace QRCode
{
    public abstract class QRCodeGenerator : IDisposable
    {
        private static readonly List<Drawing> AlignmentPatternTable = CreateAlignmentPatternTable();
        private static readonly List<EccInfo> CapacityEccTable = CreateCapacityEccTable();
        private static readonly List<VersionInfo> CapacityTable = CreateCapacityTable();
        private static readonly List<Antilog> GaloisField = CreateAntilogTable();
        private static readonly Dictionary<char, int> AlphanumEncDict = CreateAlphanumEncDict();

        public static QRCodeData CreateQrCode(string plainText, EccLevel eccLevel, bool forceUtf8 = false,
            bool utf8Bom = false, EciMode eciMode = EciMode.Default, int requestedVersion = -1)
        {
            return GenerateQrCode(plainText, eccLevel, forceUtf8, utf8Bom, eciMode, requestedVersion);
        }

        public static QRCodeData DecodeQrCode(SKBitmap bitmap)
        {
            var barcodeReader = new BarcodeReader();

            var result = barcodeReader.Decode(bitmap);
            
            return default!;
        }

        private static QRCodeData GenerateQrCode(string plainText, EccLevel eccLevel, bool forceUtf8 = false,
            bool utf8Bom = false, EciMode eciMode = EciMode.Default, int requestedVersion = -1)
        {
            // Lấy định dạng mã hóa dựa trên plainText và các tham số liên quan
            var encoding = GetEncodingFromPlaintext(plainText, forceUtf8);

            // Chuyển đổi plainText thành định dạng nhị phân
            var codedText = PlainTextToBinary(plainText, encoding, eciMode, utf8Bom, forceUtf8);

            // Lấy chiều dài dữ liệu đầu vào
            var dataInputLength = GetDataLength(encoding, plainText, codedText, forceUtf8);

            // Đặt phiên bản QR mã dựa trên tham số đã truyền hoặc tính toán từ dữ liệu đầu vào
            var version = requestedVersion;
            if (version == -1)
            {
                version = GetVersion(dataInputLength + (eciMode != EciMode.Default ? 2 : 0), encoding, eccLevel);
            }
            else
            {
                // Kiểm tra phiên bản đã chọn có hợp lệ không nếu phiên bản đã được truyền vào qua tham số
                var minVersion = GetVersion(dataInputLength + (eciMode != EciMode.Default ? 2 : 0), encoding, eccLevel);
                if (minVersion > version)
                {
                    var maxSizeByte = CapacityTable[version - 1].Details.First(x => x.ErrorCorrectionLevel == eccLevel)
                        .CapacityDict[encoding];
                    // Ném ngoại lệ nếu dữ liệu quá dài so với phiên bản đã chọn
                    throw new DataTooLongException(eccLevel.ToString(), encoding.ToString(), version,
                        maxSizeByte);
                }
            }

            // Khởi tạo chuỗi để chứa thông tin về chế độ và định dạng mã hóa
            var modeIndicator = string.Empty;
            if (eciMode != EciMode.Default)
            {
                modeIndicator = StringExtensions.DecToBin((int)eciMode, 8);
            }

            // Thêm thông tin về định dạng mã hóa và chiều dài dữ liệu vào chuỗi
            modeIndicator += StringExtensions.DecToBin((int)encoding, 4);
            var countIndicator = StringExtensions.DecToBin(dataInputLength, GetCountIndicatorLength(version, encoding));
            var bitString = modeIndicator + countIndicator;

            // Thêm dữ liệu đã mã hóa vào chuỗi
            bitString += codedText;

            // Tạo QR mã dựa trên chuỗi bit và trả về kết quả
            return GenerateQrCode(bitString, eccLevel, version);
        }

        private static QRCodeData GenerateQrCode(string bitString, EccLevel eccLevel, int version)
        {
            // Lấy thông tin ECC dựa trên phiên bản và mức ECC được chọn
            var eccInfo = CapacityEccTable.Single(x => x.Version == version && x.ErrorCorrectionLevel == eccLevel);

            // Điền đủ dữ liệu cho code word
            var dataLength = eccInfo.TotalDataCodewords * 8;
            var lengthDiff = dataLength - bitString.Length;
            // Thêm số bit '0' nếu cần để đạt đủ chiều dài
            if (lengthDiff > 0)
                bitString += new string('0', Math.Min(lengthDiff, 4));
            if ((bitString.Length % 8) != 0)
                bitString += new string('0', 8 - (bitString.Length % 8));
            // Thêm chuỗi mẫu cho đến khi đạt đủ chiều dài
            while (bitString.Length < dataLength)
                bitString += "1110110000010001";
            if (bitString.Length > dataLength)
                bitString = bitString[..dataLength];

            // Tính các từ mã lỗi
            var codeWordWithEcc = new List<CodewordBlock>(eccInfo.BlocksInGroup1 + eccInfo.BlocksInGroup2);
            for (var i = 0; i < eccInfo.BlocksInGroup1; i++)
            {
                var bitStr = bitString.Substring(i * eccInfo.CodewordsInGroup1 * 8, eccInfo.CodewordsInGroup1 * 8);
                var bitBlockList = BinaryStringToBitBlockList(bitStr);
                var eccWordList = CalculateEccWords(bitStr, eccInfo);
                codeWordWithEcc.Add(
                    new CodewordBlock(
                        bitBlockList,
                        eccWordList
                    )
                );
            }

            // Cắt chuỗi sau khi đã được xử lý cho nhóm 1
            bitString = bitString[(eccInfo.BlocksInGroup1 * eccInfo.CodewordsInGroup1 * 8)..];
            for (var i = 0; i < eccInfo.BlocksInGroup2; i++)
            {
                var bitStr = bitString.Substring(i * eccInfo.CodewordsInGroup2 * 8, eccInfo.CodewordsInGroup2 * 8);
                var bitBlockList = BinaryStringToBitBlockList(bitStr);
                var eccWordList = CalculateEccWords(bitStr, eccInfo);
                codeWordWithEcc.Add(new CodewordBlock(
                        bitBlockList,
                        eccWordList
                    )
                );
            }

            // Xáo trộn các từ mã
            var interleavedWordsSb = new StringBuilder();
            for (var i = 0; i < Math.Max(eccInfo.CodewordsInGroup1, eccInfo.CodewordsInGroup2); i++)
            {
                foreach (var codeBlock in codeWordWithEcc.Where(codeBlock => codeBlock.CodeWords.Count > i))
                    interleavedWordsSb.Append(codeBlock.CodeWords[i]);
            }

            for (var i = 0; i < eccInfo.EccPerBlock; i++)
            {
                foreach (var codeBlock in codeWordWithEcc.Where(codeBlock => codeBlock.EccWords.Count > i))
                    interleavedWordsSb.Append(codeBlock.EccWords[i]);
            }

            // Thêm số bit còn thiếu
            interleavedWordsSb.Append(new string('0', InitializeData.RemainderBits[version - 1]));
            var interleavedData = interleavedWordsSb.ToString();

            // Đặt dữ liệu đã xáo trộn lên ma trận module
            var qr = new QRCodeData(version);
            var blockedModules = new List<Rectangle>();
            // Đặt các mô hình tìm kiếm và dành chỗ cho chúng
            ModulePlacer.PlaceFinderPatterns(ref qr, ref blockedModules);
            ModulePlacer.ReserveSeperatorAreas(qr.ModuleMatrix.Count, ref blockedModules);
            // Đặt các mô hình căn chỉnh
            ModulePlacer.PlaceAlignmentPatterns(ref qr,
                AlignmentPatternTable.Where(x => x.Version == version).Select(x => x.PatternPositions).First(),
                ref blockedModules);
            // Đặt các mô hình thời gian
            ModulePlacer.PlaceTimingPatterns(ref qr, ref blockedModules);
            ModulePlacer.PlaceDarkModule(ref qr, version, ref blockedModules);
            ModulePlacer.ReserveVersionAreas(qr.ModuleMatrix.Count, version, ref blockedModules);
            // Đặt các từ dữ liệu
            ModulePlacer.PlaceDataWords(ref qr, interleavedData, ref blockedModules);
            // Áp dụng mặt nạ và trả về phiên bản mặt nạ tốt nhất
            var maskVersion = ModulePlacer.MaskCode(ref qr, version, ref blockedModules, eccLevel);
            var formatStr = StringExtensions.GetFormatString(eccLevel, maskVersion);

            // Đặt chuỗi định dạng lên QR
            ModulePlacer.PlaceFormat(ref qr, formatStr);
            // Đặt chuỗi phiên bản nếu phiên bản >= 7
            if (version >= 7)
            {
                var versionString = StringExtensions.GetVersionString(version);
                ModulePlacer.PlaceVersion(ref qr, versionString);
            }

            // Thêm vùng yên tĩnh xung quanh QR code
            ModulePlacer.AddQuietZone(ref qr);

            // Trả về dữ liệu QR code
            return qr;
        }

        private static List<string> CalculateEccWords(string bitString, EccInfo eccInfo)
        {
            // Lấy số từ mã lỗi sửa chữa (ECC) cho khối cho trước
            var eccWords = eccInfo.EccPerBlock;

            // Tính toán đa thức thông điệp dựa vào bitString đầu vào
            var messagePolynom = CalculateMessagePolynom(bitString);

            // Tính toán đa thức sinh cho số từ ECC
            var generatorPolynom = CalculateGeneratorPolynom(eccWords);

            // Điều chỉnh số mũ của đa thức thông điệp bằng số từ ECC
            for (var i = 0; i < messagePolynom.PolyItems.Count; i++)
                messagePolynom.PolyItems[i] = new PolynomItem(messagePolynom.PolyItems[i].Coefficient,
                    messagePolynom.PolyItems[i].Exponent + eccWords);

            // Điều chỉnh số mũ của đa thức sinh bằng bậc cao nhất của đa thức thông điệp trừ đi một
            for (var i = 0; i < generatorPolynom.PolyItems.Count; i++)
                generatorPolynom.PolyItems[i] = new PolynomItem(generatorPolynom.PolyItems[i].Coefficient,
                    generatorPolynom.PolyItems[i].Exponent + (messagePolynom.PolyItems.Count - 1));

            // Sử dụng phép chia tổng hợp để tìm dư khi chia đa thức thông điệp cho đa thức sinh
            var leadTermSource = messagePolynom;
            for (var i = 0;
                 (leadTermSource.PolyItems.Count > 0 &&
                  leadTermSource.PolyItems[^1].Exponent > 0);
                 i++)
            {
                // Xử lý trường hợp hệ số dẫn đầu là 0
                if (leadTermSource.PolyItems[0].Coefficient == 0)
                {
                    leadTermSource.PolyItems.RemoveAt(0);
                    leadTermSource.PolyItems.Add(new PolynomItem(0,
                        leadTermSource.PolyItems[^1].Exponent - 1));
                }
                else
                {
                    // Nhân đa thức sinh với hệ số dẫn đầu của đa thức thông điệp trong ký hiệu alpha
                    var resPoly = MultiplyGeneratorPolynomByLeadterm(generatorPolynom,
                        ConvertToAlphaNotation(leadTermSource).PolyItems[0], i);

                    // Chuyển về ký hiệu thập phân và XOR với đa thức thông điệp hiện tại để lấy phần dư
                    resPoly = ConvertToDecNotation(resPoly);
                    resPoly = XorPolynoms(leadTermSource, resPoly);
                    leadTermSource = resPoly;
                }
            }

            // Chuyển các hệ số của đa thức dư thành nhị phân và trả về dưới dạng danh sách chuỗi
            return leadTermSource.PolyItems.Select(x => StringExtensions.DecToBin(x.Coefficient, 8)).ToList();
        }

        private static Polynom ConvertToAlphaNotation(Polynom poly)
        {
            // Tạo một đa thức mới
            var newPoly = new Polynom();

            // Duyệt qua tất cả các phần tử của đa thức đầu vào
            for (var i = 0; i < poly.PolyItems.Count; i++)
                newPoly.PolyItems.Add(
                    new PolynomItem(
                        // Nếu hệ số khác 0, chuyển đổi hệ số sang ký hiệu alpha; ngược lại, giữ nguyên là 0
                        (poly.PolyItems[i].Coefficient != 0
                            ? GetAlphaExpFromIntVal(poly.PolyItems[i].Coefficient)
                            : 0),
                        poly.PolyItems[i].Exponent));

            // Trả về đa thức mới đã được chuyển đổi
            return newPoly;
        }

        private static Polynom ConvertToDecNotation(Polynom poly)
        {
            // Tạo một đa thức mới
            var newPoly = new Polynom();

            // Duyệt qua tất cả các phần tử của đa thức đầu vào và chuyển đổi hệ số từ ký hiệu alpha về thập phân
            for (var i = 0; i < poly.PolyItems.Count; i++)
                newPoly.PolyItems.Add(new PolynomItem(GetIntValFromAlphaExp(poly.PolyItems[i].Coefficient),
                    poly.PolyItems[i].Exponent));

            // Trả về đa thức mới đã được chuyển đổi
            return newPoly;
        }

        private static int GetVersion(int length, EncodingMode encMode, EccLevel eccLevel)
        {
            // Lấy danh sách các phiên bản phù hợp dựa trên dung lượng và mức độ sửa lỗi
            var fittingVersions = CapacityTable.Where(
                x => x.Details.Any(
                    y => y.ErrorCorrectionLevel == eccLevel
                         && y.CapacityDict[encMode] >= Convert.ToInt32(length)
                )
            ).Select(x => new
            {
                version = x.Version,
                capacity = x.Details.Single(y => y.ErrorCorrectionLevel == eccLevel)
                    .CapacityDict[encMode]
            }).ToList();

            // Nếu có phiên bản phù hợp, trả về phiên bản có dung lượng nhỏ nhất
            if (fittingVersions.Count != 0)
                return fittingVersions.Min(x => x.version);

            // Nếu không có phiên bản nào phù hợp, ném ra ngoại lệ
            var maxSizeByte = CapacityTable.Where(
                x => x.Details.Any(
                    y => (y.ErrorCorrectionLevel == eccLevel))
            ).Max(x => x.Details.Single(y => y.ErrorCorrectionLevel == eccLevel).CapacityDict[encMode]);

            throw new DataTooLongException(eccLevel.ToString(), encMode.ToString(), maxSizeByte);
        }

        private static EncodingMode GetEncodingFromPlaintext(string plainText, bool forceUtf8)
        {
            // Nếu buộc sử dụng UTF8, trả về chế độ mã hóa Byte
            if (forceUtf8) return EncodingMode.Byte;

            var result = EncodingMode.Numeric; // Giả sử là chế độ số

            // Duyệt qua từng ký tự của văn bản đầu vào
            foreach (var c in plainText.Where(c => !IsInRange(c, '0', '9')))
            {
                result = EncodingMode.Alphanumeric; // Không phải chế độ số, giả sử là chữ và số

                // Nếu ký tự là chữ cái hoặc nằm trong bảng mã AlphanumEncTable, tiếp tục kiểm tra
                if (IsInRange(c, 'A', 'Z') || InitializeData.AlphanumEncTable.Contains(c)) continue;

                return EncodingMode.Byte; // Không phải chế độ số hoặc chữ và số, trả về chế độ Byte
            }

            // Trả về kết quả (chế độ số hoặc chữ và số)
            return result;
        }

        private static bool IsInRange(char c, char min, char max)
        {
            // Kiểm tra xem ký tự c có nằm trong khoảng từ min đến max không
            return (uint)(c - min) <= (uint)(max - min);
        }

        private static Polynom CalculateMessagePolynom(string bitString)
        {
            var messagePol = new Polynom();

            // Chuyển chuỗi nhị phân thành các hạng tử của đa thức
            for (var i = bitString.Length / 8 - 1; i >= 0; i--)
            {
                messagePol.PolyItems.Add(new PolynomItem(StringExtensions.BinToDec(bitString[..8]), i));
                bitString = bitString.Remove(0, 8);
            }

            return messagePol;
        }

        private static Polynom CalculateGeneratorPolynom(int numEccWords)
        {
            var generatorPolynom = new Polynom();

            // Khởi tạo đa thức sinh với hai hạng tử
            generatorPolynom.PolyItems.AddRange(new[]
            {
                new PolynomItem(0, 1),
                new PolynomItem(0, 0)
            });

            // Mở rộng đa thức sinh bằng cách nhân nó với các đa thức khác
            for (var i = 1; i <= numEccWords - 1; i++)
            {
                var multiplierPolynom = new Polynom();
                multiplierPolynom.PolyItems.AddRange(new[]
                {
                    new PolynomItem(0, 1),
                    new PolynomItem(i, 0)
                });

                generatorPolynom = MultiplyAlphaPolynoms(generatorPolynom, multiplierPolynom);
            }

            return generatorPolynom;
        }

        private static List<string> BinaryStringToBitBlockList(string bitString)
        {
            const int blockSize = 8;

            // Tính số khối từ chuỗi nhị phân
            var numberOfBlocks = (int)Math.Ceiling(bitString.Length / (double)blockSize);
            var blocks = new List<string>(numberOfBlocks);

            // Chia chuỗi nhị phân thành từng khối có kích thước blockSize
            for (var i = 0; i < bitString.Length; i += blockSize)
            {
                blocks.Add(bitString.Substring(i, blockSize));
            }

            return blocks;
        }

        // Hàm trả về chiều dài chỉ số đếm dựa trên phiên bản và chế độ mã hóa
        private static int GetCountIndicatorLength(int version, EncodingMode encMode)
        {
            return version switch
            {
                // Dựa trên phiên bản và chế độ mã hóa để quyết định giá trị chiều dài chỉ số
                < 10 when encMode == EncodingMode.Numeric => 10,
                < 10 when encMode == EncodingMode.Alphanumeric => 9,
                < 10 => 8,
                < 27 when encMode == EncodingMode.Numeric => 12,
                < 27 when encMode == EncodingMode.Alphanumeric => 11,
                < 27 when encMode == EncodingMode.Byte => 16,
                < 27 => 10,
                _ => encMode switch
                {
                    EncodingMode.Numeric => 14,
                    EncodingMode.Alphanumeric => 13,
                    EncodingMode.Byte => 16,
                    _ => 12
                }
            };
        }

        // Hàm trả về chiều dài dữ liệu dựa trên các điều kiện như chế độ mã hóa và dữ liệu mã hóa
        private static int GetDataLength(EncodingMode encoding, string plainText, string codedText, bool forceUtf8)
        {
            return forceUtf8 || IsUtf8(encoding, plainText, forceUtf8) ? (codedText.Length / 8) : plainText.Length;
        }

        // Kiểm tra xem liệu chuỗi có được mã hóa bằng UTF8 hay không
        private static bool IsUtf8(EncodingMode encoding, string plainText, bool forceUtf8)
        {
            return encoding == EncodingMode.Byte && (!IsValidIso(plainText) || forceUtf8);
        }

        // Kiểm tra chuỗi có hợp lệ theo chuẩn ISO-8859-1 hay không
        private static bool IsValidIso(string input)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(input);
            var result = Encoding.GetEncoding("ISO-8859-1").GetString(bytes, 0, bytes.Length);
            return string.Equals(input, result);
        }

        // Chuyển đổi văn bản thường thành chuỗi nhị phân dựa trên chế độ mã hóa
        private static string PlainTextToBinary(string plainText, EncodingMode encMode, EciMode eciMode, bool utf8Bom,
            bool forceUtf8)
        {
            return encMode switch
            {
                EncodingMode.Alphanumeric => PlainTextToBinaryAlphanumeric(plainText),
                EncodingMode.Numeric => PlainTextToBinaryNumeric(plainText),
                EncodingMode.Byte => PlainTextToBinaryByte(plainText, eciMode, utf8Bom, forceUtf8),
                _ => string.Empty
            };
        }

        // Chuyển đổi văn bản số thành chuỗi nhị phân
        private static string PlainTextToBinaryNumeric(string plainText)
        {
            var codeText = string.Empty;
            while (plainText.Length >= 3)
            {
                var dec = Convert.ToInt32(plainText[..3]);
                codeText += StringExtensions.DecToBin(dec, 10);
                plainText = plainText[3..];
            }

            switch (plainText.Length)
            {
                case 2:
                {
                    var dec = Convert.ToInt32(plainText);
                    codeText += StringExtensions.DecToBin(dec, 7);
                    break;
                }
                case 1:
                {
                    var dec = Convert.ToInt32(plainText);
                    codeText += StringExtensions.DecToBin(dec, 4);
                    break;
                }
            }

            return codeText;
        }

        // Chuyển đổi văn bản chữ và số thành chuỗi nhị phân
        private static string PlainTextToBinaryAlphanumeric(string plainText)
        {
            var codeText = string.Empty;
            while (plainText.Length >= 2)
            {
                var token = plainText[..2];
                var dec = AlphanumEncDict[token[0]] * 45 + AlphanumEncDict[token[1]];
                codeText += StringExtensions.DecToBin(dec, 11);
                plainText = plainText[2..];
            }

            if (plainText.Length > 0)
            {
                codeText += StringExtensions.DecToBin(AlphanumEncDict[plainText[0]], 6);
            }

            return codeText;
        }

        // Chuyển đổi chuỗi thành định dạng ISO-8859
        private static string ConvertToIso8859(string value, string isoType = "ISO-8859-2")
        {
            var iso = Encoding.GetEncoding(isoType);
            var utf8 = Encoding.UTF8;
            var utfBytes = utf8.GetBytes(value);
            var isoBytes = Encoding.Convert(utf8, iso, utfBytes);

            return iso.GetString(isoBytes, 0, isoBytes.Length);
        }

        // Chuyển đổi văn bản thường thành chuỗi nhị phân theo chế độ Byte
        private static string PlainTextToBinaryByte(string plainText, EciMode eciMode, bool utf8Bom, bool forceUtf8)
        {
            byte[] codeBytes;
            var codeText = string.Empty;

            if (IsValidIso(plainText) && !forceUtf8)
                codeBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(plainText);
            else
            {
                switch (eciMode)
                {
                    case EciMode.Iso88591:
                        codeBytes = Encoding.GetEncoding("ISO-8859-1")
                            .GetBytes(ConvertToIso8859(plainText, "ISO-8859-1"));
                        break;
                    case EciMode.Iso88592:
                        codeBytes = Encoding.GetEncoding("ISO-8859-2")
                            .GetBytes(ConvertToIso8859(plainText));
                        break;
                    case EciMode.Default:
                    case EciMode.Utf8:
                    default:
                        codeBytes = utf8Bom
                            ? Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(plainText)).ToArray()
                            : Encoding.UTF8.GetBytes(plainText);
                        break;
                }
            }

            return codeBytes.Aggregate(codeText, (current, b) => current + StringExtensions.DecToBin(b, 8));
        }

        // Thực hiện phép toán XOR giữa hai đa thức
        private static Polynom XorPolynoms(Polynom messagePolynom, Polynom resPolynom)
        {
            var resultPolynom = new Polynom();
            Polynom longPoly, shortPoly;
            if (messagePolynom.PolyItems.Count >= resPolynom.PolyItems.Count)
            {
                longPoly = messagePolynom;
                shortPoly = resPolynom;
            }
            else
            {
                longPoly = resPolynom;
                shortPoly = messagePolynom;
            }

            for (var i = 0; i < longPoly.PolyItems.Count; i++)
            {
                var polItemRes = new PolynomItem
                (
                    longPoly.PolyItems[i].Coefficient ^
                    (shortPoly.PolyItems.Count > i ? shortPoly.PolyItems[i].Coefficient : 0),
                    messagePolynom.PolyItems[0].Exponent - i
                );
                resultPolynom.PolyItems.Add(polItemRes);
            }

            resultPolynom.PolyItems.RemoveAt(0);
            return resultPolynom;
        }

        // Phương thức để nhân đa thức tạo (generator polynomial) với một số hạng đứng đầu (lead term)
        private static Polynom MultiplyGeneratorPolynomByLeadterm(Polynom genPolynom, PolynomItem leadTerm,
            int lowerExponentBy)
        {
            // Tạo một đa thức mới để lưu kết quả
            var resultPolynom = new Polynom();
            // Duyệt qua mỗi số hạng của đa thức tạo và nhân với số hạng đứng đầu
            foreach (var polItemRes in genPolynom.PolyItems.Select(
                         polItemBase => new PolynomItem(
                             // Phép cộng theo module 255 cho hệ số
                             (polItemBase.Coefficient + leadTerm.Coefficient) % 255,
                             // Giảm số mũ xuống
                             polItemBase.Exponent - lowerExponentBy
                         )))
            {
                // Thêm kết quả vào đa thức kết quả
                resultPolynom.PolyItems.Add(polItemRes);
            }

            // Trả về đa thức kết quả
            return resultPolynom;
        }

        // Phương thức nhân hai đa thức khi hệ số là số mũ của alpha
        private static Polynom MultiplyAlphaPolynoms(Polynom polynomBase, Polynom polynomMultiplier)
        {
            // Tạo đa thức kết quả
            var resultPolynom = new Polynom();
            // Duyệt qua từng số hạng của hai đa thức và thực hiện phép nhân
            foreach (var polItemRes in polynomMultiplier.PolyItems.SelectMany(polItemBase =>
                         polynomBase.PolyItems.Select(polItemMulti => new PolynomItem
                         (
                             ShrinkAlphaExp(polItemBase.Coefficient + polItemMulti.Coefficient),
                             (polItemBase.Exponent + polItemMulti.Exponent)
                         ))))
            {
                resultPolynom.PolyItems.Add(polItemRes);
            }

            // Nhóm các số hạng có cùng số mũ lại và tính toán hệ số kết quả
            var exponentsToGlue = resultPolynom.PolyItems.GroupBy(x => x.Exponent).Where(x => x.Count() > 1)
                .Select(x => x.First().Exponent);
            var toGlue = exponentsToGlue as IList<int> ?? exponentsToGlue.ToList();
            var gluedPolynoms = new List<PolynomItem>(toGlue.Count);
            gluedPolynoms.AddRange(from exponent in toGlue
                let coefficient = resultPolynom.PolyItems.Where(x => x.Exponent == exponent).Aggregate(0,
                    (current, polynomOld) => current ^ GetIntValFromAlphaExp(polynomOld.Coefficient))
                select new PolynomItem(GetAlphaExpFromIntVal(coefficient), exponent));

            // Loại bỏ các số hạng cũ và thêm số hạng mới vào đa thức kết quả
            resultPolynom.PolyItems.RemoveAll(x => toGlue.Contains(x.Exponent));
            resultPolynom.PolyItems.AddRange(gluedPolynoms);
            // Sắp xếp lại các số hạng theo thứ tự giảm dần của số mũ
            resultPolynom.PolyItems.Sort((x, y) => -x.Exponent.CompareTo(y.Exponent));
            return resultPolynom;
        }

        // Lấy giá trị số nguyên từ số mũ của alpha
        private static int GetIntValFromAlphaExp(int exp)
        {
            return GaloisField.Find(antilog => antilog.ExponentAlpha == exp).IntegerValue;
        }

        // Lấy số mũ của alpha từ giá trị số nguyên
        private static int GetAlphaExpFromIntVal(int intVal)
        {
            return GaloisField.Find(antilog => antilog.IntegerValue == intVal).ExponentAlpha;
        }

        // Rút gọn số mũ của alpha
        private static int ShrinkAlphaExp(int alphaExp)
        {
            return (int)(alphaExp % 256 + Math.Floor((double)(alphaExp / 256)));
        }

        // Tạo bảng mã hóa cho các ký tự và số
        private static Dictionary<char, int> CreateAlphanumEncDict()
        {
            var localAlphanumEncDict = new Dictionary<char, int>(45);
            // Thêm số
            for (var i = 0; i < 10; i++)
                localAlphanumEncDict.Add($"{i}"[0], i);
            // Thêm ký tự
            for (var c = 'A'; c <= 'Z'; c++)
                localAlphanumEncDict.Add(c, localAlphanumEncDict.Count);
            // Thêm các ký tự đặc biệt
            foreach (var t in InitializeData.AlphanumEncTable)
                localAlphanumEncDict.Add(t, localAlphanumEncDict.Count);

            return localAlphanumEncDict;
        }

        // Hàm tạo bảng mẫu căn chỉnh (alignment pattern)
        private static List<Drawing> CreateAlignmentPatternTable()
        {
            // Khởi tạo danh sách lưu các mẫu căn chỉnh
            var localAlignmentPatternTable = new List<Drawing>(40);

            // Lặp qua mỗi nhóm giá trị căn chỉnh trong bảng dữ liệu
            for (var i = 0; i < (7 * 40); i = i + 7)
            {
                var points = new List<Point>();
                for (var x = 0; x < 7; x++)
                {
                    if (InitializeData.AlignmentPatternBaseValues[i + x] == 0) continue;
                    for (var y = 0; y < 7; y++)
                    {
                        if (InitializeData.AlignmentPatternBaseValues[i + y] == 0) continue;

                        // Tạo điểm căn chỉnh
                        var p = new Point(InitializeData.AlignmentPatternBaseValues[i + x] - 2,
                            InitializeData.AlignmentPatternBaseValues[i + y] - 2);

                        // Nếu danh sách points chưa chứa điểm này, thêm vào danh sách
                        if (!points.Contains(p))
                            points.Add(p);
                    }
                }

                // Thêm mẫu căn chỉnh mới vào danh sách
                localAlignmentPatternTable.Add(new Drawing()
                    {
                        Version = (i + 7) / 7,
                        PatternPositions = points
                    }
                );
            }

            // Trả về danh sách mẫu căn chỉnh
            return localAlignmentPatternTable;
        }

        // Hàm tạo bảng thông tin ECC (sửa lỗi và mã hóa)
        private static List<EccInfo> CreateCapacityEccTable()
        {
            var localCapacityEccTable = new List<EccInfo>(160);
            for (var i = 0; i < (4 * 6 * 40); i = i + (4 * 6))
            {
                // Thêm thông tin ECC cho các mức sửa lỗi L, M, Q, H
                localCapacityEccTable.AddRange(
                    new[]
                    {
                        new EccInfo(
                            (i + 24) / 24,
                            EccLevel.L,
                            InitializeData.CapacityEccBaseValues[i],
                            InitializeData.CapacityEccBaseValues[i + 1],
                            InitializeData.CapacityEccBaseValues[i + 2],
                            InitializeData.CapacityEccBaseValues[i + 3],
                            InitializeData.CapacityEccBaseValues[i + 4],
                            InitializeData.CapacityEccBaseValues[i + 5]),
                        new EccInfo
                        (
                            version: (i + 24) / 24,
                            errorCorrectionLevel: EccLevel.M,
                            totalDataCodewords: InitializeData.CapacityEccBaseValues[i + 6],
                            eccPerBlock: InitializeData.CapacityEccBaseValues[i + 7],
                            blocksInGroup1: InitializeData.CapacityEccBaseValues[i + 8],
                            codewordsInGroup1: InitializeData.CapacityEccBaseValues[i + 9],
                            blocksInGroup2: InitializeData.CapacityEccBaseValues[i + 10],
                            codewordsInGroup2: InitializeData.CapacityEccBaseValues[i + 11]
                        ),
                        new EccInfo
                        (
                            version: (i + 24) / 24,
                            errorCorrectionLevel: EccLevel.Q,
                            totalDataCodewords: InitializeData.CapacityEccBaseValues[i + 12],
                            eccPerBlock: InitializeData.CapacityEccBaseValues[i + 13],
                            blocksInGroup1: InitializeData.CapacityEccBaseValues[i + 14],
                            codewordsInGroup1: InitializeData.CapacityEccBaseValues[i + 15],
                            blocksInGroup2: InitializeData.CapacityEccBaseValues[i + 16],
                            codewordsInGroup2: InitializeData.CapacityEccBaseValues[i + 17]
                        ),
                        new EccInfo
                        (
                            version: (i + 24) / 24,
                            errorCorrectionLevel: EccLevel.H,
                            totalDataCodewords: InitializeData.CapacityEccBaseValues[i + 18],
                            eccPerBlock: InitializeData.CapacityEccBaseValues[i + 19],
                            blocksInGroup1: InitializeData.CapacityEccBaseValues[i + 20],
                            codewordsInGroup1: InitializeData.CapacityEccBaseValues[i + 21],
                            blocksInGroup2: InitializeData.CapacityEccBaseValues[i + 22],
                            codewordsInGroup2: InitializeData.CapacityEccBaseValues[i + 23]
                        )
                    });
            }

            return localCapacityEccTable;
        }

        // Hàm tạo bảng dung lượng
        private static List<VersionInfo> CreateCapacityTable()
        {
            var localCapacityTable = new List<VersionInfo>(40);
            for (var i = 0; i < (16 * 40); i = i + 16)
            {
                // Thêm thông tin dung lượng cho các phiên bản và mức sửa lỗi
                localCapacityTable.Add(new VersionInfo(
                    (i + 16) / 16,
                    new List<VersionInfoDetails>(4)
                    {
                        new(
                            EccLevel.L,
                            new Dictionary<EncodingMode, int>()
                            {
                                { EncodingMode.Numeric, InitializeData.CapacityBaseValues[i] },
                                { EncodingMode.Alphanumeric, InitializeData.CapacityBaseValues[i + 1] },
                                { EncodingMode.Byte, InitializeData.CapacityBaseValues[i + 2] },
                            }
                        ),
                        new(
                            EccLevel.M,
                            new Dictionary<EncodingMode, int>()
                            {
                                { EncodingMode.Numeric, InitializeData.CapacityBaseValues[i + 4] },
                                { EncodingMode.Alphanumeric, InitializeData.CapacityBaseValues[i + 5] },
                                { EncodingMode.Byte, InitializeData.CapacityBaseValues[i + 6] },
                            }
                        ),
                        new(
                            EccLevel.Q,
                            new Dictionary<EncodingMode, int>()
                            {
                                { EncodingMode.Numeric, InitializeData.CapacityBaseValues[i + 8] },
                                { EncodingMode.Alphanumeric, InitializeData.CapacityBaseValues[i + 9] },
                                { EncodingMode.Byte, InitializeData.CapacityBaseValues[i + 10] },
                            }
                        ),
                        new(
                            EccLevel.H,
                            new Dictionary<EncodingMode, int>()
                            {
                                { EncodingMode.Numeric, InitializeData.CapacityBaseValues[i + 12] },
                                { EncodingMode.Alphanumeric, InitializeData.CapacityBaseValues[i + 13] },
                                { EncodingMode.Byte, InitializeData.CapacityBaseValues[i + 14] },
                            }
                        )
                    }
                ));
            }

            return localCapacityTable;
        }

        // Hàm tạo bảng trường Galois (antilog table)
        private static List<Antilog> CreateAntilogTable()
        {
            var localGaloisField = new List<Antilog>(256);

            var gfItem = 1;
            for (var i = 0; i < 256; i++)
            {
                // Thêm giá trị vào bảng trường Galois
                localGaloisField.Add(new Antilog(i, gfItem));
                gfItem *= 2;
                if (gfItem > 255)
                    gfItem ^= 285;
            }

            // Trả về bảng trường Galois
            return localGaloisField;
        }

        public void Dispose()
        {
            // left for back-compat
        }
    }
}