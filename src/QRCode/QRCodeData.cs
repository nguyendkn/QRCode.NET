using System.Collections;

namespace QRCode;

public class QRCodeData : IDisposable
{
    public List<BitArray> ModuleMatrix { get; }

    public QRCodeData(int version)
    {
        Version = version;
        var size = ModulesPerSideFromVersion(version);
        ModuleMatrix = new List<BitArray>();
        for (var i = 0; i < size; i++)
            ModuleMatrix.Add(new BitArray(size));
    }

    public int Version { get; private set; }

    private static int ModulesPerSideFromVersion(int version)
    {
        return 21 + (version - 1) * 4;
    }

    public void Dispose()
    {
        Version = 0;
    }
}