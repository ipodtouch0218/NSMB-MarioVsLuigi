using JimmysUnityUtilities;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LogicUI.FancyTextRendering
{
    /// <summary>
    /// Allows links in TextMeshPro text objects to be clicked on, and gives them custom colors when they are hovered or clicked.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextLinkHelper : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private TMP_Text _Text;
        public TMP_Text Text
        {
            get
            {
                if (_Text == null)
                    _Text = GetComponent<TMP_Text>();

                return _Text;
            }
        }

        private void OnValidate()
        {
            SetAllLinksToNormalColor();
        }

        private void OnEnable()
        {
            previouslyHoveredLinkIndex = -1;

            // I'm not sure why the frame delay is necessary, but it is.
            // I suspect that TMP changes the colors in LateUpdate or something
            CoroutineUtility.RunAfterOneFrame(SetAllLinksToNormalColor);
        }

        private void OnDisable()
        {
            HoverEnded();
        }


        [ColorUsage(showAlpha: false), SerializeField]
        Color32 LinkNormalColor = new Color32(29, 124, 234, 255);

        [ColorUsage(showAlpha: false), SerializeField]
        Color32 LinkHoveredColor = new Color32(72, 146, 231, 255);

        [ColorUsage(showAlpha: false), SerializeField]
        Color32 LinkClickColor = new Color32(38, 108, 190, 255);

        public void SetColors(Color32 linkNormalColor, Color32 linkHoveredColor, Color32 linkClickColor)
        {
            LinkNormalColor = linkNormalColor;
            LinkHoveredColor = linkHoveredColor;
            LinkClickColor = linkClickColor;

            SetAllLinksToNormalColor();
        }

        public void LinkDataUpdated()
        {
            previouslyHoveredLinkIndex = -1;
            SetAllLinksToNormalColor();
        }


        public event Action<string> OnLinkClicked;

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(Text, eventData.pressPosition, eventData.pressEventCamera);
            if (linkIndex > -1)
            {
                var link = Text.textInfo.linkInfo[linkIndex];
                OnLinkClicked?.Invoke(link.GetLinkID());
            }
        }



        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            PointerIsDown = true;
            SetLinkToColor(previouslyHoveredLinkIndex, LinkClickColor);
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            PointerIsDown = false;
            SetLinkToColor(previouslyHoveredLinkIndex, LinkNormalColor);
            previouslyHoveredLinkIndex = -1; // Reset the link hovered caching so that in Update() it's set back to the hovered color
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            CurrentlyHoveredOver = true;
            PointerIsDown = false;
            cachedCamera = eventData.enterEventCamera;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            CurrentlyHoveredOver = false;
            HoverEnded();
            SetAllLinksToNormalColor();
        }


        Camera cachedCamera;

        bool CurrentlyHoveredOver;
        bool PointerIsDown;
        void Update()
        {
            if (!CurrentlyHoveredOver)
                return;

            if (PointerIsDown)
                return;


            int linkIndex = TMP_TextUtilities.FindIntersectingLink(Text, Input.mousePosition, cachedCamera);
            if (linkIndex < 0)
                HoverEnded();
            else
                HoverOnLink(linkIndex);
        }


        private void SetAllLinksToNormalColor()
        {
            if (Text.textInfo == null || Text.textInfo.linkInfo == null) // Text object isn't initialized yet; required as of TMP 2.1
                return;

            for (int i = 0; i < Text.textInfo.linkInfo.Length; i++)
                SetLinkToColor(i, LinkNormalColor);
        }


        public event Action<string> OnLinkHovered;
        public event Action OnLinkHoverEnded;

        private int previouslyHoveredLinkIndex = -1;
        private void HoverOnLink(int linkIndex)
        {
            if (linkIndex < 0 || linkIndex >= Text.textInfo.linkInfo.Length)
                return;

            if (linkIndex == previouslyHoveredLinkIndex)
                return;

            previouslyHoveredLinkIndex = linkIndex;
            SetLinkToColor(linkIndex, LinkHoveredColor);

            string linkID = Text.textInfo.linkInfo[linkIndex].GetLinkID();
            OnLinkHovered?.Invoke(linkID);
        }

        private void HoverEnded()
        {
            SetLinkToColor(previouslyHoveredLinkIndex, LinkNormalColor);

            if (previouslyHoveredLinkIndex > -1)
                OnLinkHoverEnded?.Invoke();

            previouslyHoveredLinkIndex = -1;
        }


        private void SetLinkToColor(int linkIndex, Color32 color)
        {
            if (linkIndex < 0 || linkIndex >= Text.textInfo.linkInfo.Length)
                return;

            TMP_LinkInfo linkInfo = Text.textInfo.linkInfo[linkIndex];

            if (linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength - 1 >= Text.textInfo.characterInfo.Length)
                return;


            for (int i = 0; i < linkInfo.linkTextLength; i++)
            {
                int characterIndex = linkInfo.linkTextfirstCharacterIndex + i;
                var charInfo = Text.textInfo.characterInfo[characterIndex];
                int meshIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                var characterVertexColors = Text.textInfo.meshInfo[meshIndex].colors32;

                if (charInfo.isVisible)
                {
                    characterVertexColors[vertexIndex + 0] = color;
                    characterVertexColors[vertexIndex + 1] = color;
                    characterVertexColors[vertexIndex + 2] = color;
                    characterVertexColors[vertexIndex + 3] = color;
                }
            }

            Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }
    }
}