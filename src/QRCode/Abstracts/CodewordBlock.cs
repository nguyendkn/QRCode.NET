namespace QRCode.Abstracts;

public struct CodewordBlock
{
    public CodewordBlock(
        List<string> codeWords,
        List<string> eccWords
    )
    {
        CodeWords = codeWords;
        EccWords = eccWords;
    }

    public List<string> CodeWords { get; }
    public List<string> EccWords { get; }
}