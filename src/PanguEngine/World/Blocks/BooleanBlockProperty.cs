namespace PanguEngine.World.Blocks;

internal sealed class BooleanBlockProperty : BlockProperty<bool>
{
    private static readonly bool[] SharedValues = [false, true];

    internal BooleanBlockProperty(string name) : base(name, SharedValues)
    {
    }

    internal override int ValueCount => 2;

    internal override string GetValueString(int valueIndex) => valueIndex == 0 ? "false" : "true";

    internal override int GetValueIndex(string value)
    {
        switch (value)
        {
            case "false":
                return 0;
            case "true":
                return 1;
            default:
                return -1;
        }
    }

    internal override int IndexOf(bool value) => value ? 1 : 0;
}