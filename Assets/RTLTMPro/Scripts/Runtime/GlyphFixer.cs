using System.Collections.Generic;

namespace RTLTMPro
{
    public static class GlyphFixer
    {
        public static Dictionary<char, char> EnglishToFarsiNumberMap = new Dictionary<char, char>()
        {
            [(char)EnglishNumbers.Zero] = (char)FarsiNumbers.Zero,
            [(char)EnglishNumbers.One] = (char)FarsiNumbers.One,
            [(char)EnglishNumbers.Two] = (char)FarsiNumbers.Two,
            [(char)EnglishNumbers.Three] = (char)FarsiNumbers.Three,
            [(char)EnglishNumbers.Four] = (char)FarsiNumbers.Four,
            [(char)EnglishNumbers.Five] = (char)FarsiNumbers.Five,
            [(char)EnglishNumbers.Six] = (char)FarsiNumbers.Six,
            [(char)EnglishNumbers.Seven] = (char)FarsiNumbers.Seven,
            [(char)EnglishNumbers.Eight] = (char)FarsiNumbers.Eight,
            [(char)EnglishNumbers.Nine] = (char)FarsiNumbers.Nine,
        };

        public static Dictionary<char, char> EnglishToHinduNumberMap = new Dictionary<char, char>()
        {
            [(char)EnglishNumbers.Zero] = (char)HinduNumbers.Zero,
            [(char)EnglishNumbers.One] = (char)HinduNumbers.One,
            [(char)EnglishNumbers.Two] = (char)HinduNumbers.Two,
            [(char)EnglishNumbers.Three] = (char)HinduNumbers.Three,
            [(char)EnglishNumbers.Four] = (char)HinduNumbers.Four,
            [(char)EnglishNumbers.Five] = (char)HinduNumbers.Five,
            [(char)EnglishNumbers.Six] = (char)HinduNumbers.Six,
            [(char)EnglishNumbers.Seven] = (char)HinduNumbers.Seven,
            [(char)EnglishNumbers.Eight] = (char)HinduNumbers.Eight,
            [(char)EnglishNumbers.Nine] = (char)HinduNumbers.Nine,
        };


        /// <summary>
        ///     Fixes the shape of letters based on their position.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="preserveNumbers"></param>
        /// <param name="farsi"></param>
        /// <returns></returns>
        public static void Fix(FastStringBuilder input, FastStringBuilder output, bool preserveNumbers, bool farsi, bool fixTextTags)
        {
            FixYah(input, farsi);

            output.SetValue(input);

            for (int i = 0; i < input.Length; i++)
            {
                bool skipNext = false;
                int iChar = input.Get(i);

                // For special Lam Letter connections.
                if (iChar == (int)ArabicGeneralLetters.Lam)
                {
                    if (i < input.Length - 1)
                    {
                        skipNext = HandleSpecialLam(input, output, i);
                        if (skipNext)
                            iChar = output.Get(i);
                    }
                }

                // We don't want to fix tatweel or zwnj character
                if (iChar == (int)ArabicGeneralLetters.Tatweel ||
                    iChar == (int)SpecialCharacters.ZeroWidthNoJoiner)
                {
                    continue;
                }

                if (iChar < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)iChar))
                {
                    char converted = GlyphTable.Convert((char)iChar);

                    if (IsMiddleLetter(input, i))
                    {
                        output.Set(i, (char)(converted + 3));
                    } else if (IsFinishingLetter(input, i))
                    {
                        output.Set(i, (char)(converted + 1));
                    } else if (IsLeadingLetter(input, i))
                    {
                        output.Set(i, (char)(converted + 2));
                    } else
                    {
                        output.Set(i, (char)converted);
                    }
                }

                // If this letter as Lam and special Lam-Alef connection was made, We want to skip the Alef
                // (Lam-Alef occupies 1 space)
                if (skipNext)
                {
                    i++;
                }
            }

