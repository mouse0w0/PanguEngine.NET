using System.Text;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Graphics.Text;

public sealed class FontManagerTests : IDisposable
{
    private const string AliasFamily = "Testxx Han Sans CN";
    private readonly List<FontManager> _managers = [];

    [Fact]
    public void FontUsesCaseInsensitiveFamilyValueEquality()
    {
        var first = new Font("Example Sans", FontWeight.Bold, FontStyle.Italic);
        var second = new Font("example sans", FontWeight.Bold, FontStyle.Italic);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, second with { Weight = FontWeight.Normal });
    }

    [Fact]
    public void RegisterLoadsFontAndReleasesInputStreamOwnership()
    {
        var manager = CreateEmptyManager();
        using var stream = OpenSourceHanSans();

        var font = Assert.Single(manager.Register(stream, 0));
        manager.DefaultFont = font;
        var face = manager.Match(font);

        Assert.True(stream.CanRead);
        Assert.Equal("Source Han Sans CN", font.FamilyName);
        Assert.Equal(FontWeight.Normal, font.Weight);
        Assert.Equal(FontStyle.Normal, font.Style);
        Assert.Equal(0, face.NativeFace.FaceIndex);
        Assert.True(manager.Supports(face, 'A'));
        Assert.True(manager.Supports(face, '盘'));
    }

    [Fact]
    public void DefaultFontAndMatchRejectAccessBeforeInitialization()
    {
        var manager = CreateEmptyManager();
        var request = new Font("Source Han Sans CN");

        Assert.Throws<InvalidOperationException>(() => manager.DefaultFont);
        Assert.Throws<InvalidOperationException>(() => manager.Match(request));
    }

    [Fact]
    public void DefaultFontRejectsUnregisteredFontWithoutChangingCurrentValue()
    {
        var manager = CreateManager();
        var current = manager.DefaultFont;

        Assert.Throws<ArgumentException>(() => manager.DefaultFont = new Font("Missing Family"));

        Assert.Same(current, manager.DefaultFont);
    }

    [Fact]
    public void DefaultFontCanBeReplacedWithAnotherRegisteredFont()
    {
        var manager = CreateManager();
        var alias = RegisterAlias(manager, AliasFamily);

        manager.DefaultFont = new Font(AliasFamily);

        Assert.Same(alias, manager.DefaultFont);
        Assert.Same(manager.Match(alias), manager.Match(new Font("Missing Family")));
    }

    [Fact]
    public void RegisterCopiesNonSeekableStreamAndReusesFontAndFaceIdentity()
    {
        var manager = CreateManager();
        using var firstStream = new NonSeekableReadStream(ReadSourceHanSans());
        var first = Assert.Single(manager.Register(firstStream, 0));
        var firstFace = manager.Match(first);
        using var secondStream = OpenSourceHanSans();

        var second = Assert.Single(manager.Register(secondStream, 0));
        var secondFace = manager.Match(second);

        Assert.Same(manager.DefaultFont, first);
        Assert.Same(first, second);
        Assert.Same(firstFace, secondFace);
        Assert.Equal([first], manager.Fonts);
    }

    [Fact]
    public void RegisterUsesFirstFaceWhenDifferentBytesHaveEqualMetadata()
    {
        var manager = CreateEmptyManager();
        using var firstStream = OpenSourceHanSans();
        var first = Assert.Single(manager.Register(firstStream, 0));
        var data = ReadSourceHanSans();
        Array.Resize(ref data, data.Length + 1);
        using var secondStream = new MemoryStream(data, writable: false);

        var second = Assert.Single(manager.Register(secondStream, 0));

        Assert.Same(first, second);
        Assert.Equal([first], manager.Fonts);
    }

    [Fact]
    public void RegisterRejectsEmptyStreamWithoutChangingFonts()
    {
        var manager = CreateManager();
        var before = manager.Fonts.ToArray();
        using var stream = new MemoryStream();

        Assert.Throws<InvalidDataException>(() => manager.Register(stream));

        Assert.Equal(before, manager.Fonts);
    }

    [Fact]
    public void RegisterRejectsOutOfRangeFaceIndex()
    {
        var manager = CreateManager();
        using var stream = OpenSourceHanSans();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => manager.Register(stream, 1));

        Assert.Equal("faceIndex", exception.ParamName);
    }

    [Fact]
    public void MatchInvalidatesDefaultResultWhenRequestedFamilyIsRegistered()
    {
        var manager = CreateManager();

        var defaultMatch = manager.Match(new Font(AliasFamily));
        var resource = RegisterAlias(manager, AliasFamily);
        var first = manager.Match(new Font("testxx han sans cn"));
        var second = manager.Match(new Font("TESTXX HAN SANS CN"));

        Assert.Same(manager.Match(manager.DefaultFont), defaultMatch);
        Assert.Same(resource, first.Font);
        Assert.Same(first, second);
    }

    [Fact]
    public void MatchUsesClosestFaceInRequestedFamily()
    {
        var manager = CreateManager();

        var face = manager.Match(new Font("Source Han Sans CN", FontWeight.Bold, FontStyle.Italic));

        Assert.Same(manager.DefaultFont, face.Font);
    }

    [Fact]
    public void ResolveFallbackKeepsCoveredPreferredResourceFace()
    {
        var manager = CreateManager();
        var resource = RegisterAlias(manager, AliasFamily);
        var resourceFace = manager.Match(resource);

        var result = manager.ResolveFallback(resourceFace, "A", 0, 1);

        Assert.Same(resourceFace, result.FontFace);
        Assert.False(result.IsMissing);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("盘")]
    public void ResolveFallbackKeepsCoveredDefaultFace(string text)
    {
        var manager = CreateManager();
        var defaultFace = manager.Match(manager.DefaultFont);

        var result = manager.ResolveFallback(defaultFace, text, 0, text.Length);

        Assert.Same(defaultFace, result.FontFace);
        Assert.False(result.IsMissing);
    }

    [Fact]
    public void ResolveFallbackMarksUncoveredElementMissing()
    {
        var manager = CreateManager();
        var defaultFace = manager.Match(manager.DefaultFont);

        var result = manager.ResolveFallback(defaultFace, "\U0010FFFF", 0, 2);

        Assert.Same(defaultFace, result.FontFace);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public void ResolveFallbackDoesNotRequireDefaultIgnorableGlyphs()
    {
        var manager = CreateManager();
        var defaultFace = manager.Match(manager.DefaultFont);

        var result = manager.ResolveFallback(defaultFace, "A\u2060", 0, 2);

        Assert.Same(defaultFace, result.FontFace);
        Assert.False(result.IsMissing);
    }

    [Fact]
    public void ResolveFallbackDoesNotIgnoreOtherFormatCharacters()
    {
        var manager = CreateManager();
        var defaultFace = manager.Match(manager.DefaultFont);

        var result = manager.ResolveFallback(defaultFace, "\u0600", 0, 1);

        Assert.Same(defaultFace, result.FontFace);
        Assert.True(result.IsMissing);
    }

    [Fact]
    public void RegisterRejectsAccessFromAnotherThread()
    {
        var manager = CreateManager();
        var data = ReadSourceHanSans();

        Exception? exception = null;
        var thread = new Thread(() => exception = Record.Exception(() =>
        {
            using var stream = new MemoryStream(data, writable: false);
            manager.Register(stream, 0);
        }));
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void DestroyKeepsMetadataReadableButRejectsRegistration()
    {
        var manager = CreateManager();
        var font = manager.DefaultFont;
        var face = manager.Match(font);

        manager.Destroy();
        manager.Destroy();

        Assert.Equal("Source Han Sans CN", font.FamilyName);
        Assert.Equal(font, face.Font);
        Assert.Equal(0, face.NativeFace.FaceIndex);
        using var stream = OpenSourceHanSans();
        Assert.Throws<ObjectDisposedException>(() => manager.Register(stream, 0));
    }

    public void Dispose()
    {
        for (var i = _managers.Count - 1; i >= 0; i--)
            _managers[i].Destroy();
    }

    private FontManager CreateManager()
    {
        var manager = CreateEmptyManager();
        try
        {
            using var stream = OpenSourceHanSans();
            manager.DefaultFont = Assert.Single(manager.Register(stream, 0));
            return manager;
        }
        catch
        {
            manager.Destroy();
            throw;
        }
    }

    private FontManager CreateEmptyManager()
    {
        var manager = new FontManager();
        _managers.Add(manager);
        return manager;
    }

    private static FileStream OpenSourceHanSans()
    {
        return File.OpenRead(Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "SourceHanSansCN-Regular.otf"));
    }

    private static byte[] ReadSourceHanSans()
    {
        using var stream = OpenSourceHanSans();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Font RegisterAlias(FontManager manager, string familyName)
    {
        var data = ReadSourceHanSans();
        ReplaceUtf16BigEndian(data, "Source Han Sans CN", familyName);
        using var stream = new MemoryStream(data, writable: false);
        return Assert.Single(manager.Register(stream, 0));
    }

    private static void ReplaceUtf16BigEndian(byte[] data, string original, string replacement)
    {
        Assert.Equal(original.Length, replacement.Length);
        var source = Encoding.BigEndianUnicode.GetBytes(original);
        var target = Encoding.BigEndianUnicode.GetBytes(replacement);
        var replacements = 0;
        for (var i = 0; i <= data.Length - source.Length; i++)
        {
            if (!data.AsSpan(i, source.Length).SequenceEqual(source))
                continue;
            target.CopyTo(data, i);
            replacements++;
            i += source.Length - 1;
        }

        Assert.NotEqual(0, replacements);
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        internal NonSeekableReadStream(byte[] data)
        {
            _inner = new MemoryStream(data, writable: false);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}