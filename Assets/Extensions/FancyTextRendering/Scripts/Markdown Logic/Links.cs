using System;
using System.Text;
using UnityEngine;
using JimmysUnityUtilities;

namespace LogicUI.FancyTextRendering.MarkdownLogic
{
    // Important note about link coloring: yes, link colors are assigned by LinkTextHelper. However:
    //    A) This can only be done after a delay of one frame due to TMP bullshit. Unless we also set the color in the rich
    //       text tags, the color will flicker for a frame.
    //    B) If we want to change the alpha of the text (such as fading chat messages), that resets the LinkTextHelper colors,
    //       because TMP resets the mesh vertex colors when the base color changes.
    // So, TextLinkHelper handles colors for hovering and clicking on the link, but we ensure links always appear with the
    // appropriate color by also doing it in rich text.

    class Links : SimpleMarkdownLineProcessor
    {
        protected override void ProcessLine(MarkdownLine line, MarkdownRenderingSettings settings)
        {
            StringBuilder builder = line.Builder;

            int linkTextStart = line.UnescapedIndexOf('[');

            while (linkTextStart > -1)
            {
                int linkTextEnd = line.UnescapedIndexOf(']');
                if (linkTextEnd < 0)
                    return;

                int linkContentStart = line.UnescapedIndexOf('(', startIndex: linkTextEnd);
                if (linkContentStart < 0)
                    return;

                if (linkContentStart != linkTextEnd + 1)
                    return;

                int linkContentEnd = line.UnescapedIndexOf(')', startIndex: linkContentStart); // Escaping this doesn't work properly for some reason -- todo investigate
                if (linkContentEnd < 0)
                    return;

                if (linkContentEnd - linkContentStart < 2)
                    return;


                string linkText = builder.Snip(linkTextStart + 1, linkTextEnd - 1);
                string linkContent = builder.Snip(linkContentStart + 1, linkContentEnd - 1);

                builder.Remove(linkTextStart, linkContentEnd - linkTextStart + 1);
                builder.InsertChain(linkTextStart, out int insertionEndIndex, 
                    "<color=#", ColorUtility.ToHtmlStringRGBA(settings.Links.LinkColor), ">",
                    "<link=\"", linkContent, "\">",
                    linkText,
                    "</link></color>");


                linkTextStart = builder.IndexOf('[', startIndex: insertionEndIndex);
            }
        }


        protected override bool AllowedToProces(MarkdownRenderingSettings settings)
            => settings.Links.RenderLinks;
    }

    // Auto-links don't override regular links because they only apply to links without a character in front of them, and auto links always have
    // the quotation marks.
    abstract class AutoLinks : SimpleMarkdownLineProcessor
    {
        protected abstract string LinkStartString { get; }

        protected override void ProcessLine(MarkdownLine line, MarkdownRenderingSettings settings)
        {
            StringBuilder builder = line.Builder;

            int linkStartIndex = builder.IndexOf(LinkStartString, ignoreCase: true);

            while (linkStartIndex > -1)
            {
                if (linkStartIndex == 0 || builder[linkStartIndex - 1].IsWhitespaceOrNonBreakingSpace())
                {
                    int linkEndIndex = builder.IndexOfWhitespace(linkStartIndex + LinkStartString.Length) - 1;
                    if (linkEndIndex < 0)
                        linkEndIndex = builder.Length - 1;

                    int linkLength = linkEndIndex - linkStartIndex;
                    if (linkLength <= LinkStartString.Length)
                    {
                        linkStartIndex = builder.IndexOf(LinkStartString, linkStartIndex + LinkStartString.Length, ignoreCase: true);
                        continue;
                    }

                    int nextDotIndex = builder.IndexOf('.', linkStartIndex + LinkStartString.Length);

                    if (nextDotIndex < 0 || nextDotIndex >= linkEndIndex)
                    {
                        linkStartIndex = builder.IndexOf(LinkStartString, linkEndIndex, ignoreCase: true);
                        continue;
                    }


                    string linkText = builder.Snip(linkStartIndex, linkEndIndex);

                    builder.InsertChain(linkStartIndex, out int insertionEndIndex,
                        "<color=#", ColorUtility.ToHtmlStringRGBA(settings.Links.LinkColor), ">",
                        "<link=\"", linkText, "\">");

                    builder.Insert(insertionEndIndex + linkText.Length, "</link></color>");

                    linkStartIndex = builder.IndexOf(LinkStartString, insertionEndIndex + 15);
                }
                else
                {
                    linkStartIndex = builder.IndexOf(LinkStartString, linkStartIndex + LinkStartString.Length);
                }
            }
        }


        protected override bool AllowedToProces(MarkdownRenderingSettings settings)
            => settings.Links.RenderAutoLinks;
    }

    class AutoLinksHttps : AutoLinks
    {
        protected override string LinkStartString => "https://";
    }

    class AutoLinksHttp : AutoLinks
    {
        protected override string LinkStartString => "http://";
    }
}