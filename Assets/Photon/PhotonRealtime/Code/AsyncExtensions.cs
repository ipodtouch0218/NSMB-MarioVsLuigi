// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Extension methods to make async use of RealtimeClient.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System.Threading;
    #if SUPPORTED_UNITY
    using UnityEngine;
    #if UNITY_EDITOR
    using UnityEditor;
    #endif
    /// <summary>
    /// Does vital initialization to make TPL work in Unity. Should be automatically called by Unity based on the RuntimeInitializeOnLoadMethod attribute.
    /// </summary>
    public class AsyncSetup
    {
        /// <summary>
        /// A cancellation token that is used for all tasks related to the Realtime connection.
        /// </summary>
        public static CancellationTokenSource GlobalCancellationSource = new CancellationTokenSource();

        /// <summary>
        /// Creates a linked cancel token source that also triggers on global cancellation.
        /// </summary>
        /// <param name="token">Cancel token to be linked with global cancellation.</param>
        /// <returns>A cancel token source liked to the global cancellation.</returns>
        public static CancellationTokenSource CreateLinkedSource(CancellationToken token)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationSource.Token, token);
        }

        /// <summary>Initialization within Unity. Setting CancellationToken and some more.</summary>
        [RuntimeInitializeOnLoadMethod]
        public static void Startup()
        {
            // Uses a task factory that creates tasks on the same synchronization context (main thread). This is essential to make TPL comfortably work in Unity.
            AsyncConfig.InitForUnity();

            #if UNITY_EDITOR
            // Unlike coroutines Unity does not stop any task when switching the play mode in the Editor.
            // The global AsyncConfig has a cancellation that will stop all tasks there were created for the connection handling.
            AsyncConfig.Global.CancellationToken = GlobalCancellationSource.Token;
            EditorApplication.playModeStateChanged += (change) => {
                if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.ExitingEditMode) {
                    GlobalCancellationSource?.Cancel();
                    GlobalCancellationSource?.Dispose();
                    GlobalCancellationSource = new CancellationTokenSource();
                }
            };
            #endif
        }
    }
    #endif
}

namespace Photon.Realtime
{
    using System;
    #if UNITY_WEBGL
    using System.Diagnostics;
    #endif
    using System.Threading;
    using System.Threading.Tasks;
    using Photon.Client;

    /// <summary>
    /// Extensions methods to wrap Photon Realtime API calls into <see cref="System.Threading.Tasks"/>.
    /// 
    /// </summary>
    public static class AsyncExtensions
    {
        internal static AsyncConfig Resolve(this AsyncConfig config)
        {
            return config ?? AsyncConfig.Global;
        }

        /// <summary>
        /// Connect to master server.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="appSettings">App settings.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When connected to master server callback was called.</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection is terminated.</exception>
        /// <exception cref="AuthenticationFailedException">Is thrown when the authentication failed.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task ConnectUsingSettingsAsync(this RealtimeClient client, AppSettings appSettings, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.State != ClientState.Disconnected && client.State != ClientState.PeerCreated)
                {
                    return Task.FromException(new OperationStartException("Client still connected"));
                }

                if (client.ConnectUsingSettings(appSettings) == false)
                {
                    return Task.FromException(new OperationStartException("Failed to start connecting"));
                }

                var handler = client.CreateConnectionHandler(true, config.Resolve());
#if DEBUG
                handler.Name = "ConnectUsingSettings";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnCustomAuthenticationFailedMsg>(m => handler.SetException(new AuthenticationFailedException(m.debugMessage))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnConnectedToMasterMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Runs reconnect and rejoin.
        /// </summary>
        /// <param name="client">Client object should be in Disconnected state.</param>
        /// <param name="throwOnError">Set ErrorCode as result on RoomJoinFailed.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>Returns when inside the room or error</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> ReconnectAndRejoinAsync(this RealtimeClient client, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.State != ClientState.Disconnected && client.State != ClientState.PeerCreated)
                {
                    return Task.FromException<short>(new OperationStartException("Client still connected"));
                }

                if (client.ReconnectAndRejoin() == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to start reconnecting"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "ReconnectAndRejoin";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRandomFailedMsg>(m => {
                    if (throwOnError) {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else {
                        handler.SetResult(m.returnCode);
                    }}));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Reconnect to master server.
        /// </summary>
        /// <param name="client">Client object should be in Disconnected state.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns></returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task ReconnectToMasterAsync(this RealtimeClient client, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.State != ClientState.Disconnected && client.State != ClientState.PeerCreated)
                {
                    return Task.FromException(new OperationStartException("Client still connected"));
                }

