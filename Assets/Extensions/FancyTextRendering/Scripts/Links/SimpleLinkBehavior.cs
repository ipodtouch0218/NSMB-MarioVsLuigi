using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicUI.FancyTextRendering
{
    /// <summary>
    /// Allows links in TextMeshPro text objects to be clicked on, and gives them custom colors when they are hovered or clicked.
    /// </summary>
    [RequireComponent(typeof(TextLinkHelper))]
    [DisallowMultipleComponent]
    public class SimpleLinkBehavior : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<TextLinkHelper>().OnLinkClicked += ClickOnLink;
        }

        private void ClickOnLink(string linkID)
        {
            if (CustomLinks.TryGetValue(linkID, out var action))
                action?.Invoke();
            else
                Application.OpenURL(linkID);

            Debug.Log($"clicked on link: {linkID}");
        }


        private Dictionary<string, Action> CustomLinks = new Dictionary<string, Action>();

        /// <summary>
        /// Sets some code to be run when a link is clicked on. 
        /// If a link doesn't have a custom action set, we will use <see cref="Application.OpenURL(string)"/> on it.
        /// </summary>
        /// <param name="linkID"></param>
        /// <param name="linkAction"></param>
        public void SetCustomLink(string linkID, Action linkAction)
        {
            CustomLinks[linkID] = linkAction;
        }
    }
}