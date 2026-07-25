using System.Globalization;
using PanguEngine.World;
using PanguEngine.World.Blocks;

namespace PanguEngine.Tests.World.Blocks;

public sealed class BlockPropertyTests
{
    // --- Boolean property ---

    [Fact]
    public void BooleanPropertyHasFalseThenTrueValues()
    {
        var prop = BlockProperty.CreateBoolean("powered");

        Assert.Equal(2, prop.Values.Count);
        Assert.Equal(false, prop.Values[0]);
        Assert.Equal(true, prop.Values[1]);
    }

    [Fact]
    public void BooleanPropertyNameIsPreserved()
    {
        var prop = BlockProperty.CreateBoolean("open");

        Assert.Equal("open", prop.Name);
    }

    // --- Enum property ---

    [Fact]
    public void EnumPropertyPreservesDeclarationOrder()
    {
        var prop = BlockProperty.CreateEnum("facing",
            Direction.North, Direction.South, Direction.West, Direction.East);

        Assert.Equal(4, prop.Values.Count);
        Assert.Equal(Direction.North, prop.Values[0]);
        Assert.Equal(Direction.East, prop.Values[3]);
    }

    [Fact]
    public void EnumPropertyAcceptsSubset()
    {
        var prop = BlockProperty.CreateEnum("axis",
            Direction.North, Direction.East);

        Assert.Equal(2, prop.Values.Count);
    }

    // --- Integer property ---

    [Fact]
    public void IntegerPropertyGeneratesAscendingClosedRange()
    {
        var prop = BlockProperty.CreateInteger("layers", 1, 8);

        Assert.Equal(8, prop.Values.Count);
        Assert.Equal(1, prop.Values[0]);
        Assert.Equal(8, prop.Values[7]);
        for (var i = 0; i < 8; i++)
            Assert.Equal(1 + i, prop.Values[i]);
    }

    [Fact]
    public void IntegerPropertySingleValueIsAllowed()
    {
        var prop = BlockProperty.CreateInteger("age", 5, 5);

        Assert.Single(prop.Values);
        Assert.Equal(5, prop.Values[0]);
    }

    [Fact]
    public void IntegerPropertyAt65536ValuesSucceeds()
    {
        // Range [0, 65535] produces exactly 65536 values.
        var prop = BlockProperty.CreateInteger("big", 0, 65535);

        Assert.Equal(65536, prop.Values.Count);
    }

    [Fact]
    public void IntegerPropertyValueStringUsesInvariantNegativeSign()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        try
        {
            CultureInfo.CurrentCulture = culture;
            var prop = BlockProperty.CreateInteger("level", -1, 0);

            Assert.Equal("-1", prop.GetValueString(0));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // --- Validation errors ---

    [Fact]
    public void NullNameThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BlockProperty.CreateBoolean(null!));
        Assert.Throws<ArgumentNullException>(() => BlockProperty.CreateEnum<Direction>(null!, Direction.North));
        Assert.Throws<ArgumentNullException>(() => BlockProperty.CreateInteger(null!, 0, 1));
    }

    [Fact]
    public void EmptyNameThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BlockProperty.CreateBoolean(""));
    }

    [Fact]
    public void UppercaseNameThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BlockProperty.CreateBoolean("Powered"));
    }

    [Fact]
    public void SpaceInNameThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BlockProperty.CreateBoolean("has power"));
    }

    [Fact]
    public void NullEnumValuesArrayThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BlockProperty.CreateEnum<Direction>("facing", null!));
    }

    [Fact]
    public void EmptyEnumValuesThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            BlockProperty.CreateEnum<Direction>("facing"));
    }

    [Fact]
    public void DuplicateEnumValueThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            BlockProperty.CreateEnum("facing", Direction.North, Direction.North));
    }

    [Fact]
    public void IntegerMinGreaterThanMaxThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlockProperty.CreateInteger("level", 5, 4));
    }

    [Fact]
    public void IntegerPropertyExceeding65536ValuesThrowsArgumentOutOfRangeException()
    {
        // Range [0, 65536] produces 65537 values.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BlockProperty.CreateInteger("big", 0, 65536));
    }

    // --- Values collection immutability ---

    [Fact]
    public void ValuesCollectionIsReadOnly()
    {
        var prop = BlockProperty.CreateBoolean("powered");

        Assert.IsAssignableFrom<IReadOnlyList<bool>>(prop.Values);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<bool>)prop.Values).Add(true));
    }

    // --- Reference identity ---

    [Fact]
    public void TwoPropertiesWithSameNameAreDifferentObjects()
    {
        var p1 = BlockProperty.CreateBoolean("powered");
        var p2 = BlockProperty.CreateBoolean("powered");

        Assert.NotSame(p1, p2);
    }
}