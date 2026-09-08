using System;
using System.Text;

namespace AS400PADCustomAction.Core.Tn5250
{
    /// <summary>
    /// Bidirectional codec for IBM EBCDIC Code Page 037 (US-Canada) and ASCII/Unicode.
    /// Provides fast, thread-safe character conversion for 5250 streams.
    /// </summary>
    public static class EbcdicCodec
    {
        private static readonly Encoding EbcdicEncoding;
        private static readonly char[] EbcdicToUnicodeTable = new char[256];
        private static readonly byte[] UnicodeToEbcdicTable = new byte[256];

        static EbcdicCodec()
        {
            try
            {
                EbcdicEncoding = Encoding.GetEncoding(37);
            }
            catch
            {
                EbcdicEncoding = null;
            }

            // Build deterministic fallback lookup tables for IBM EBCDIC CP037
            for (int i = 0; i < 256; i++)
            {
                EbcdicToUnicodeTable[i] = ' ';
                UnicodeToEbcdicTable[i] = 0x40; // Default to EBCDIC space
            }

            // Initialize control and special characters
            EbcdicToUnicodeTable[0x00] = '\0';
            EbcdicToUnicodeTable[0x40] = ' ';

            // Punctuation & symbols
            EbcdicToUnicodeTable[0x4B] = '.';
            EbcdicToUnicodeTable[0x4C] = '<';
            EbcdicToUnicodeTable[0x4D] = '(';
            EbcdicToUnicodeTable[0x4E] = '+';
            EbcdicToUnicodeTable[0x4F] = '|';
            EbcdicToUnicodeTable[0x50] = '&';
            EbcdicToUnicodeTable[0x5A] = '!';
            EbcdicToUnicodeTable[0x5B] = '$';
            EbcdicToUnicodeTable[0x5C] = '*';
            EbcdicToUnicodeTable[0x5D] = ')';
            EbcdicToUnicodeTable[0x5E] = ';';
            EbcdicToUnicodeTable[0x5F] = '^';
            EbcdicToUnicodeTable[0x60] = '-';
            EbcdicToUnicodeTable[0x61] = '/';
            EbcdicToUnicodeTable[0x6B] = ',';
            EbcdicToUnicodeTable[0x6C] = '%';
            EbcdicToUnicodeTable[0x6D] = '_';
            EbcdicToUnicodeTable[0x6E] = '>';
            EbcdicToUnicodeTable[0x6F] = '?';
            EbcdicToUnicodeTable[0x79] = '`';
            EbcdicToUnicodeTable[0x7A] = ':';
            EbcdicToUnicodeTable[0x7B] = '#';
            EbcdicToUnicodeTable[0x7C] = '@';
            EbcdicToUnicodeTable[0x7D] = '\'';
            EbcdicToUnicodeTable[0x7E] = '=';
            EbcdicToUnicodeTable[0x7F] = '"';

            // Lowercase a-i (0x81-0x89)
            for (int i = 0; i < 9; i++)
            {
                EbcdicToUnicodeTable[0x81 + i] = (char)('a' + i);
            }
            // Lowercase j-r (0x91-0x99)
            for (int i = 0; i < 9; i++)
            {
                EbcdicToUnicodeTable[0x91 + i] = (char)('j' + i);
            }
            // Lowercase s-z (0xA2-0xA9)
            for (int i = 0; i < 8; i++)
            {
                EbcdicToUnicodeTable[0xA2 + i] = (char)('s' + i);
            }

            // Uppercase A-I (0xC1-0xC9)
            for (int i = 0; i < 9; i++)
            {
                EbcdicToUnicodeTable[0xC1 + i] = (char)('A' + i);
            }
            // Uppercase J-R (0xD1-0xD9)
            for (int i = 0; i < 9; i++)
            {
                EbcdicToUnicodeTable[0xD1 + i] = (char)('J' + i);
            }
            // Uppercase S-Z (0xE2-0xE9)
            for (int i = 0; i < 8; i++)
            {
                EbcdicToUnicodeTable[0xE2 + i] = (char)('S' + i);
            }

            // Digits 0-9 (0xF0-0xF9)
            for (int i = 0; i < 10; i++)
            {
                EbcdicToUnicodeTable[0xF0 + i] = (char)('0' + i);
            }

            // Populate reverse table
            for (int i = 0; i < 256; i++)
            {
                char c = EbcdicToUnicodeTable[i];
                if (c < 256 && c != ' ')
                {
                    UnicodeToEbcdicTable[(byte)c] = (byte)i;
                }
            }
            UnicodeToEbcdicTable[' '] = 0x40;
            UnicodeToEbcdicTable['\0'] = 0x00;

            // If system encoding is available, sync complete mapping
            if (EbcdicEncoding != null)
            {
                byte[] allBytes = new byte[256];
                for (int i = 0; i < 256; i++) allBytes[i] = (byte)i;
                char[] chars = EbcdicEncoding.GetChars(allBytes);
                for (int i = 0; i < 256; i++)
                {
                    EbcdicToUnicodeTable[i] = chars[i];
                    if (chars[i] < 256)
                    {
                        UnicodeToEbcdicTable[(byte)chars[i]] = (byte)i;
                    }
                }
            }
        }

        /// <summary>
        /// Converts an EBCDIC byte to Unicode character.
        /// </summary>
        public static char ToChar(byte ebcdicByte)
        {
            return EbcdicToUnicodeTable[ebcdicByte];
        }

        /// <summary>
        /// Converts an array of EBCDIC bytes to a Unicode string.
        /// </summary>
        public static string ToString(byte[] bytes, int offset, int length)
        {
            if (bytes == null || length <= 0 || offset < 0 || offset + length > bytes.Length)
                return string.Empty;

            if (EbcdicEncoding != null)
            {
                return EbcdicEncoding.GetString(bytes, offset, length);
            }

            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = EbcdicToUnicodeTable[bytes[offset + i]];
            }
            return new string(chars);
        }

        /// <summary>
        /// Converts a Unicode character to an EBCDIC CP037 byte.
        /// </summary>
        public static byte ToEbcdic(char c)
        {
            if (c < 256)
            {
                return UnicodeToEbcdicTable[(byte)c];
            }
            return 0x40; // Default space
        }

        /// <summary>
        /// Converts a Unicode string into an array of EBCDIC CP037 bytes.
        /// </summary>
        public static byte[] ToEbcdicBytes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new byte[0];

            if (EbcdicEncoding != null)
            {
                return EbcdicEncoding.GetBytes(text);
            }

            byte[] result = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                result[i] = ToEbcdic(text[i]);
            }
            return result;
        }
    }
}
