using System.Text;
using NUnit.Framework;

namespace RTLTMPro.Tests
{
    public class RichTextFixerTests
    {
        [Test]
        public void FindTag_FindsSimpleOpeningTag()
        {
            // Arrange
            var text = new FastStringBuilder("text <opening> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(13, tag.End);
        }
        
        [Test]
        public void FindTag_FindsOpeningTagWithSpaceInside()
        {
            // Arrange
            var text = new FastStringBuilder("text <ope ning> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(14, tag.End);
        }
        
        [Test]
        public void FindTag_FindsOpeningTagWithValue()
        {
            // Arrange
            var text = new FastStringBuilder("text <opening=12m> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(17, tag.End);
        }
        
        [Test]
        public void FindTag_ProducesTheSameHash_ForOpeningTagsWithDifferentValues()
        {
            // Arrange
            var text1 = new FastStringBuilder("text <opening=12m> text");
            var text2 = new FastStringBuilder("text <opening=18s> text");
            
            // Act
            RichTextFixer.FindTag(text1, 0, out var tag1);
            RichTextFixer.FindTag(text2, 0, out var tag2);
            
            // Assert
            Assert.AreEqual(tag1.HashCode, tag2.HashCode);
        }

        [Test]
        public void FindTag_FindsSimpleSelfContainedTag()
        {
            // Arrange
            var text = new FastStringBuilder("text <opening/> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.SelfContained, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(14, tag.End);
        }
        
        [Test]
        public void FindTag_FindsSelfContainedTagWithValue()
        {
            // Arrange
            var text = new FastStringBuilder("text <opening=15m/> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.SelfContained, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(18, tag.End);
        }

        [Test]
        public void FindTag_ProducesTheSameHash_ForSelfContainedTagsWithDifferentValues()
        {
            // Arrange
            var text1 = new FastStringBuilder("text <opening=15m/> text");
            var text2 = new FastStringBuilder("text <opening=20d/> text");

            // Act
            RichTextFixer.FindTag(text1, 0, out var tag1);
            RichTextFixer.FindTag(text2, 0, out var tag2);

            // Assert
            Assert.AreEqual(tag1.HashCode, tag2.HashCode);
        }
        
        [Test]
        public void FindTag_FindsClosingTag()
        {
            // Arrange
            var text = new FastStringBuilder("text </closing> text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(RichTextFixer.TagType.Closing, tag.Type);
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(14, tag.End);
        }

        [Test]
        public void FindTag_ProducesTheSameHash_ForSameOpeningAndClosingTag()
        {
            // Arrange
            var text1 = new FastStringBuilder("text <tag=15m/> text");
            var text2 = new FastStringBuilder("text </tag> text");

            // Act
            RichTextFixer.FindTag(text1, 0, out var tag1);
            RichTextFixer.FindTag(text2, 0, out var tag2);

            // Assert
            Assert.AreEqual(tag1.HashCode, tag2.HashCode);   
        }

        [Test]
        public void FindTag_ReturnsZero_WhenNoTagIsFound()
        {
            // Arrange
            var text = new FastStringBuilder("text");
            
            // Act
            RichTextFixer.FindTag(text, 0, out var tag);
            
            // Assert
            Assert.AreEqual(0, tag.Start);
            Assert.AreEqual(0, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.None, tag.Type);
            Assert.AreEqual(0, tag.HashCode);
        }

        [Test]
        public void FindTag_StartFromTheProvidedStartPosition()
        {
            // Arrange
            var text = new FastStringBuilder(" <tag> text");
            
            // Act
            RichTextFixer.FindTag(text, 6, out var tag);
            
            // Assert
            Assert.AreEqual(0, tag.Start);
            Assert.AreEqual(0, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.None, tag.Type);
            Assert.AreEqual(0, tag.HashCode);
        }

        [Test]
        public void FindTag_OpeningTagWithAttributes()
        {
            // Arrange
            var text = new FastStringBuilder("test <mytag=4 name=\"asdf\" id=5> text");

            // Act
            RichTextFixer.FindTag(text, 0, out var tag);

            // Assert
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(30, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
        }

        [Test]
        public void FindTag_IgnoreTagWithoutClosingChar()
        {
            // Arrange
            var text = new FastStringBuilder("test <my invalid tag<b> text");

            // Act
            RichTextFixer.FindTag(text, 0, out var tag);

            // Assert
            Assert.AreEqual(20, tag.Start);
            Assert.AreEqual(22, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
        }

        [Test]
        public void FindTag_IgnoreTagStartingWithSpace()
        {
            // Arrange
            var text = new FastStringBuilder("test < my invalid tag> text");

            // Act
            RichTextFixer.FindTag(text, 0, out var tag);

            // Assert
            Assert.AreEqual(0, tag.Start);
            Assert.AreEqual(0, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.None, tag.Type);
        }

        // prevent regression for issue #56
        [Test]
        public void FindTag_SpriteWithIdTag()
        {
            // Arrange
            var text = new FastStringBuilder("test <sprite=6> text");

            // Act
            RichTextFixer.FindTag(text, 0, out var tag);

            // Assert
            Assert.AreEqual(5, tag.Start);
            Assert.AreEqual(14, tag.End);
            Assert.AreEqual(RichTextFixer.TagType.Opening, tag.Type);
        }

        [Test]
        public void Fix_Reverses_SelfContainedTags()
        {
            // Arrange
            var text = new FastStringBuilder("text <self-contained/> text");
            
            // Act
            RichTextFixer.Fix(text);
            
            // Assert
            Assert.AreEqual("text >/deniatnoc-fles< text", text.ToString());
        }

        [Test]
        public void Fix_Reverses_OpenTag_ThatDoesntHaveClosingTag()
        {
            // Arrange
            var text = new FastStringBuilder("text <open> text");
            
            // Act
            RichTextFixer.Fix(text);
            
            // Assert
            Assert.AreEqual("text >nepo< text", text.ToString());
        }
        
        [Test]
        public void Fix_Reverses_SimpleOpenAndClosingTag()
        {
            // Arrange
            var text = new FastStringBuilder("text </open> text <open>");
            
            // Act
            RichTextFixer.Fix(text);
            
            // Assert
            Assert.AreEqual("text >nepo/< text >nepo<", text.ToString());
        }
        
        [Test]
        public void Fix_Reverses_SimpleOpenAndClosingTagWithValue()
        {
            // Arrange
            var text = new FastStringBuilder("text </open> text <open=134>");
            
            // Act
            RichTextFixer.Fix(text);
            
            // Assert
            Assert.AreEqual("text >nepo/< text >431=nepo<", text.ToString());
        }
    }
}
