using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using FreeTypeSharp;
using PanguEngine.Resources;

namespace PanguEngine.Graphics.Text;

/// <summary>
/// Loads, matches, and owns CPU font resources.
/// </summary>
public sealed class FontManager
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly FreeTypeLibrary _freeTypeLibrary = new();
    private readonly Dictionary<string, FontDataBlock> _dataBlocks = new(StringComparer.Ordinal);
    private readonly Dictionary<FaceKey, FontFace> _sourceFaces = [];
    private readonly Dictionary<Font, FontFace> _faces = [];
    private readonly List<Font> _fontOrder = [];
    private readonly List<FontFace> _faceOrder = [];
    private readonly List<NativeFontFace> _nativeFaces = [];
    private readonly Dictionary<Font, FontFace> _matches = [];
    private FontFace? _defaultFace;
    private ulong _nextFontId = 1;
    private bool _destroyed;

    /// <summary>
    /// Initializes an empty font manager.
    /// </summary>
    internal FontManager()
    {
    }

    /// <summary>
    /// Gets or sets the engine default font.
    /// </summary>
    public Font DefaultFont
    {
        get
        {
            VerifyAccess();
            ThrowIfDestroyed();
            return GetDefaultFace().Font;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyAccess();
            ThrowIfDestroyed();
            if (!_faces.TryGetValue(value, out var face))
                throw new ArgumentException("The default font must be registered with this font manager.",
                    nameof(value));
            if (ReferenceEquals(_defaultFace, face))
                return;

            _defaultFace = face;
            _matches.Clear();
        }
    }

    /// <summary>
    /// Gets the registered fonts in registration order.
    /// </summary>
    public IReadOnlyList<Font> Fonts
    {
        get
        {
            VerifyAccess();
            ThrowIfDestroyed();
            return _fontOrder.ToArray();
        }
    }

    /// <summary>
    /// Registers all faces, or one selected face, from a font stream.
    /// </summary>
    /// <param name="stream">The readable font stream.</param>
    /// <param name="faceIndex">The physical collection face index, or <see langword="null"/> for all faces.</param>
    /// <returns>The registered fonts in physical face order, with equal metadata returned once.</returns>
    public IReadOnlyList<Font> Register(Stream stream, int? faceIndex = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        VerifyAccess();
        ThrowIfDestroyed();
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));

        byte[] bytes;
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            if (memory.Length == 0)
                throw new InvalidDataException("The font stream is empty.");
            if (memory.Length > int.MaxValue)
                throw new InvalidDataException("The font stream is too large.");
            bytes = memory.ToArray();
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var isNewBlock = !_dataBlocks.TryGetValue(hash, out var dataBlock);
        dataBlock ??= new FontDataBlock(bytes);
        var createdNativeFaces = new List<NativeFontFace>();
        try
        {
            if (isNewBlock)
                dataBlock.FaceCount = NativeFontFace.GetFaceCount(_freeTypeLibrary, dataBlock);
            if (faceIndex.HasValue && faceIndex.Value >= dataBlock.FaceCount)
                throw new ArgumentOutOfRangeException(nameof(faceIndex));

            var indexes = faceIndex.HasValue
                ? [faceIndex.Value]
                : Enumerable.Range(0, dataBlock.FaceCount).ToArray();
            var stagedSources = new List<(FaceKey Key, FontFace Face)>();
            var stagedFaces = new List<FontFace>();
            var stagedByFont = new Dictionary<Font, FontFace>();
            var results = new List<Font>();
            var resultFonts = new HashSet<Font>();

            foreach (var index in indexes)
            {
                var sourceKey = new FaceKey(hash, index);
                FontFace face;
                if (_sourceFaces.TryGetValue(sourceKey, out var sourceFace))
                {
                    face = sourceFace;
                }
                else
                {
                    var nativeFace = NativeFontFace.Create(_freeTypeLibrary, dataBlock, index);
                    createdNativeFaces.Add(nativeFace);
                    var font = new Font(nativeFace.FamilyName, nativeFace.Weight, nativeFace.Style);
                    if (_faces.TryGetValue(font, out var registeredFace))
                    {
                        face = registeredFace;
                    }
                    else if (stagedByFont.TryGetValue(font, out var stagedFace))
                    {
                        face = stagedFace;
                    }
                    else
                    {
                        face = new FontFace(this, _nextFontId++, font, nativeFace);
                        stagedByFont.Add(font, face);
                        stagedFaces.Add(face);
                    }

                    stagedSources.Add((sourceKey, face));
                }

                if (resultFonts.Add(face.Font))
                    results.Add(face.Font);
            }

            var retainedNativeFaces = stagedFaces
                .Select(face => face.NativeFace)
                .ToHashSet(ReferenceEqualityComparer.Instance);
            for (var i = createdNativeFaces.Count - 1; i >= 0; i--)
            {
                if (!retainedNativeFaces.Contains(createdNativeFaces[i]))
                    createdNativeFaces[i].Destroy();
            }

            if (isNewBlock)
            {
                if (stagedFaces.Count == 0)
                    dataBlock.Destroy();
                else
                    _dataBlocks.Add(hash, dataBlock);
            }

            foreach (var face in stagedFaces)
            {
                _faces.Add(face.Font, face);
                _fontOrder.Add(face.Font);
                _faceOrder.Add(face);
                _nativeFaces.Add(face.NativeFace);
            }

            foreach (var (key, face) in stagedSources)
                _sourceFaces.Add(key, face);
            if (stagedFaces.Count > 0)
                _matches.Clear();

            createdNativeFaces.Clear();
            return results;
        }
        catch
        {
            for (var i = createdNativeFaces.Count - 1; i >= 0; i--)
                createdNativeFaces[i].Destroy();
            if (isNewBlock)
                dataBlock.Destroy();
            throw;
        }
    }

    /// <summary>
    /// Resolves a font request to a loaded physical face.
    /// </summary>
    /// <param name="font">The requested font.</param>
    /// <returns>The closest registered face, or the default face when the family is unavailable.</returns>
    public FontFace Match(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        VerifyAccess();
        ThrowIfDestroyed();
        var defaultFace = GetDefaultFace();
        if (_matches.TryGetValue(font, out var cached))
            return cached;

        FontFace match;
        if (_faces.TryGetValue(font, out var exact))
        {
            match = exact;
        }
        else
        {
            match = _faceOrder
                .Where(face => string.Equals(
                    face.Font.FamilyName,
                    font.FamilyName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(face => GetStyleDistance(face.Font.Style, font.Style))
                .ThenBy(face => Math.Abs((int)face.Font.Weight - (int)font.Weight))
                .ThenBy(face => GetWeightTieBreaker(face.Font.Weight, font.Weight))
                .ThenBy(face => face.Id)
                .FirstOrDefault() ?? defaultFace;
        }

        _matches.Add(font, match);
        return match;
    }

    internal void VerifyFace(FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        VerifyAccess();
        ThrowIfDestroyed();
        if (!ReferenceEquals(face.Owner, this))
            throw new ArgumentException("The font face belongs to a different font manager.", nameof(face));
    }

    internal void VerifyServiceAccess()
    {
        VerifyAccess();
        ThrowIfDestroyed();
    }

    internal GlyphBitmap Rasterize(
        FontFace face,
        uint pixelSize,
        uint glyphId,
        GlyphRasterizationMode mode)
    {
        VerifyFace(face);
        return face.NativeFace.Rasterize(pixelSize, glyphId, mode);
    }

    internal bool Supports(FontFace face, uint scalar)
    {
        VerifyFace(face);
        return face.NativeFace.Supports(scalar);
    }

    internal FontFallbackResult ResolveFallback(
        FontFace preferredFace,
        string text,
        int start,
        int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        VerifyFace(preferredFace);

        if (Covers(preferredFace, text, start, length))
            return new FontFallbackResult(preferredFace, false);
        var defaultFace = GetDefaultFace();
        if (!ReferenceEquals(defaultFace, preferredFace) && Covers(defaultFace, text, start, length))
            return new FontFallbackResult(defaultFace, false);
        return new FontFallbackResult(defaultFace, true);
    }

    internal void Destroy()
    {
        VerifyAccess();
        if (_destroyed)
            return;

        for (var i = _nativeFaces.Count - 1; i >= 0; i--)
            _nativeFaces[i].Destroy();
        foreach (var block in _dataBlocks.Values)
            block.Destroy();
        _freeTypeLibrary.Dispose();
        _destroyed = true;
    }

    private FontFace GetDefaultFace()
    {
        return _defaultFace
               ?? throw new InvalidOperationException("The default font has not been initialized.");
    }

    internal void RegisterResources(ResourceManager resources)
    {
        var sources = resources.Sources;
        var sourcePriorities = new Dictionary<IResourceSource, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < sources.Count; index++)
            sourcePriorities.Add(sources[index], index);

        var fontResources = resources.Namespaces
            .SelectMany(namespaceName => resources.List($"{namespaceName}/fonts", recursive: true))
            .Where(resource => IsFontResource(resource.Path))
            .OrderBy(resource => sourcePriorities[resource.Source])
            .ThenBy(resource => resource.Path, StringComparer.Ordinal);
        foreach (var resource in fontResources)
        {
            using var stream = resource.Open();
            Register(stream);
        }
    }

    private static bool IsFontResource(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".otf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".otc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Covers(FontFace face, string text, int start, int length)
    {
        var span = text.AsSpan(start, length);
        for (var index = 0; index < span.Length;)
        {
            var status = Rune.DecodeFromUtf16(span[index..], out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            index += consumed;

            if (IsDefaultIgnorable(rune.Value))
                continue;
            if (!face.NativeFace.Supports((uint)rune.Value))
                return false;
        }

        return true;
    }

    private static int GetStyleDistance(FontStyle candidate, FontStyle requested)
    {
        if (candidate == requested)
            return 0;
        if (candidate is FontStyle.Italic or FontStyle.Oblique &&
            requested is FontStyle.Italic or FontStyle.Oblique)
            return 1;
        return 2;
    }

    private static int GetWeightTieBreaker(FontWeight candidate, FontWeight requested)
    {
        return (int)requested <= 500 ? (int)candidate : -(int)candidate;
    }

    private static bool IsDefaultIgnorable(int scalar)
    {
        return scalar is 0x00AD or 0x034F or 0x061C or
            >= 0x115F and <= 0x1160 or
            >= 0x17B4 and <= 0x17B5 or
            >= 0x180B and <= 0x180F or
            >= 0x200B and <= 0x200F or
            >= 0x202A and <= 0x202E or
            >= 0x2060 and <= 0x206F or
            0x3164 or
            >= 0xFE00 and <= 0xFE0F or
            0xFEFF or 0xFFA0 or
            >= 0xFFF0 and <= 0xFFF8 or
            >= 0x1BCA0 and <= 0x1BCA3 or
            >= 0x1D173 and <= 0x1D17A or
            >= 0xE0000 and <= 0xE0FFF;
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Font services must be accessed from their owner thread.");
    }

    private void ThrowIfDestroyed()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
    }

    private readonly record struct FaceKey(string Hash, int FaceIndex);
}