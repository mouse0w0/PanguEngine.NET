using System.Globalization;

namespace PanguEngine.Client.UI.Styling;

/// <summary>Represents an integer position sequence with a non-negative sequence index.</summary>
internal readonly record struct UiNthExpression(int A, int B)
{
    /// <summary>Determines whether the supplied one-based position belongs to this sequence.</summary>
    internal bool Matches(int position)
    {
        if (A == 0)
            return position == B;
        var difference = (long)position - B;
        return difference % A == 0 && difference / A >= 0;
    }

    /// <summary>Returns the normalized CSS representation of this sequence.</summary>
    public override string ToString()
    {
        if (A == 0)
            return B.ToString(CultureInfo.InvariantCulture);
        var coefficient = A switch
        {
            1 => "n",
            -1 => "-n",
            _ => A.ToString(CultureInfo.InvariantCulture) + "n"
        };
        return B == 0 ? coefficient : coefficient +
            (B > 0 ? "+" : "") + B.ToString(CultureInfo.InvariantCulture);
    }
}