                if (client.ReconnectToMaster() == false)
                {
                    return Task.FromException(new OperationStartException("Failed to start reconnecting"));
                }

                var handler = client.CreateConnectionHandler(true, config.Resolve());
#if DEBUG
                handler.Name = "ReconnectToMaster";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnConnectedToMasterMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Disconnects the client.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>Returns when the client has successfully disconnected</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task DisconnectAsync(this RealtimeClient client, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client == null)
                {
                    return Task.CompletedTask;
                }

                if (client.State == ClientState.Disconnected || client.State == ClientState.Disconnecting || client.State == ClientState.PeerCreated)
                {
                    return Task.CompletedTask;
                }

                var handler = client.CreateConnectionHandler(true, config.Resolve());
#if DEBUG
                handler.Name = "Disconnect";
#endif
                var logLevel = client.LogLevel;

                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => {
                    if (logLevel >= LogLevel.Info)
                    {
                        Log.Info($"Disconnected: {m.cause}");
                    }
                    handler.SetResult(ErrorCode.Ok);
                }));

                if (client.State != ClientState.Disconnecting)
                {
                    client.Disconnect();
                }

                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Connect to the name server and wait until ping results are available.
        /// Will work when client is already connected or connecting to nameserver.
        /// Will fail when connected to another server type.
        /// The client connection will be disconnected AFTER returning the result. It's not supposed to be useable.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="appSettings">Photon AppSettings, only uses AppId.</param>
        /// <param name="config">Async config.</param>
        /// <returns>RegionHandler with filled out EnabledRegions</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<RegionHandler> ConnectToNameserverAndWaitForRegionsAsync(this RealtimeClient client, AppSettings appSettings, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                // connected // connecting
                if (client.State == ClientState.ConnectedToNameServer || client.State == ClientState.ConnectingToNameServer)
                {
                    // empty
                }
                // disconnected
                else if (client.State == ClientState.Disconnected || client.State == ClientState.PeerCreated)
                {
                    // TODO: add different app ids here
                    var appSettingsCopy = new AppSettings(appSettings);
                    appSettingsCopy.FixedRegion = null;

                    // TODO: Implement FetchRegions() instead
                    if (client.ConnectUsingSettings(appSettingsCopy) == false)
                    {
                        return Task.FromException<RegionHandler>(new OperationStartException("Failed to start connection to nameserver"));
                    }
                }
                // everything else
                else
                {
                    return Task.FromException<RegionHandler>(new OperationStartException($"Client state ({client.State}) unuseable for name server connection."));
                }

                if (client.RegionHandler?.EnabledRegions == null || client.RegionHandler?.EnabledRegions.Count <= 0)
                {
                    var handler = client.CreateConnectionHandler(true, config.Resolve());
#if DEBUG
                    handler.Name = "ConnectToNameserverAndWaitForRegions";
#endif
                    // Because we set PingAvailableRegions ourselves the connection logic started by ConnectUsingSettings is canceled.
                    client.CallbackMessage.ListenManual<OnRegionListReceivedMsg>(m => m.regionHandler.PingAvailableRegions(r => handler.SetResult(ErrorCode.Ok)));
                    var result = handler.Task.ContinueWith(c => client.RegionHandler);
                    result.ContinueWith(c => client.DisconnectAsync());
                    return result;
                }
                return Task.FromResult(client.RegionHandler);
            }).Unwrap();
        }

        /// <summary>
        /// Create and join a room.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="enterRoomArgs">Enter room params.</param>
        /// <param name="throwOnError">Set ErrorCode as result on RoomCreateFailed or RoomJoinFailed.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When the room has been entered</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> CreateAndJoinRoomAsync(this RealtimeClient client, EnterRoomArgs enterRoomArgs, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpCreateRoom(enterRoomArgs) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send CreateRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "CreateAndJoinRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnCreateRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Join room.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="enterRoomArgs">Enter room params.</param>
        /// <param name="throwOnError">Set ErrorCode as result when JoinRoomFailed.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When room has been entered</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> JoinRoomAsync(this RealtimeClient client, EnterRoomArgs enterRoomArgs, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpJoinRoom(enterRoomArgs) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send JoinRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "JoinRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Rejoin room.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="roomName">Room name to rejoin.</param>
        /// <param name="ticket">Matchmaking Ticket for this user and room. Can be null if not used.</param>
        /// <param name="throwOnError">Set ErrorCode as result when JoinRoomFailed.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When room has been entered</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> RejoinRoomAsync(this RealtimeClient client, string roomName, object ticket = null, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {

                if (client.State != ClientState.ConnectedToMasterServer)
                {
                    return Task.FromException<short>(new OperationStartException("Must be connected to master server"));
                }

                if (client.OpRejoinRoom(roomName) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send RejoinRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "RejoinRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Join or create room.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="enterRoomArgs">Enter room params.</param>
        /// <param name="throwOnError">Set ErrorCode as result when JoinRoomFailed.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When room has been entered</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> JoinOrCreateRoomAsync(this RealtimeClient client, EnterRoomArgs enterRoomArgs, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpJoinOrCreateRoom(enterRoomArgs) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send JoinRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "JoinOrCreateRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnCreateRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Join random or create room
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="joinRandomRoomParams">Join random room params.</param>
        /// <param name="enterRoomArgs">Enter room params.</param>
        /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When inside a room</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> JoinRandomOrCreateRoomAsync(this RealtimeClient client, JoinRandomRoomArgs joinRandomRoomParams = null, EnterRoomArgs enterRoomArgs = null, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpJoinRandomOrCreateRoom(joinRandomRoomParams, enterRoomArgs) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send JoinRandomOrCreateRoom operation"));
                }
                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "JoinRandomOrCreateRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnCreateRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRandomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRoomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Join random room
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="joinRandomRoomParams">Join random room params.</param>
        /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When inside a room</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<short> JoinRandomRoomAsync(this RealtimeClient client, JoinRandomRoomArgs joinRandomRoomParams = null, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpJoinRandomRoom(joinRandomRoomParams) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send JoinRandomRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "JoinRandomRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedRoomMsg>(m => handler.SetResult(ErrorCode.Ok)));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinRandomFailedMsg>(m => {
                    if (throwOnError)
                    {
                        handler.SetException(new OperationException(m.returnCode, m.message));
                    }
                    else
                    {
                        handler.SetResult(m.returnCode);
                    }
                }));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Leave room
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="becomeInactive">If true, this player becomes inactive in the game and can return later (if PlayerTTL of the room is != 0).</param>
        /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When the room has been left and the connection to the master server was resumed.</returns>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task LeaveRoomAsync(this RealtimeClient client, bool becomeInactive = false, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.State != ClientState.Joined)
                {
                    return Task.FromException(new OperationStartException("Must be inside a room"));
                }

                if (client.OpLeaveRoom(becomeInactive) == false)
                {
                    return Task.FromException(new OperationStartException("Failed to send LeaveRoom operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "LeaveRoom";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnConnectedToMasterMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Join a Lobby
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="lobby">Lobby to Join.</param>
        /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When inside a Lobby</returns>
        public static Task<short> JoinLobbyAsync(this RealtimeClient client, TypedLobby lobby = null, bool throwOnError = true, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpJoinLobby(lobby) == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send JoinLobby operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "JoinLobby";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnJoinedLobbyMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Leave a Lobby
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="throwOnError">Set ErrorCode as result when operation fails with ErrorCode.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>When inside a Lobby</returns>
        public static Task<short> LeaveLobbyAsync(this RealtimeClient client, bool throwOnError = true, AsyncConfig config = null)
        {
            if (client.State == ClientState.ConnectedToMasterServer)
            {
                return Task.FromResult((short)ErrorCode.Ok);
            }
            else if (client.State != ClientState.JoinedLobby)
            {
                return Task.FromException<short>(new OperationStartException("Must be inside a lobby"));
            }

            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client.OpLeaveLobby() == false)
                {
                    return Task.FromException<short>(new OperationStartException("Failed to send LeaveLobby operation"));
                }

                var handler = client.CreateConnectionHandler(throwOnError, config.Resolve());
#if DEBUG
                handler.Name = "LeaveLobby";
#endif
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetException(new DisconnectException(m.cause))));
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnLeftLobbyMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }

        /// <summary>
        /// Create a <see cref="AsyncOperationHandler"/> instance, sets up the Photon callbacks, schedules removing them, create a connection service task.
        /// The handler will monitor the Photon callbacks and complete, fault accordingly.
        /// <see cref="AsyncOperationHandler.Task"/> can complete with ErrorCode.Ok, exception on errors and a timeout <see cref="OperationTimeoutException"/>.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="throwOnErrors">The default implementation will throw an exception on every unexpected result, set this to false to return a result ErrorCode instead.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        /// <returns>Phtoon Connection Handler object</returns>
        public static AsyncOperationHandler CreateConnectionHandler(this RealtimeClient client, bool throwOnErrors = true, AsyncConfig config = null)
        {
            var handler = new AsyncOperationHandler(config.Resolve().OperationTimeoutSec);

            //client.AddCallbackTarget(handler);
            //handler.Task.ContinueWith(t =>
            //{
            //    client.RemoveCallbackTarget(handler);
            //}, config.Resolve().TaskScheduler);

            CreateServiceTask(client, handler.Token, handler.CompletionSource, handler, config);

            return handler;
        }

        /// <summary>
        /// Starts a task that calls <see cref="RealtimeClient.Service()"/> every updateIntervalMs milliseconds.
        /// The task is stopped by the cancellation token from <see cref="AsyncOperationHandler.Token"/>.
        /// It will set an exception on the <see cref="AsyncOperationHandler"/> TaskCompletionSource if after the timeout it is still not completed.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="token">Cancellation token to stop the update loop.</param>
        /// <param name="completionSource">Completion source is notified on an exception in Service().</param>
        /// <param name="disposable">OperationHandler requires disposing.</param>
        /// <param name="config">Optional AsyncConfig, otherwise AsyncConfig.Global is used.</param>
        public static void CreateServiceTask(this RealtimeClient client, CancellationToken token, TaskCompletionSource<short> completionSource = null, IDisposable disposable = null, AsyncConfig config = null)
        {
            var startTime = DateTime.Now;
            config.Resolve().TaskFactory.StartNew(async () =>
            {
                // use combined tokens to support cancel all tasks (Unity)
                var linkedCancellationSource = default(CancellationTokenSource);
                var combinedToken = token;
                if (config.Resolve().CancellationToken != CancellationToken.None)
                {
                    try
                    {
                        linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, config.Resolve().CancellationToken);
                        combinedToken = linkedCancellationSource.Token;
                    }
                    catch (Exception e)
                    {
                        completionSource?.TrySetException(e);
                    }
                }

                if (config.Resolve().CreateServiceTask)
                {
#if UNITY_WEBGL
                    var sw = new Stopwatch();
#endif

                    while (combinedToken.IsCancellationRequested == false)
                    {
                        try
                        {
                            // TODO: replace by sendoutgoing verbose?
                            client.Service();

#if UNITY_WEBGL
                            sw.Restart();
                            while (combinedToken.IsCancellationRequested == false && sw.ElapsedMilliseconds < config.Resolve().ServiceIntervalMs)
                            {
                                // Task.Delay() requires threading which is not supported on WebGL. Using this yield seems to be the simplest workaround.
                                await Task.Yield();
                            }
#else
                            await Task.Delay(config.Resolve().ServiceIntervalMs, combinedToken);
#endif

                        }
                        catch (OperationCanceledException)
                        {
                            // operation handled notified it ended (breaking out of Task.Delay)
                            break;
                        }
                        catch (Exception e)
                        {
                            // exception in service, try to stop the operation handler
                            completionSource?.TrySetException(e);
                            break;
                        }
                    };
                }
                else
                {
                    await completionSource.Task;
                }

                // if the (handler) token did not signal, and the task is still running, mark it as cancelled
                if (token.IsCancellationRequested == false)
                {
                    switch (completionSource.Task.Status)
                    {
                        case TaskStatus.RanToCompletion: break;
                        case TaskStatus.Faulted: break;
                        case TaskStatus.Canceled: break;
                        default:
                            completionSource?.TrySetException(new OperationCanceledException("Operation canceled"));
                            break;
                    }
                }

                disposable?.Dispose();
                linkedCancellationSource?.Dispose();
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Returns a task that is completed when the connection disconnects.
        /// This does not create a service handler not does the task timeout.
        /// </summary>
        /// <param name="client">Client object.</param>
        /// <param name="config">Async config.</param>
        /// <returns>Task that completes upon disconnect</returns>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task WaitForDisconnect(this RealtimeClient client, AsyncConfig config = null)
        {
            return config.Resolve().TaskFactory.StartNew(() =>
            {
                if (client == null)
                {
                    return Task.CompletedTask;
                }

                if (client.State == ClientState.Disconnected || client.State == ClientState.PeerCreated)
                {
                    return Task.CompletedTask;
                }

                var handler = new AsyncOperationHandler();
                handler.Disposables.Enqueue(client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => handler.SetResult(ErrorCode.Ok)));
                return handler.Task;
            }).Unwrap();
        }
    }
}

