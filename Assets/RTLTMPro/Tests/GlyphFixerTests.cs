using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RTLTMPro.Tests
{
    public class GlyphFixerTests
    {
        private static FastStringBuilder GetEnglishNumbers()
        {
            var text = new FastStringBuilder(10);
            var englishNumbers = Enum.GetValues(typeof(EnglishNumbers));
            foreach (int englishNumber in englishNumbers)
                text.Append((char) englishNumber);
            return text;
        }

        private static FastStringBuilder GetFarsiNumbers()
        {
            var text = new FastStringBuilder(10);
            var farsiNumbers = Enum.GetValues(typeof(FarsiNumbers));
            foreach (int farsiNumber in farsiNumbers)
                text.Append((char) farsiNumber);
            return text;
        }

        private static FastStringBuilder GetHinduNumbers()
        {
            var text = new FastStringBuilder(10);
            var hinduNumbers = Enum.GetValues(typeof(HinduNumbers));
            foreach (int hinduNumber in hinduNumbers)
                text.Append((char) hinduNumber);
            return text;
        }

        [Test]
        public void ConvertNumbers_ConvertsEnglishNumbersToFarsi_WhenFarsiIsTrue()
        {
            var text = GetEnglishNumbers();
            var expected = GetFarsiNumbers();

            GlyphFixer.FixNumbers(text, true);

            Assert.AreEqual(expected.ToString(), text.ToString());
        }

        [Test]
        public void ConvertNumbers_ConvertsEnglishNumbersToHindu_WhenFarsiIsFalse()
        {
            var text = GetEnglishNumbers();
            var expected = GetHinduNumbers();

            GlyphFixer.FixNumbers(text, false);

            Assert.AreEqual(expected.ToString(), text.ToString());
        }

        [Test]
        public void GlyphFixer_ConvertsNumbers_Farsi()
        {
            var text = GetEnglishNumbers();
            var output = new FastStringBuilder(10);

            GlyphFixer.Fix(text, output, false, true, false);

            Assert.AreEqual(GetFarsiNumbers().ToString(), output.ToString());
        }

        [Test]
        public void GlyphFixer_ConvertsNumbers_Hindu()
        {
            var text = GetEnglishNumbers();
            var output = new FastStringBuilder(10);

            GlyphFixer.Fix(text, output, false, false, false);

            Assert.AreEqual(GetHinduNumbers().ToString(), output.ToString());
        }

        [Test]
        public void GlyphFixer_PreservesNumbers_WhenPreserveNumberIsTrue()
        {
            var text = GetEnglishNumbers();
            var output = new FastStringBuilder(10);

            GlyphFixer.Fix(text, output, true, false, false);

            Assert.AreEqual(text.ToString(), output.ToString());
        }

        [Test]
        public void GlyphFixer_FixesYah_WhenFarsiIsRequired()
        {
            var text = new FastStringBuilder(10);
            text.Append((char) ArabicGeneralLetters.Yeh);

            GlyphFixer.FixYah(text, true);

            Assert.AreEqual(((char) ArabicGeneralLetters.FarsiYeh).ToString(), text.ToString());
        }
    }
}