            if (!preserveNumbers)
            {
                if (fixTextTags)
                {
                    FixNumbersOutsideOfTags(output, farsi);
                } else
                {
                    FixNumbers(output, farsi);
                }
            }
        }

        /// <summary>
        ///     Removes tashkeel. Converts general RTL letters to isolated form. Also fixes Farsi and Arabic ی letter.
        /// </summary>
        /// <param name="text">Input to prepare</param>
        /// <param name="farsi"></param>
        /// <returns>Prepared input in char array</returns>
        public static void FixYah(FastStringBuilder text, bool farsi)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (farsi && text.Get(i) == (int)ArabicGeneralLetters.Yeh)
                {
                    text.Set(i, (char)ArabicGeneralLetters.FarsiYeh);
                } else if (farsi == false && text.Get(i) == (int)ArabicGeneralLetters.FarsiYeh)
                {
                    text.Set(i, (char)ArabicGeneralLetters.Yeh);
                }
            }
        }

        /// <summary>
        ///     Handles the special Lam-Alef connection in the text.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="i">Index of Lam letter</param>
        /// <returns><see langword="true" /> if special connection has been made.</returns>
        private static bool HandleSpecialLam(FastStringBuilder input, FastStringBuilder output, int i)
        {
            bool isFixed;
            switch (input.Get(i + 1))
            {
                case (char)ArabicGeneralLetters.AlefHamzaBelow:
                    output.Set(i, (char)0xFEF7);
                    isFixed = true;
                    break;
                case (char)ArabicGeneralLetters.Alef:
                    output.Set(i, (char)0xFEF9);
                    isFixed = true;
                    break;
                case (char)ArabicGeneralLetters.AlefHamzaAbove:
                    output.Set(i, (char)0xFEF5);
                    isFixed = true;
                    break;
                case (char)ArabicGeneralLetters.AlefMaddaAbove:
                    output.Set(i, (char)0xFEF3);
                    isFixed = true;
                    break;
                default:
                    isFixed = false;
                    break;
            }

            if (isFixed)
            {
                output.Set(i + 1, (char)0xFFFF);
            }

            return isFixed;
        }

        /// <summary>
        ///     Converts English numbers to Persian or Arabic numbers.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="farsi"></param>
        /// <returns>Converted number</returns>
        public static void FixNumbers(FastStringBuilder text, bool farsi)
        {
            text.Replace((char)EnglishNumbers.Zero, farsi ? (char)FarsiNumbers.Zero : (char)HinduNumbers.Zero);
            text.Replace((char)EnglishNumbers.One, farsi ? (char)FarsiNumbers.One : (char)HinduNumbers.One);
            text.Replace((char)EnglishNumbers.Two, farsi ? (char)FarsiNumbers.Two : (char)HinduNumbers.Two);
            text.Replace((char)EnglishNumbers.Three, farsi ? (char)FarsiNumbers.Three : (char)HinduNumbers.Three);
            text.Replace((char)EnglishNumbers.Four, farsi ? (char)FarsiNumbers.Four : (char)HinduNumbers.Four);
            text.Replace((char)EnglishNumbers.Five, farsi ? (char)FarsiNumbers.Five : (char)HinduNumbers.Five);
            text.Replace((char)EnglishNumbers.Six, farsi ? (char)FarsiNumbers.Six : (char)HinduNumbers.Six);
            text.Replace((char)EnglishNumbers.Seven, farsi ? (char)FarsiNumbers.Seven : (char)HinduNumbers.Seven);
            text.Replace((char)EnglishNumbers.Eight, farsi ? (char)FarsiNumbers.Eight : (char)HinduNumbers.Eight);
            text.Replace((char)EnglishNumbers.Nine, farsi ? (char)FarsiNumbers.Nine : (char)HinduNumbers.Nine);
        }

        /// <summary>
        ///     Converts English numbers that are outside tags to Persian or Arabic numbers.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="farsi"></param>
        /// <returns>Text with converted numbers</returns>
        public static void FixNumbersOutsideOfTags(FastStringBuilder text, bool farsi)
        {
            var englishDigits = new HashSet<char>(EnglishToFarsiNumberMap.Keys);
            for (int i = 0; i < text.Length; i++)
            {
                var iChar = text.Get(i);
                // skip valid tags
                if (iChar == '<')
                {
                    bool sawValidTag = false;
                    for (int j = i + 1; j < text.Length; j++)
                    {
                        int jChar = text.Get(j);
                        if ((j == i + 1 && jChar == ' ') || jChar == '<')
                        {
                            break;
                        } else if (jChar == '>')
                        {
                            i = j;
                            sawValidTag = true;
                            break;
                        }
                    }

                    if (sawValidTag) continue;
                }

                if (englishDigits.Contains((char)iChar))
                {
                    text.Set(i, farsi ? EnglishToFarsiNumberMap[(char)iChar] : EnglishToHinduNumberMap[(char)iChar]);
                }
            }
        }

        /// <summary>
        ///     Is the letter at provided index a leading letter?
        /// </summary>
        /// <returns><see langword="true" /> if the letter is a leading letter</returns>
        private static bool IsLeadingLetter(FastStringBuilder letters, int index)
        {
            var currentIndexLetter = letters.Get(index);

            int previousIndexLetter = default;
            if (index != 0)
                previousIndexLetter = letters.Get(index - 1);

            int nextIndexLetter = default;
            if (index < letters.Length - 1)
                nextIndexLetter = letters.Get(index + 1);

            bool isPreviousLetterNonConnectable = index == 0 ||
                                                  (previousIndexLetter < 0xFFFF && !TextUtils.IsGlyphFixedArabicCharacter((char)previousIndexLetter)) ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Hamza ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefMaddaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefHamzaBelow ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.WawHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Alef ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Dal ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Thal ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Reh ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Zain ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Jeh ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Waw ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefMaddaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefHamzaBelow ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.WawHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Alef ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Hamza ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Dal ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Thal ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Reh ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Zain ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Jeh ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Waw ||
                                                  previousIndexLetter == (int)SpecialCharacters.ZeroWidthNoJoiner;


            bool canThisLetterBeLeading = currentIndexLetter != ' ' &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                          currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;
                                          

            bool isNextLetterConnectable = index < letters.Length - 1 &&
                                           (nextIndexLetter < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)nextIndexLetter)) &&
                                           nextIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                           nextIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;

            return isPreviousLetterNonConnectable &&
                   canThisLetterBeLeading &&
                   isNextLetterConnectable;
        }

        /// <summary>
        ///     Is the letter at provided index a finishing letter?
        /// </summary>
        /// <returns><see langword="true" /> if the letter is a finishing letter</returns>
        private static bool IsFinishingLetter(FastStringBuilder letters, int index)
        {
            int currentIndexLetter = letters.Get(index);

            int previousIndexLetter = default;
            if (index != 0)
                previousIndexLetter = letters.Get(index - 1);

            bool isPreviousLetterConnectable = index != 0 &&
                                               previousIndexLetter != ' ' &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Hamza &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefMaddaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaBelow &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.WawHamzaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Alef &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Dal &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Thal &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Reh &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Zain &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Jeh &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Waw &&
                                               previousIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                               (previousIndexLetter < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)previousIndexLetter));


            bool canThisLetterBeFinishing = currentIndexLetter != ' ' &&
                                            currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                            currentIndexLetter != (int)ArabicGeneralLetters.Hamza;

            return isPreviousLetterConnectable && canThisLetterBeFinishing;
        }

        /// <summary>
        ///     Is the letter at provided index a middle letter?
        /// </summary>
        /// <returns><see langword="true" /> if the letter is a middle letter</returns>
        private static bool IsMiddleLetter(FastStringBuilder letters, int index)
        {
            var currentIndexLetter = letters.Get(index);

            int previousIndexLetter = default;
            if (index != 0)
                previousIndexLetter = letters.Get(index - 1);

            int nextIndexLetter = default;
            if (index < letters.Length - 1)
                nextIndexLetter = letters.Get(index + 1);

            bool middleLetterCheck = index != 0 &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                     currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;

            bool previousLetterCheck = index != 0 &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Hamza &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefMaddaAbove &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaAbove &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaBelow &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.WawHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Alef &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Dal &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Thal &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Reh &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Zain &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Jeh &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Waw &&
                                       previousIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                       (previousIndexLetter < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)previousIndexLetter));

            bool nextLetterCheck = index < letters.Length - 1 &&
                                   (nextIndexLetter < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)nextIndexLetter)) &&
                                   nextIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                   nextIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                   nextIndexLetter != (int)ArabicIsolatedLetters.Hamza;

            return nextLetterCheck && previousLetterCheck && middleLetterCheck;
        }
    }
}