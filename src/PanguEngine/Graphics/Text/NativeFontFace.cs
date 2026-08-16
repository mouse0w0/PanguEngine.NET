using FreeTypeSharp;
using HarfBuzzSharp;
using HbFace = HarfBuzzSharp.Face;
using HbFont = HarfBuzzSharp.Font;

namespace PanguEngine.Graphics.Text;

internal sealed unsafe class NativeFontFace
{
    private FT_FaceRec_* _freeTypeFace;
    private Blob? _harfBuzzBlob;
    private HbFace? _harfBuzzFace;
    private HbFont? _harfBuzzFont;

    private NativeFontFace(
        FT_FaceRec_* freeTypeFace,
        Blob harfBuzzBlob,
        HbFace harfBuzzFace,
        HbFont harfBuzzFont,
        int faceIndex,
        OpenTypeMetadata metadata)
    {
        _freeTypeFace = freeTypeFace;
        _harfBuzzBlob = harfBuzzBlob;
        _harfBuzzFace = harfBuzzFace;
        _harfBuzzFont = harfBuzzFont;
        FaceIndex = faceIndex;
        FamilyName = metadata.FamilyName;
        Weight = metadata.Weight;
        Style = metadata.Style;
        UnitsPerEm = freeTypeFace->units_per_EM;
        Ascender = freeTypeFace->ascender;
        Descender = -freeTypeFace->descender;
        LineGap = Math.Max(0, freeTypeFace->height - freeTypeFace->ascender + freeTypeFace->descender);
    }

    internal int FaceIndex { get; }
    internal string FamilyName { get; }
    internal FontWeight Weight { get; }
    internal FontStyle Style { get; }
    internal int UnitsPerEm { get; }
    internal int Ascender { get; }
    internal int Descender { get; }
    internal int LineGap { get; }
    internal HbFont HarfBuzzFont => _harfBuzzFont
        ?? throw new ObjectDisposedException(nameof(NativeFontFace));

    internal static int GetFaceCount(FreeTypeLibrary library, FontDataBlock data)
    {
        FT_FaceRec_* probe = null;
        var error = FT.FT_New_Memory_Face(
            library.Native,
            data.Pointer,
            data.Length,
            -1,
            &probe);
        if (error != FT_Error.FT_Err_Ok || probe == null)
            throw new InvalidDataException($"FreeType could not read the font collection ({error}).");

        try
        {
            var count = checked((int)probe->num_faces);
            return count > 0 ? count : throw new InvalidDataException("The font collection contains no faces.");
        }
        finally
        {
            FT.FT_Done_Face(probe);
        }
    }

    internal static NativeFontFace Create(FreeTypeLibrary library, FontDataBlock data, int faceIndex)
    {
        FT_FaceRec_* freeTypeFace = null;
        Blob? blob = null;
        HbFace? harfBuzzFace = null;
        HbFont? harfBuzzFont = null;
        try
        {
            var error = FT.FT_New_Memory_Face(
                library.Native,
                data.Pointer,
                data.Length,
                faceIndex,
                &freeTypeFace);
            if (error != FT_Error.FT_Err_Ok || freeTypeFace == null)
                throw new InvalidDataException($"FreeType could not load face {faceIndex} ({error}).");
            if ((freeTypeFace->face_flags & (nint)FT_FACE_FLAG.FT_FACE_FLAG_SFNT) == 0)
                throw new InvalidDataException($"Font face {faceIndex} is not an SFNT OpenType face.");

            error = FT.FT_Select_Charmap(freeTypeFace, FT_Encoding_.FT_ENCODING_UNICODE);
            if (error != FT_Error.FT_Err_Ok)
                throw new InvalidDataException($"Font face {faceIndex} does not have a Unicode character map.");

            var metadata = OpenTypeMetadata.Read(new ReadOnlySpan<byte>(data.Pointer, data.Length), faceIndex);
            blob = new Blob((IntPtr)data.Pointer, data.Length, MemoryMode.ReadOnly);
            harfBuzzFace = new HbFace(blob, faceIndex);
            harfBuzzFont = new HbFont(harfBuzzFace);
            harfBuzzFont.SetFunctionsOpenType();
            harfBuzzFont.SetScale(harfBuzzFace.UnitsPerEm, harfBuzzFace.UnitsPerEm);
            return new NativeFontFace(
                freeTypeFace,
                blob,
                harfBuzzFace,
                harfBuzzFont,
                faceIndex,
                metadata);
        }
        catch
        {
            harfBuzzFont?.Dispose();
            harfBuzzFace?.Dispose();
            blob?.Dispose();
            if (freeTypeFace != null)
                FT.FT_Done_Face(freeTypeFace);
            throw;
        }
    }

    internal bool Supports(uint scalar)
    {
        ThrowIfDestroyed();
        return FT.FT_Get_Char_Index(_freeTypeFace, (UIntPtr)scalar) != 0;
    }

    internal int GetHorizontalAdvance(uint glyphId)
    {
        ThrowIfDestroyed();
        return HarfBuzzFont.GetHorizontalGlyphAdvance(glyphId);
    }

    internal bool TryGetGlyphExtents(uint glyphId, out GlyphExtents extents)
    {
        ThrowIfDestroyed();
        return HarfBuzzFont.TryGetGlyphExtents(glyphId, out extents);
    }

    internal void Destroy()
    {
        _harfBuzzFont?.Dispose();
        _harfBuzzFont = null;
        _harfBuzzFace?.Dispose();
        _harfBuzzFace = null;
        _harfBuzzBlob?.Dispose();
        _harfBuzzBlob = null;
        if (_freeTypeFace != null)
        {
            FT.FT_Done_Face(_freeTypeFace);
            _freeTypeFace = null;
        }
    }

    private void ThrowIfDestroyed()
    {
        ObjectDisposedException.ThrowIf(_freeTypeFace == null, this);
    }
}
