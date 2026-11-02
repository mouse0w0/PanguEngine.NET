using System.Reflection;
using Xunit.Sdk;

namespace PanguEngine.Tests.UI;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class UIThreadTestAttribute(bool initialize = true) : BeforeAfterTestAttribute
{
#if DEBUG
    public const string? DebugOnly = null;
#else
    public const string DebugOnly = "UI thread diagnostics are enabled only in Debug builds.";
#endif

    private TextTestContext? _context;

    public bool Initialize { get; } = initialize;

    public override void Before(MethodInfo methodUnderTest)
    {
        if (Initialize && methodUnderTest.GetCustomAttribute<UIThreadTestAttribute>()?.Initialize != false)
            _context = new TextTestContext();
    }

    public override void After(MethodInfo methodUnderTest)
    {
        _context?.Dispose();
        _context = null;
    }
}
