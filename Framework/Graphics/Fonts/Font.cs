using StbTrueTypeSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Foster.Framework
{
    /// <summary>
    /// Static preconstructed Charsets
    /// </summary>
    public static class Charsets
    {
        public static string ASCII { get; } = Make(32, 126);

        public static string Make(int from, int to)
        {
            return Make((char)from, (char)to);
        }

        public static string Make(char from, char to)
        {
            Span<char> range = stackalloc char[to - from + 1];

            for (var i = 0; i < range.Length; i++)
            {
                range[i] = (char)(from + i);
            }

            return new string(range);
        }
    }

    /// <summary>
    /// Parses and contains the Data to a single Font
    /// </summary>
    public class Font : IDisposable
    {
        internal StbTrueType.stbtt_fontinfo FontInfo { get; }

        private readonly byte[] _fontBuffer;
        private readonly GCHandle _fontHandle;
        private readonly Dictionary<char, int> _glyphs = new Dictionary<char, int>();

        /// <summary>
        /// The Font Family Name
        /// </summary>
        public string FamilyName { get; }

        /// <summary>
        /// The Font Style Name
        /// </summary>
        public string StyleName { get; }

        /// <summary>
        /// The Font Ascent
        /// </summary>
        public int Ascent { get; }

        /// <summary>
        /// The Font Descent
        /// </summary>
        public int Descent { get; }

        /// <summary>
        /// The Line Gap of the Font. This is the vertical space between lines
        /// </summary>
        public int LineGap { get; }

        /// <summary>
        /// The Height of the Font (Ascent - Descent)
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// The Line Height of the Font (Height + LineGap). This is the total height of a single line, including the line gap
        /// </summary>
        public int LineHeight { get; }

        /// <summary>
        /// Whether the Font has been Disposed
        /// </summary>
        public bool Disposed { get; private set; } = false;

        /// <summary>
        /// Loads a Font from the specified Path
        /// </summary>
        public Font(string path) : this(File.ReadAllBytes(path))
        {

        }

        /// <summary>
        /// Loads a Font from the specified Stream
        /// </summary>
        public Font(Stream stream) : this(ReadAllBytes(stream))
        {

        }

        /// <summary>
        /// Loads a Font from the byte array. The Font will use this buffer until it is disposed.
        /// </summary>
        public unsafe Font(byte[] buffer)
        {
            _fontBuffer = buffer;
            _fontHandle = GCHandle.Alloc(_fontBuffer, GCHandleType.Pinned);
            FontInfo = new StbTrueType.stbtt_fontinfo();

            StbTrueType.stbtt_InitFont(FontInfo, (byte*)(_fontHandle.AddrOfPinnedObject().ToPointer()), 0);

            FamilyName = GetName(FontInfo, 1);
            StyleName = GetName(FontInfo, 2);

            // properties
            int ascent, descent, linegap;
            StbTrueType.stbtt_GetFontVMetrics(FontInfo, &ascent, &descent, &linegap);
            Ascent = ascent;
            Descent = descent;
            LineGap = linegap;
            Height = Ascent - Descent;
            LineHeight = Height + LineGap;

            static unsafe string GetName(StbTrueType.stbtt_fontinfo fontInfo, int nameID)
            {
                int length = 0;

                sbyte* ptr = StbTrueType.stbtt_GetFontNameString(fontInfo, &length,
                    StbTrueType.STBTT_PLATFORM_ID_MICROSOFT,
                    StbTrueType.STBTT_MS_EID_UNICODE_BMP,
                    StbTrueType.STBTT_MS_LANG_ENGLISH,
                    nameID);

                if (length > 0)
                {
                    return new string(ptr, 0, length, Encoding.BigEndianUnicode);
                }

                return "Unknown";
            }
        }

        ~Font()
        {
            Dispose();
        }

        /// <summary>
        /// Gets the Scale of the Font for a given Height. This value can then be used to scale proprties of a Font for the given Height
        /// </summary>
        public float GetScale(int height)
        {
            if (Disposed)
            {
                throw new Exception("Cannot get Font data as it is disposed");
            }

            return StbTrueType.stbtt_ScaleForPixelHeight(FontInfo, height);
        }

        /// <summary>
        /// Gets the Glyph code for a given Unicode value, if it exists, or 0 otherwise
        /// </summary>
        public int GetGlyph(char unicode)
        {
            if (!_glyphs.TryGetValue(unicode, out var glyph))
            {
                if (Disposed)
                {
                    throw new Exception("Cannot get Font data as it is disposed");
                }

                glyph = StbTrueType.stbtt_FindGlyphIndex(FontInfo, unicode);
                _glyphs[unicode] = glyph;
            }

            return glyph;
        }

        /// <summary>
        /// Disposes the Font and all its resources
        /// </summary>
        public void Dispose()
        {
            Disposed = true;

            if (_fontHandle.IsAllocated)
            {
                _fontHandle.Free();
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }
    }
}
