namespace PanguEngine.World.Blocks;

internal sealed class BooleanBlockProperty : BlockProperty<bool>
{
    private static readonly bool[] SharedValues = [false, true];

    internal BooleanBlockProperty(string name) : base(name, SharedValues)
    {
    }

    internal override int ValueCount => 2;

    internal override string GetValueString(int valueIndex) => valueIndex == 0 ? "false" : "true";

    internal override int IndexOf(bool value) => value ? 1 : 0;
}