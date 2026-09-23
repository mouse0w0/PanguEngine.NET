namespace PanguEngine.Client.UI.Drawing;

/// <summary>Specifies how a path and its stroke fit the arranged bounds.</summary>
public enum PathStretch
{
    /// <summary>Preserves the natural size at the local origin.</summary>
    None,
    /// <summary>Scales each axis independently to fill the bounds.</summary>
    Fill,
    /// <summary>Scales uniformly and centers the path within the bounds.</summary>
    Uniform
}
