namespace PanguEngine.Server;

internal sealed class PlayerNames
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase);

    internal void Reserve(string username)
    {
        if (username.Length is < 1 or > 32 ||
            username.Any(character => character is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
            throw new InvalidDataException("Username must contain 1 to 32 ASCII letters, digits, or underscores.");
        lock (_gate)
        {
            if (!_reservedNames.Add(username))
                throw new InvalidDataException("Username is already in use.");
        }
    }

    internal void Release(string username)
    {
        lock (_gate)
            _reservedNames.Remove(username);
    }
}
