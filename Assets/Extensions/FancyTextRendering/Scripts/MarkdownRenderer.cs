using TMPro;
using UnityEngine;

namespace LogicUI.FancyTextRendering
{
    [RequireComponent(typeof(TMP_Text))]
    public class MarkdownRenderer : MonoBehaviour
    {
        [SerializeField]
        [TextArea(minLines: 10, maxLines: 50)]
        string _Source;

        public string Source
        {
            get => _Source;
            set
            {
                _Source = value;
                RenderText();
            }
        }

        TMP_Text _TextMesh;
        public TMP_Text TextMesh
        {
            get
            {
                if (_TextMesh == null)
                    _TextMesh = GetComponent<TMP_Text>();

                return _TextMesh;
            }
        }

        private void OnValidate()
        {
            RenderText();
        }


        public MarkdownRenderingSettings RenderSettings = MarkdownRenderingSettings.Default;

        private void RenderText()
        {
            Markdown.RenderToTextMesh(Source, TextMesh, RenderSettings);
        }
    }
}