namespace Photon.Realtime
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The operation handler is used to monitor the Photon Realtime operation callbacks for an async task.
    /// </summary>
    public class AsyncOperationHandler : IDisposable
    {
        private TaskCompletionSource<short> _result;
        private CancellationTokenSource _cancellation;

        /// <summary>
        /// Returns the task to complete.
        /// </summary>
        public Task<short> Task => _result.Task;
        /// <summary>
        /// The completion source to trigger by operation callbacks.
        /// </summary>
        public TaskCompletionSource<short> CompletionSource => _result;
        /// <summary>
        /// The cancellation token for the operation timeout, will return CancellationToken.None if no timeout is set.
        /// </summary>
        public CancellationToken Token => _cancellation == null ? CancellationToken.None : _cancellation.Token;
        /// <summary>
        /// Returns if cancellation has been requested.
        /// </summary>
        public bool IsCancellationRequested => _cancellation == null ? false : _cancellation.IsCancellationRequested;
        /// <summary>
        /// A collection of additional objects or callbacks that need to be disposed once the operation handler is done.
        /// For example subscriptions to <see cref="RealtimeClient.CallbackMessage"/>.
        /// </summary>
        public ConcurrentQueue<IDisposable> Disposables { get; private set; }
        /// <summary>
        /// The name of the operation handler, only set in DEBUG build configuration.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Create a new operation handler with a timeout.
        /// </summary>
        /// <param name="operationTimeoutSec">Operation timeout in seconds.</param>
        public AsyncOperationHandler(float operationTimeoutSec)
        {
            _result = new TaskCompletionSource<short>();
            _cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(operationTimeoutSec));
            _cancellation.Token.Register(() => SetException(new OperationTimeoutException($"Operation timed out {Name}")));
            Disposables = new ConcurrentQueue<IDisposable>();
        }

        /// <summary>
        /// Create a new operation handler without a timeout.
        /// </summary>
        public AsyncOperationHandler()
        {
            _result = new TaskCompletionSource<short>();
            Disposables = new ConcurrentQueue<IDisposable>();
        }

        /// <summary>
        /// Set the result and complete the operation handler.
        /// </summary>
        /// <param name="result">Result.</param>
        public void SetResult(short result)
        {
            if (_result.TrySetResult(result))
            {
                // Signal waiting is over
                if (_cancellation != null && _cancellation.IsCancellationRequested == false)
                {
                    _cancellation.Cancel();
                }

                Dispose();
            }
        }

        /// <summary>
        /// Set an exception as result and complete the operation handler.
        /// </summary>
        /// <param name="e">Exception to raise.</param>
        public void SetException(Exception e)
        {
            if (_result.TrySetException(e))
            {
                // Signal waiting is over
                if (_cancellation != null && _cancellation.IsCancellationRequested == false)
                {
                    _cancellation.Cancel();
                }

                Dispose();
            }
        }

        /// <summary>
        /// Dispose the handler and dispose all its <see cref="Disposables"/>.
        /// </summary>
        public void Dispose()
        {
            _cancellation?.Dispose();
            _cancellation = null;

            while (Disposables.TryDequeue(out IDisposable di))
            {
                di.Dispose();
            }
        }
    }
}

