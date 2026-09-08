using System;
using System.Text;

namespace AS400PADCustomAction.Core.Tn5250
{
    /// <summary>
    /// Represents the in-memory 24x80 virtual presentation space for an IBM 5250 terminal screen.
    /// Thread-safe for concurrent read and write operations.
    /// </summary>
    public class Tn5250ScreenBuffer
    {
        public const int Rows = Tn5250Constants.SCREEN_ROWS;
        public const int Columns = Tn5250Constants.SCREEN_COLS;
        public const int TotalSize = Tn5250Constants.SCREEN_SIZE;

        private readonly char[] _cells = new char[TotalSize];
        private readonly byte[] _attributes = new byte[TotalSize];
        private readonly object _lock = new object();

        private int _cursorRow = 1;
        private int _cursorCol = 1;

        public int CursorRow
        {
            get { lock (_lock) { return _cursorRow; } }
            set { lock (_lock) { _cursorRow = Math.Max(1, Math.Min(Rows, value)); } }
        }

        public int CursorCol
        {
            get { lock (_lock) { return _cursorCol; } }
            set { lock (_lock) { _cursorCol = Math.Max(1, Math.Min(Columns, value)); } }
        }

        public Tn5250ScreenBuffer()
        {
            Clear();
        }

        /// <summary>
        /// Clears the screen buffer by filling all cells with spaces and resets the cursor to (1, 1).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < TotalSize; i++)
                {
                    _cells[i] = ' ';
                    _attributes[i] = 0;
                }
                _cursorRow = 1;
                _cursorCol = 1;
            }
        }

        /// <summary>
        /// Sets cursor to 1-based (row, col) coordinates with validation.
        /// </summary>
        public void SetCursor(int row, int col)
        {
            if (row < 1 || row > Rows)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"Row '{row}' is out of range. Must be between 1 and {Rows}.");
            if (col < 1 || col > Columns)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"Column '{col}' is out of range. Must be between 1 and {Columns}.");

            lock (_lock)
            {
                _cursorRow = row;
                _cursorCol = col;
            }
        }

        /// <summary>
        /// Retrieves the current cursor coordinates as a Tuple of (Row, Col).
        /// </summary>
        public Tuple<int, int> GetCursor()
        {
            lock (_lock)
            {
                return Tuple.Create(_cursorRow, _cursorCol);
            }
        }

        /// <summary>
        /// Converts 1-based row and column to a 0-based linear index.
        /// </summary>
        public static int ToIndex(int row, int col)
        {
            return ((row - 1) * Columns) + (col - 1);
        }

        /// <summary>
        /// Converts 0-based linear index to 1-based (row, col).
        /// </summary>
        public static void FromIndex(int index, out int row, out int col)
        {
            row = (index / Columns) + 1;
            col = (index % Columns) + 1;
        }

        /// <summary>
        /// Writes a character at the current cursor position and advances the cursor.
        /// </summary>
        public void WriteCharAndAdvance(char c, byte attribute = 0)
        {
            lock (_lock)
            {
                int index = ToIndex(_cursorRow, _cursorCol);
                if (index >= 0 && index < TotalSize)
                {
                    _cells[index] = c;
                    _attributes[index] = attribute;
                }

                // Advance cursor
                index++;
                if (index >= TotalSize)
                {
                    index = 0; // Wrap around to top
                }
                FromIndex(index, out _cursorRow, out _cursorCol);
            }
        }

        /// <summary>
        /// Writes a string at the current cursor position, advancing the cursor per character.
        /// </summary>
        public void WriteString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            lock (_lock)
            {
                foreach (char c in text)
                {
                    WriteCharAndAdvance(c);
                }
            }
        }

        /// <summary>
        /// Writes text starting at specific (row, col) coordinates without sending an AID key.
        /// </summary>
        public void WriteAt(int row, int col, string text)
        {
            SetCursor(row, col);
            WriteString(text);
        }

        /// <summary>
        /// Blanks out a specified length of characters with spaces starting at (row, col).
        /// </summary>
        public void EraseAt(int row, int col, int length)
        {
            if (length <= 0) return;
            SetCursor(row, col);

            lock (_lock)
            {
                int startIndex = ToIndex(row, col);
                for (int i = 0; i < length && (startIndex + i) < TotalSize; i++)
                {
                    _cells[startIndex + i] = ' ';
                }
            }
        }

        /// <summary>
        /// Implements the 5250 Repeat-to-Address (RA) order.
        /// Fills characters from current cursor location up to (targetRow, targetCol).
        /// </summary>
        public void RepeatToAddress(int targetRow, int targetCol, char fillChar)
        {
            lock (_lock)
            {
                int startIndex = ToIndex(_cursorRow, _cursorCol);
                int targetIndex = ToIndex(Math.Max(1, Math.Min(Rows, targetRow)), Math.Max(1, Math.Min(Columns, targetCol)));

                if (targetIndex < startIndex)
                {
                    // Wraps around end of buffer
                    for (int i = startIndex; i < TotalSize; i++) _cells[i] = fillChar;
                    for (int i = 0; i <= targetIndex; i++) _cells[i] = fillChar;
                }
                else
                {
                    for (int i = startIndex; i <= targetIndex; i++) _cells[i] = fillChar;
                }

                int next = (targetIndex + 1) % TotalSize;
                FromIndex(next, out _cursorRow, out _cursorCol);
            }
        }

        /// <summary>
        /// Reads a contiguous linear slice of characters from the presentation space.
        /// </summary>
        public string ReadSlice(int startRow, int startCol, int length)
        {
            if (startRow < 1 || startRow > Rows)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"StartRow '{startRow}' is out of range. Must be between 1 and {Rows}.");
            if (startCol < 1 || startCol > Columns)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"StartCol '{startCol}' is out of range. Must be between 1 and {Columns}.");
            if (length <= 0)
                return string.Empty;

            lock (_lock)
            {
                int startIndex = ToIndex(startRow, startCol);
                int actualLength = Math.Min(length, TotalSize - startIndex);
                if (actualLength <= 0) return string.Empty;

                return new string(_cells, startIndex, actualLength);
            }
        }

        /// <summary>
        /// Reads a rectangular region defined by (startRow, startCol) to (endRow, endCol).
        /// </summary>
        public string ReadBox(int startRow, int startCol, int endRow, int endCol)
        {
            if (startRow < 1 || startRow > Rows)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"StartRow '{startRow}' is out of range. Must be between 1 and {Rows}.");
            if (startCol < 1 || startCol > Columns)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"StartCol '{startCol}' is out of range. Must be between 1 and {Columns}.");
            if (endRow < startRow || endRow > Rows)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"EndRow '{endRow}' is invalid. Must be between {startRow} and {Rows}.");
            if (endCol < startCol || endCol > Columns)
                throw new AS400Exception(AS400ErrorCode.InvalidCoordinate, $"EndCol '{endCol}' is invalid. Must be between {startCol} and {Columns}.");

            lock (_lock)
            {
                StringBuilder sb = new StringBuilder();
                for (int r = startRow; r <= endRow; r++)
                {
                    int rowStart = ToIndex(r, startCol);
                    int width = (endCol - startCol) + 1;
                    sb.Append(new string(_cells, rowStart, width));
                    if (r < endRow)
                    {
                        sb.AppendLine();
                    }
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Searches for text across the presentation space starting at (startRow, startCol).
        /// Returns true if found along with output 1-based (foundRow, foundCol).
        /// </summary>
        public bool FindText(string searchText, bool caseSensitive, out int foundRow, out int foundCol, int startRow = 1, int startCol = 1)
        {
            foundRow = 0;
            foundCol = 0;
            if (string.IsNullOrEmpty(searchText)) return false;

            lock (_lock)
            {
                string fullScreen = new string(_cells);
                int startIndex = ToIndex(Math.Max(1, Math.Min(Rows, startRow)), Math.Max(1, Math.Min(Columns, startCol)));

                StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int matchIndex = fullScreen.IndexOf(searchText, startIndex, comparison);

                if (matchIndex >= 0)
                {
                    FromIndex(matchIndex, out foundRow, out foundCol);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns the full 24x80 presentation space as a formatted multi-line string.
        /// </summary>
        public string GetFullPresentationSpace()
        {
            lock (_lock)
            {
                StringBuilder sb = new StringBuilder(TotalSize + (Rows * 2));
                for (int r = 1; r <= Rows; r++)
                {
                    int rowStart = ToIndex(r, 1);
                    sb.AppendLine(new string(_cells, rowStart, Columns));
                }
                return sb.ToString();
            }
        }
    }
}
