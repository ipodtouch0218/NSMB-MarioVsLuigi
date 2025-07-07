// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Error information event type.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using Photon.Client;


    /// <summary>
    /// Class wrapping the received <see cref="EventCode.ErrorInfo"/> event.
    /// </summary>
    /// <remarks>
    /// This is passed inside <see cref="IErrorInfoCallback.OnErrorInfo"/> callback.
    /// If you implement <see cref="IOnEventCallback.OnEvent"/> or <see cref="RealtimeClient.EventReceived"/> you will also get <see cref="EventCode.ErrorInfo"/> but not parsed.
    ///
    /// In most cases this could be either:
    /// 1. an error from webhooks plugin (if HasErrorInfo is enabled), read more here:
    /// https://doc.photonengine.com/en-us/realtime/current/gameplay/web-extensions/webhooks#options
    /// 2. an error sent from a custom server plugin via PluginHost.BroadcastErrorInfoEvent, see example here:
    /// https://doc.photonengine.com/en-us/server/current/plugins/manual#handling_http_response
    /// 3. an error sent from the server, for example, when the limit of cached events has been exceeded in the room
    /// (all clients will be disconnected and the room will be closed in this case)
    /// read more here: https://doc.photonengine.com/en-us/realtime/current/gameplay/cached-events#special_considerations
    /// </remarks>
    public class ErrorInfo
    {
        /// <summary>
        /// String containing information about the error.
        /// </summary>
        public readonly string Info;

        /// <summary>Creates a new ErrorInfo from a (Photon) event.</summary>
        /// <param name="eventData">The event to read.</param>
        public ErrorInfo(EventData eventData)
        {
            this.Info = eventData[ParameterCode.Info] as string;
        }

        /// <summary>Provides a string representation of this instance.</summary>
        /// <returns>String representation of this instance.</returns>
        public override string ToString()
        {
            return string.Format("ErrorInfo: {0}", this.Info);
        }
    }
}