namespace Photon.Realtime
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The Photon Realtime async extension configuration is used in all async calls and will use the <see cref="Global"/> if not explicitly set.
    /// </summary>
    public class AsyncConfig
    {
        /// <summary>
        /// The global async config used by all async calls if no explicit config is passed.
        /// </summary>
        public static AsyncConfig Global = new AsyncConfig();

        /// <summary>
        /// Runs client.Service() during the operation.
        /// </summary>
        public bool CreateServiceTask { get; set; } = true;

        /// <summary>
        /// Update interval of the ServiceTask
        /// </summary>
        public int ServiceIntervalMs { get; set; } = 10;

        /// <summary>
        /// Timeout for operations will cause a OperationCanceled exception.
        /// </summary>
        public float OperationTimeoutSec { get; set; } = 15.0f;

        /// <summary>
        /// Customize the task factory. Default is Task.Factory.
        /// </summary>
        public TaskFactory TaskFactory { get; set; } = Task.Factory;

        /// <summary>
        /// Get the task scheduler from the TaskFactory, otherwise TaskScheduler.Default.
        /// </summary>
        public TaskScheduler TaskScheduler
        {
            get
            {
                if (TaskFactory?.Scheduler != null)
                {
                    return TaskFactory.Scheduler;
                }

                return TaskScheduler.Default;
            }
        }

        /// <summary>
        /// A cancellation token that is used by default in Photon Realtime async tasks and can be used to globally cancel everything.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Is the cancellation requested.
        /// </summary>
        public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

        /// <summary>
        /// Set the Global config to work with Unity.
        /// </summary>
        public static void InitForUnity()
        {
            Global = CreateUnityAsyncConfig();
        }

        /// <summary>
        /// Creates a config that works in Unity: only runs tasks on the main thread plus a CancellationSource.
        /// </summary>
        /// <returns>AsyncConfig</returns>
        public static AsyncConfig CreateUnityAsyncConfig()
        {
            return new AsyncConfig { TaskFactory = CreateUnityTaskFactory() };
        }

        /// <summary>
        /// Create a task factory that work in Unity: only runs tasks on the main thread.
        /// </summary>
        /// <returns>AsyncConfig</returns>
        public static TaskFactory CreateUnityTaskFactory()
        {
            return new TaskFactory(
              CancellationToken.None,
              TaskCreationOptions.DenyChildAttach,
              TaskContinuationOptions.DenyChildAttach | TaskContinuationOptions.ExecuteSynchronously,
              TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}

namespace Photon.Realtime
{
    using System;
    using System.Threading;

    /// <summary>
    /// Can be used inside a using-scope to keep updating the connection.
    /// </summary>
    public class ConnectionServiceScope : IDisposable
    {
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Creates a service task that calls <see cref="RealtimeClient.Service()"/> regularly.
        /// </summary>
        /// <param name="client">Client connection object.</param>
        /// <param name="config">Async config.</param>
        public ConnectionServiceScope(RealtimeClient client, AsyncConfig config = null)
        {
            cancellationTokenSource = new CancellationTokenSource();
            client.CreateServiceTask(cancellationTokenSource.Token, config: config ?? AsyncConfig.Global);
        }

        /// <summary>
        /// Dispose the service task and stop calling Service().
        /// </summary>
        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }
}

namespace Photon.Realtime
{
    using System;

    /// <summary>
    /// Is used by the <see cref="AsyncExtensions"/> to signal disconnects.
    /// </summary>
    public class DisconnectException : Exception
    {
        /// <summary>
        /// Disconnect cause.
        /// </summary>
        public DisconnectCause Cause;
        /// <summary>
        /// Create disconnect exception with cause.
        /// </summary>
        /// <param name="cause">Disconnect cause.</param>
        public DisconnectException(DisconnectCause cause) : base($"DisconnectException: {cause}")
        {
            Cause = cause;
        }
    }

    /// <summary>
    /// Is used by the <see cref="AsyncExtensions"/> to signal authentication errors during connect.
    /// </summary>
    public class AuthenticationFailedException : Exception
    {
        /// <summary>
        /// Create authentication failed exception.
        /// </summary>
        /// <param name="message">Debug message.</param>
        public AuthenticationFailedException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Is used by the <see cref="AsyncExtensions"/> to signal that the requested operation failed.
    /// </summary>
    public class OperationException : Exception
    {
        /// <summary>
        /// Error code specific to the Photon Realtime operation.
        /// </summary>
        public short ErrorCode;
        /// <summary>
        /// Create an operation exception with error code and message.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="message">Debug message.</param>
        public OperationException(short errorCode, string message) : base($"{message} (ErrorCode: {errorCode})")
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Is used by the <see cref="AsyncExtensions"/> to signal that the operation could not be started.
    /// </summary>
    public class OperationStartException : Exception
    {
        /// <summary>
        /// Create an operation start exception with message.
        /// </summary>
        /// <param name="message">Debug message.</param>
        public OperationStartException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Is used by the <see cref="AsyncExtensions"/> to signal that the operation timed out.
    /// The operation timeout is controlled by the <see cref="AsyncConfig.OperationTimeoutSec"/>.
    /// </summary>
    public class OperationTimeoutException : Exception
    {
        /// <summary>
        /// Create a operation timeout exception with message.
        /// </summary>
        /// <param name="message">Debug message.</param>
        public OperationTimeoutException(string message) : base(message)
        {
        }
    }
}
