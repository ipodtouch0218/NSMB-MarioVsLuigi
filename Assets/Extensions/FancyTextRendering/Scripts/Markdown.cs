using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JimmysUnityUtilities;
using LogicUI.FancyTextRendering.MarkdownLogic;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

namespace LogicUI.FancyTextRendering
{
    /// <summary>
    /// Converts markdown into TMP rich text tags.
    /// Very unfinished and experimental. Not even close to being a complete markdown renderer.
    /// </summary>
    public static class Markdown
    {
        // Useful links for anyone who wants to finish this
        // http://digitalnativestudios.com/textmeshpro/docs/rich-text/
        // https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet


        public static void RenderToTextMesh(string markdownSource, TMP_Text textMesh)
            => RenderToTextMesh(markdownSource, textMesh, MarkdownRenderingSettings.Default);

        public static void RenderToTextMesh(string markdownSource, TMP_Text textMesh, MarkdownRenderingSettings settings, params ICustomTextPreProcessor[] customTextPreProcessors)
        {
            string richText = MarkdownToRichText(markdownSource, settings, customTextPreProcessors);

            textMesh.text = richText;
            UpdateTextMesh(textMesh);
        }

        public static void UpdateTextMesh(TMP_Text textMesh)
        {
            ResetLinkInfo(); // TextMeshPro doesn't reset the link infos automatically, so we have to do it manually in situations where it will be changed

            textMesh.ForceMeshUpdate();
            textMesh.GetComponent<TextLinkHelper>()?.LinkDataUpdated();


            void ResetLinkInfo()
            {
                if (textMesh.textInfo != null) // Make sure the text is initialized; required as of TMP 2.1
                {
                    textMesh.textInfo.linkInfo = Array.Empty<TMP_LinkInfo>();
                    textMesh.textInfo.linkCount = 0;
                }
            }
        }


        public static string MarkdownToRichText(string source)
            => MarkdownToRichText(source, MarkdownRenderingSettings.Default);

        public static string MarkdownToRichText(string source, MarkdownRenderingSettings settings, params ICustomTextPreProcessor[] customTextPreProcessors)
        {
            if (source.IsNullOrEmpty())
                return String.Empty;


            Profiler.BeginSample(nameof(MarkdownToRichText));

            var lines = new List<MarkdownLine>();

            using (var reader = new StringReader(source))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(new MarkdownLine()
                    {
                        Builder = new StringBuilder(line)
                    });
                }
            }


            foreach (var processor in customTextPreProcessors)
            {
                foreach (var line in lines)
                    processor.ProcessLine(line.Builder);
            }


            foreach (var processor in BuiltInLineProcessors)
                processor.Process(lines, settings);


            var builder = new StringBuilder();

            foreach (var line in lines)
            {
                if (!line.DeleteLineAfterProcessing)
                    builder.AppendLine(line.Finish());
            }

            Profiler.EndSample();
            return builder.ToString();
        }


        private static readonly IReadOnlyList<MarkdownLineProcessorBase> BuiltInLineProcessors = new MarkdownLineProcessorBase[]
        {
            // Order of processing here does matter, be mindful when adding to this list.
            new AutoLinksHttp(),
            new AutoLinksHttps(),
            new UnorderedLists(),
            new OrderedLists(),
            new Bold(),
            new Italics(),
            new Strikethrough(),
            new SuperscriptChain(), // Important to process chain before single!
            new SuperscriptSingle(),
            new Monospace(),
            new Headers(),
            new Links(),
        };
    }
}