using System.Text;

namespace LogicUI.FancyTextRendering
{
    /// <summary>
    /// Allows efficient pre-processing of text before the main markdown processing is applied. Use with <see cref="Markdown.MarkdownToRichText(string, MarkdownRenderingSettings, ICustomTextPreProcessor[])"/> or 
    /// </summary>
    public interface ICustomTextPreProcessor
    {
        void ProcessLine(StringBuilder lineBuilder);
    }
}