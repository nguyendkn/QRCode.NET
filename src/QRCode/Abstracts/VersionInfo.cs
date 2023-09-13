namespace QRCode.Abstracts;

public struct VersionInfo
{
    public VersionInfo(int version, List<VersionInfoDetails> versionInfoDetails)
    {
        Version = version;
        Details = versionInfoDetails;
    }

    public int Version { get; }
    public List<VersionInfoDetails> Details { get; }
}

public struct VersionInfoDetails
{
    public VersionInfoDetails(EccLevel errorCorrectionLevel, Dictionary<EncodingMode, int> capacityDict)
    {
        ErrorCorrectionLevel = errorCorrectionLevel;
        CapacityDict = capacityDict;
    }

    public EccLevel ErrorCorrectionLevel { get; }
    public Dictionary<EncodingMode, int> CapacityDict { get; }
}