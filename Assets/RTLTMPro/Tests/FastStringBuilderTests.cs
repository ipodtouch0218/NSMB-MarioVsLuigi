using System;
using NUnit.Framework;

// ReSharper disable StringLiteralTypo

namespace RTLTMPro.Tests
{
    public class FastFastStringBuilderTests
    {
        [Test]
        public void SetValue_SetsTheValueOfStringBuilderWithProvidedText()
        {
            var builder = new FastStringBuilder(10);
            
            builder.SetValue("abcd");
            
            Assert.AreEqual("abcd", builder.ToString());
        }
        
        [Test]
        public void SetValue_CanCopyValueOfAnotherStringBuilder()
        {
            var builder1 = new FastStringBuilder("my holy moly text");
            var builder2 = new FastStringBuilder(10);
            
            builder2.SetValue(builder1);
            
            Assert.AreEqual(builder1.ToString(), builder2.ToString());
        }
        
        [Test]
        public void SetValue_MakesIndependentCopies()
        {
            var builder1 = new FastStringBuilder("my holy moly text");
            var builder2 = new FastStringBuilder(10);
            
            builder2.SetValue(builder1);
            
            builder1.Set(5, 'f');
            
            Assert.AreNotEqual(builder1.ToString(), builder2.ToString());
        }

        [Test]
        public void SetValue_IncreasesCapacity_AsNeeded()
        {
            var builder = new FastStringBuilder(0);
            
            builder.SetValue("abcd");
            
            Assert.AreEqual("abcd", builder.ToString());
        }

        [Test]
        public void Append_AddsACharacter()
        {
            var builder = new FastStringBuilder(10);
            
            builder.Append('a');
            builder.Append('b');
            builder.Append('c');
            
            Assert.AreEqual("abc", builder.ToString());
        }
        
        [Test]
        public void Remove_RemovesAllOccurrencesOfCharacter()
        {
            // Arrange
            var builder = new FastStringBuilder("cabccdececgc");

            // Act
            builder.RemoveAll('c');

            // Assert
            Assert.AreEqual("abdeeg", builder.ToString());
        }

        [Test]
        public void Reverse_CanReverseTheWholeString_WhenLengthIsEven()
        {
            // Arrange
            var builder = new FastStringBuilder("abcdef");

            // Act
            builder.Reverse(0, builder.Length);

            // Assert
            Assert.AreEqual("fedcba", builder.ToString());
        }

        [Test]
        public void Reverse_CanReverseTheWholeString_WhenLengthIsOdd()
        {
            // Arrange
            var builder = new FastStringBuilder("abcdefg");

            // Act
            builder.Reverse(0, builder.Length);

            // Assert
            Assert.AreEqual("gfedcba", builder.ToString());
        }

        [TestCase("abcdefg", "", "", "abcdefg")]
        [TestCase("abcdefg", "a", "b", "bbcdefg")]
        [TestCase("abcdefg", "cde", "fcdef", "abfcdeffg")]
        [TestCase("abcdefg", "ce", "nn", "abcdefg")]
        // Middle
        [TestCase("abcdefg", "cde", "klm", "abklmfg")]
        [TestCase("abcdefg", "cde", "kl", "abklfg")]
        [TestCase("abcdefg", "cde", "klmn", "abklmnfg")]
        // Beginning
        [TestCase("abcdefg", "abc", "klm", "klmdefg")]
        [TestCase("abcdefg", "abc", "kl", "kldefg")]
        [TestCase("abcdefg", "abc", "klmn", "klmndefg")]
        // End
        [TestCase("abcdefg", "efg", "klm", "abcdklm")]
        [TestCase("abcdefg", "efg", "kl", "abcdkl")]
        [TestCase("abcdefg", "efg", "klmn", "abcdklmn")]
        public void Replace_CanReplaceStrings(string text, string oldStr, string newStr, string expected)
        {
            var fastString = (FastStringBuilder)text;
            fastString.Replace(oldStr, newStr);
            
            Assert.AreEqual(expected, (string)fastString);
        }

