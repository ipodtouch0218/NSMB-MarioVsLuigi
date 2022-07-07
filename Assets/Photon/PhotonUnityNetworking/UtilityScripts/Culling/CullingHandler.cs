﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CullingHandler.cs" company="Exit Games GmbH">
//   Part of: Photon Unity Utilities,
// </copyright>
// <summary>
//  Handles the network culling.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEngine;

using Photon.Pun;

namespace Photon.Pun.UtilityScripts
{
    using ExitGames.Client.Photon;


    /// <summary>
    ///     Handles the network culling.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class CullingHandler : MonoBehaviour, IPunObservable
    {
        #region VARIABLES

        private int orderIndex;

        private CullArea cullArea;

        private List<byte> previousActiveCells, activeCells;

        private PhotonView pView;

        private Vector3 lastPosition, currentPosition;
        
        
        // used to limit the number of UpdateInterestGroups calls per second (there is no use to change groups more than a few times per second, even if the Culling algorithm makes it look like that)
        private float timeSinceUpdate;
        // see timeSinceUpdate
        private float timeBetweenUpdatesMin = 0.33f;


        #endregion

        #region UNITY_FUNCTIONS

        /// <summary>
        ///     Gets references to the PhotonView component and the cull area game object.
        /// </summary>
        private void OnEnable()
        {
            if (this.pView == null)
            {
                this.pView = GetComponent<PhotonView>();

                if (!this.pView.IsMine)
                {
                    return;
                }
            }

            if (this.cullArea == null)
            {
                this.cullArea = FindObjectOfType<CullArea>();
            }

            this.previousActiveCells = new List<byte>(0);
            this.activeCells = new List<byte>(0);

            this.currentPosition = this.lastPosition = transform.position;
        }

        /// <summary>
        ///     Initializes the right interest group or prepares the permanent change of the interest Group of the PhotonView component.
        /// </summary>
        private void Start()
        {
            if (!this.pView.IsMine)
            {
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                if (this.cullArea.NumberOfSubdivisions == 0)
                {
                    this.pView.Group = this.cullArea.FIRST_GROUP_ID;

                    PhotonNetwork.SetInterestGroups(this.cullArea.FIRST_GROUP_ID, true);
                }
                else
                {
                    // This is used to continuously update the active group.
                    this.pView.ObservedComponents.Add(this);
                }
            }
        }



        /// <summary>
        ///     Checks if the player has moved previously and updates the interest groups if necessary.
        /// </summary>
        private void Update()
        {
            if (!this.pView.IsMine)
            {
                return;
            }

            // we'll limit how often this update may run at all (to avoid too frequent changes and flooding the server with SetInterestGroups calls)
            this.timeSinceUpdate += Time.deltaTime;
            if (this.timeSinceUpdate < this.timeBetweenUpdatesMin)
            {
                return;
            }

            this.lastPosition = this.currentPosition;
            this.currentPosition = transform.position;

            // This is a simple position comparison of the current and the previous position. 
            // When using Network Culling in a bigger project keep in mind that there might
            // be more transform-related options, e.g. the rotation, or other options to check.
            if (this.currentPosition != this.lastPosition)
            {
                if (this.HaveActiveCellsChanged())
                {
                    this.UpdateInterestGroups();
                    this.timeSinceUpdate = 0;
                }
            }
        }

        /// <summary>
        ///     Drawing informations.
        /// </summary>
        private void OnGUI()
        {
            if (!this.pView.IsMine)
            {
                return;
            }

            string subscribedAndActiveCells = "Inside cells:\n";
            string subscribedCells = "Subscribed cells:\n";

            for (int index = 0; index < this.activeCells.Count; ++index)
            {
                if (index <= this.cullArea.NumberOfSubdivisions)
                {
                    subscribedAndActiveCells += this.activeCells[index] + " | ";
                }

                subscribedCells += this.activeCells[index] + " | ";
            }
            GUI.Label(new Rect(20.0f, Screen.height - 120.0f, 200.0f, 40.0f), "<color=white>PhotonView Group: " + this.pView.Group + "</color>", new GUIStyle() { alignment = TextAnchor.UpperLeft, fontSize = 16 });
            GUI.Label(new Rect(20.0f, Screen.height - 100.0f, 200.0f, 40.0f), "<color=white>" + subscribedAndActiveCells + "</color>", new GUIStyle() { alignment = TextAnchor.UpperLeft, fontSize = 16 });
            GUI.Label(new Rect(20.0f, Screen.height - 60.0f, 200.0f, 40.0f), "<color=white>" + subscribedCells + "</color>", new GUIStyle() { alignment = TextAnchor.UpperLeft, fontSize = 16 });
        }

        #endregion

        /// <summary>
        ///     Checks if the previously active cells have changed.
        /// </summary>
        /// <returns>True if the previously active cells have changed and false otherwise.</returns>
        private bool HaveActiveCellsChanged()
        {
            if (this.cullArea.NumberOfSubdivisions == 0)
            {
                return false;
            }

            this.previousActiveCells = new List<byte>(this.activeCells);
            this.activeCells = this.cullArea.GetActiveCells(transform.position);

            // If the player leaves the area we insert the whole area itself as an active cell.
            // This can be removed if it is sure that the player is not able to leave the area.
            while (this.activeCells.Count <= this.cullArea.NumberOfSubdivisions)
            {
                this.activeCells.Add(this.cullArea.FIRST_GROUP_ID);
            }

            if (this.activeCells.Count != this.previousActiveCells.Count)
            {
                return true;
            }

            if (this.activeCells[this.cullArea.NumberOfSubdivisions] != this.previousActiveCells[this.cullArea.NumberOfSubdivisions])
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Unsubscribes from old and subscribes to new interest groups.
        /// </summary>
        private void UpdateInterestGroups()
        {
            List<byte> disable = new List<byte>(0);

            foreach (byte groupId in this.previousActiveCells)
            {
                if (!this.activeCells.Contains(groupId))
                {
                    disable.Add(groupId);
                }
            }

            PhotonNetwork.SetInterestGroups(disable.ToArray(), this.activeCells.ToArray());
        }

        #region IPunObservable implementation

        /// <summary>
        ///     This time OnPhotonSerializeView is not used to send or receive any kind of data.
        ///     It is used to change the currently active group of the PhotonView component, making it work together with PUN more directly.
        ///     Keep in mind that this function is only executed, when there is at least one more player in the room.
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            // If the player leaves the area we insert the whole area itself as an active cell.
            // This can be removed if it is sure that the player is not able to leave the area.
            while (this.activeCells.Count <= this.cullArea.NumberOfSubdivisions)
            {
                this.activeCells.Add(this.cullArea.FIRST_GROUP_ID);
            }

            if (this.cullArea.NumberOfSubdivisions == 1)
            {
                this.orderIndex = (++this.orderIndex % this.cullArea.SUBDIVISION_FIRST_LEVEL_ORDER.Length);
                this.pView.Group = this.activeCells[this.cullArea.SUBDIVISION_FIRST_LEVEL_ORDER[this.orderIndex]];
            }
            else if (this.cullArea.NumberOfSubdivisions == 2)
            {
                this.orderIndex = (++this.orderIndex % this.cullArea.SUBDIVISION_SECOND_LEVEL_ORDER.Length);
                this.pView.Group = this.activeCells[this.cullArea.SUBDIVISION_SECOND_LEVEL_ORDER[this.orderIndex]];
            }
            else if (this.cullArea.NumberOfSubdivisions == 3)
            {
                this.orderIndex = (++this.orderIndex % this.cullArea.SUBDIVISION_THIRD_LEVEL_ORDER.Length);
                this.pView.Group = this.activeCells[this.cullArea.SUBDIVISION_THIRD_LEVEL_ORDER[this.orderIndex]];
            }
        }

        #endregion
    }
}