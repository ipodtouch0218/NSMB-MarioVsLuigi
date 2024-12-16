using System;
using System.Collections.Generic;
using System.Text;
using JimmysUnityUtilities;

namespace LogicUI.FancyTextRendering.MarkdownLogic
{
    internal abstract class MarkdownLineProcessorBase
    {
        public void Process(IReadOnlyList<MarkdownLine> lines, MarkdownRenderingSettings settings)
        {
            if (!AllowedToProces(settings))
                return;

            ProcessInternal(lines, settings);
        }

        protected virtual bool AllowedToProces(MarkdownRenderingSettings settings) => true;
        protected abstract void ProcessInternal(IReadOnlyList<MarkdownLine> lines, MarkdownRenderingSettings settings);
    }
}