        [TestCase(0, 1, "abcdef")]
        [TestCase(0, 2, "bacdef")]
        [TestCase(5, 1, "abcdef")]
        [TestCase(4, 2, "abcdfe")]
        [TestCase(1, 3, "adcbef")]
        [TestCase(2, 3, "abedcf")]
        [TestCase(1, 4, "aedcbf")]
        [TestCase(2, 4, "abfedc")]
        public void Reverse_CanReversePartOfString_WhenLengthIsEven(int start, int length, string expected)
        {
            // Arrange
            var builder = new FastStringBuilder("abcdef");

            // Act
            builder.Reverse(start, length);

            // Assert
            Assert.AreEqual(expected, builder.ToString());
        }

        [TestCase(0, 1, "abcdefg")]
        [TestCase(0, 2, "bacdefg")]
        [TestCase(6, 1, "abcdefg")]
        [TestCase(5, 2, "abcdegf")]
        [TestCase(1, 4, "aedcbfg")]
        [TestCase(2, 3, "abedcfg")]
        [TestCase(1, 6, "agfedcb")]
        [TestCase(2, 5, "abgfedc")]
        public void Reverse_CanReversePartOfString_WhenLengthIsOdd(int start, int length, string expected)
        {
            // Arrange
            var builder = new FastStringBuilder("abcdefg");

            // Act
            builder.Reverse(start, length);

            // Assert
            Assert.AreEqual(expected, builder.ToString());
        }

        [TestCase(0, "secondfirst")]
        [TestCase(5, "firstsecond")]
        [TestCase(2, "fisecondrst")]
        public void Insert_CanInsertAnotherFastStringBuilder(int position, string expected)
        {
            // Arrange
            var a = new FastStringBuilder("first");
            var b = new FastStringBuilder("second");

            // Act
            a.Insert(position, b);

            // Assert
            Assert.AreEqual(expected, a.ToString());
        }

        [TestCase(0, 1, 3, "ecofirst")]
        [TestCase(5, 1, 4, "firstecon")]
        [TestCase(2, 1, 4, "fieconrst")]
        public void Insert_CanInsertPartOfAnotherFastStringBuilder(int position, int start, int length, string expected)
        {
            // Arrange
            var a = new FastStringBuilder("first");
            var b = new FastStringBuilder("second");

            // Act
            a.Insert(position, b, start, length);

            // Assert
            Assert.AreEqual(expected, a.ToString());
        }

        [Test]
        public void Insert_ThrowsException_IfYouPassSameFastStringBuilder()
        {
            var a = new FastStringBuilder("test");

            Assert.Throws<InvalidOperationException>(() => a.Insert(0, a, 0, 1));
        }

        [Test]
        public void Insert_DoesNotThrowsException_IfTwoFastStringBuildersContainsSameThing()
        {
            var a = new FastStringBuilder("test");
            var b = new FastStringBuilder("test");

            Assert.DoesNotThrow(() => a.Insert(0, b, 0, 1));
        }

        [TestCase("test", '1', "test1")]
        [TestCase("0123456789ABCDE", 'F', "0123456789ABCDEF")]
        [TestCase("0123456789ABCDEF", '0', "0123456789ABCDEF0")]
        public void Append_WhenCapacityThresholdIsHit(string initial, char append, string expected)
        {
            var a = new FastStringBuilder(initial);
            a.Append(append);
            Assert.AreEqual(expected, a.ToString());
        }

        [TestCase("test", 5, "test")]
        [TestCase("test", 4, "test")]
        [TestCase("test", 2, "te")]
        [TestCase("test", 0, "")]
        public void Length_Set(string initial, int len, string expected)
        {
            var a = new FastStringBuilder(initial);
            a.Length = len;
            Assert.AreEqual(expected, a.ToString());
        }
    }
}
