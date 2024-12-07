using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JimmysUnityUtilities;

namespace LogicUI.FancyTextRendering.MarkdownLogic
{
    internal class MarkdownLine
    {
        public StringBuilder Builder;


        public bool DeleteLineAfterProcessing { get; set; }

        public bool DisableFutureProcessing 
        { 
            get => _DisableFutureProcessing || DeleteLineAfterProcessing; 
            set => _DisableFutureProcessing = value;
        }
        private bool _DisableFutureProcessing;



        // To create vertical whitespace, separating some text from the stuff above or below it, we have an empty line at a specific size.
        // The line must have some contents for TMP to apply the custom size (I think this may be a TMP bug). Therefore the line contains a single zero-width space.
        public void AddVerticalWhitespaceBefore(float spacingFontUnits)
        {
            if (!spacingFontUnits.IsPrettyCloseTo(0))
                Builder.Prepend("<size=" + spacingFontUnits.ToString(CultureInfo.InvariantCulture) + "em>" + SpecialStrings.ZeroWidthSpace + "</size>\n");
        }
        public void AddVerticalWhitespaceAfter(float spacingFontUnits)
        {
            if (!spacingFontUnits.IsPrettyCloseTo(0))
                Builder.Append("\n<size=" + spacingFontUnits.ToString(CultureInfo.InvariantCulture) + "em>" + SpecialStrings.ZeroWidthSpace + "</size>");
        }


        public string Finish()
        {
            if (DeleteLineAfterProcessing)
                throw new Exception("You shouldn't process this line -- it's supposed to be deleted!");

            ReplaceEscapes();
            return Builder.ToString();


            void ReplaceEscapes()
            {
                int countingIndex = 0;
                while (countingIndex < Builder.Length)
                {
                    int index = Builder.IndexOf(EscapeCharacater, countingIndex);

                    if (index < 0)
                        return;

                    // Remove the escape character
                    Builder.Remove(startIndex: index, length: 1);

                    // Skip one chracter. This allows you to escape escapes, i.e. '\\' becomes '\'. Big brain moment
                    countingIndex = index + 1;
                }
            }
        }


        public static readonly char EscapeCharacater = '\\';
        public int UnescapedIndexOf(string find, int startIndex = 0)
        {
            int countingIndex = startIndex;

            while (countingIndex < Builder.Length)
            {
                int index = Builder.IndexOf(find, countingIndex);

                if (index < 0)
                    return -1;

                // Count how many escape characters come before the index. If it's odd, that means the index is escaped.
                // If it's even, that means the escape is escaped, or there are no escapes.
                if (CountPrecedingEscapesCharacters(index).IsOdd())
                {
                    countingIndex = index + find.Length;
                    continue;
                }

                return index;
            }

            return -1;
        }
        public int UnescapedIndexOf(char find, int startIndex = 0)
        {
            int countingIndex = startIndex;

            while (countingIndex < Builder.Length)
            {
                int index = Builder.IndexOf(find, countingIndex);

                if (index < 0)
                    return -1;

                // Count how many escape characters come before the index. If it's odd, that means the index is escaped.
                // If it's even, that means the escape is escaped, or there are no escapes.
                if (CountPrecedingEscapesCharacters(index).IsOdd())
                {
                    countingIndex = index + 1;
                    continue;
                }

                return index;
            }

            return -1;
        }


        int CountPrecedingEscapesCharacters(int index)
        {
            if (index < 0 || index > Builder.Length)
                throw new IndexOutOfRangeException();

            if (index == 0)
                return 0;


            int count = 0;

            for (int i = index; i > 0; i--)
            {
                if (Builder[index - 1] == EscapeCharacater)
                    count++;
                else
                    break;
            }

            return count;
        }
    }
}