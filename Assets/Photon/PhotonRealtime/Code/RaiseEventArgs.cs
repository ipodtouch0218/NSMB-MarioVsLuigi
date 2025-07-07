// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Argument for Op RaiseEvent.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    /// <summary>Aggregates several less-often used args for operation RaiseEvent. See field descriptions for usage details.</summary>
    public struct RaiseEventArgs
    {
        /// <summary>Default args: CachingOption: DoNotCache, InterestGroup: 0, targetActors: null, receivers: Others, sequenceChannel: 0.</summary>
        public readonly static RaiseEventArgs Default = new RaiseEventArgs();

        /// <summary>Defines if the server should simply send the event, put it in the cache or remove events that are like this one.</summary>
        /// <remarks>
        /// When using option: SliceSetIndex, SlicePurgeIndex or SlicePurgeUpToIndex, set a CacheSliceIndex. All other args except SequenceChannel get ignored.
        /// </remarks>
        public EventCaching CachingOption;

        /// <summary>The number of the Interest Group to send this to. 0 goes to all users but to get 1 and up, clients must subscribe to the group first.</summary>
        public byte InterestGroup;

        /// <summary>A list of Player.ActorNumbers to send this event to. You can implement events that just go to specific users this way.</summary>
        public int[] TargetActors;

        /// <summary>Sends the event to All, MasterClient or Others (default). Be careful with MasterClient, as the client might disconnect before it got the event and it gets lost.</summary>
        public ReceiverGroup Receivers;

        ///// <summary>Used along with CachingOption SliceSetIndex, SlicePurgeIndex or SlicePurgeUpToIndex if you want to set or purge a specific cache-slice.</summary>
        //public int CacheSliceIndex;
    }
}