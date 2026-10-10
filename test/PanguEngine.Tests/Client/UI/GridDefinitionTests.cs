using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Tests.Client.UI;

public sealed class GridDefinitionTests
{
    [Fact]
    public void DefinitionsCopyInputAndCompareByValue()
    {
        var input = new[] { new GridDefinition(GridLength.Auto), new GridDefinition(40) };
        var definitions = new GridDefinitions(input);
        input[0] = new GridDefinition(100);

        Assert.Equal(GridLength.Auto, definitions[0].Length);
        Assert.Equal(new GridDefinitions(GridLength.Auto, 40), definitions);
        Assert.Equal(new GridDefinitions(GridLength.Auto, 40).GetHashCode(), definitions.GetHashCode());
        Assert.Equal(2, definitions.Count);
        Assert.Equal(definitions[0], definitions.First());
    }

    [Fact]
    public void DefaultValuesRepresentZeroLengthWithoutMaximum()
    {
        var definition = default(GridDefinition);
        Assert.Equal(new GridLength(0), definition.Length);
        Assert.Equal(0, definition.MinLength);
        Assert.Equal(double.PositiveInfinity, definition.MaxLength);
        Assert.Equal(new GridDefinition(0), definition);
        Assert.Equal(new GridLength(1, GridUnitType.Auto), new GridLength(10, GridUnitType.Auto));
    }

    [Fact]
    public void ParseAcceptsMixedUnitsAndNone()
    {
        var definitions = GridDefinitions.Parse(" Auto , 2*, 40px, 1.5, 0* ");
        Assert.Equal(new GridDefinitions(GridLength.Auto, new GridLength(2, GridUnitType.Star),
            40, 1.5, new GridLength(0, GridUnitType.Star)), definitions);
        Assert.Equal(GridDefinitions.Empty, GridDefinitions.Parse("none"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Auto,")]
    [InlineData(",*")]
    [InlineData("1,,2")]
    [InlineData("-1")]
    [InlineData("-2*")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("2fr")]
    [InlineData("50%")]
    [InlineData("1px*")]
    [InlineData("1 2")]
    public void ParseRejectsUnsupportedValues(string value) =>
        Assert.Throws<FormatException>(() => GridDefinitions.Parse(value));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void LengthAndMinimumRejectInvalidNumbers(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridLength(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridLength(value, GridUnitType.Star));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridDefinition(GridLength.Star, value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void MaximumRejectsInvalidNumbers(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridDefinition(GridLength.Star, maxLength: value));
}
