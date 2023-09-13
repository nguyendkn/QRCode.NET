namespace QRCode.Abstracts;

public struct EccInfo
{
    public EccInfo(
        int version,
        EccLevel errorCorrectionLevel,
        int totalDataCodewords,
        int eccPerBlock,
        int blocksInGroup1,
        int codewordsInGroup1,
        int blocksInGroup2,
        int codewordsInGroup2
    )
    {
        Version = version;
        ErrorCorrectionLevel = errorCorrectionLevel;
        TotalDataCodewords = totalDataCodewords;
        EccPerBlock = eccPerBlock;
        BlocksInGroup1 = blocksInGroup1;
        CodewordsInGroup1 = codewordsInGroup1;
        BlocksInGroup2 = blocksInGroup2;
        CodewordsInGroup2 = codewordsInGroup2;
    }

    public int Version { get; }
    public EccLevel ErrorCorrectionLevel { get; }
    public int TotalDataCodewords { get; }
    public int EccPerBlock { get; }
    public int BlocksInGroup1 { get; }
    public int CodewordsInGroup1 { get; }
    public int BlocksInGroup2 { get; }
    public int CodewordsInGroup2 { get; }
}