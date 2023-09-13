using System.Text;

namespace QRCode.Abstracts;

public struct Drawing
{
    public int Version;
    public List<Point> PatternPositions;
}

public class Point
{
    public int X { get; }
    public int Y { get; }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public class Rectangle
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public Rectangle(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }
}

public struct PolynomItem
{
    public PolynomItem(int coefficient, int exponent)
    {
        Coefficient = coefficient;
        Exponent = exponent;
    }

    public int Coefficient { get; }
    public int Exponent { get; }
}

public class Polynom
{
    public Polynom()
    {
        PolyItems = new List<PolynomItem>();
    }

    public List<PolynomItem> PolyItems { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var polyItem in PolyItems)
        {
            sb.Append("a^" + polyItem.Coefficient + "*x^" + polyItem.Exponent + " + ");
        }

        return sb.ToString().TrimEnd(' ', '+');
    }
}