// ----------------------------------------------------------------------------
// <copyright file="RegionHandler.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Provides methods to ping Photon Cloud regions.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif

#if UNITY_WEBGL
#define PING_VIA_COROUTINE
#endif

namespace Photon.Realtime
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Net;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Photon.Client;
    using System.Linq;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    #endif
    #if SUPPORTED_UNITY || NETFX_CORE
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>
    /// Provides methods to work with Photon's regions (Photon Cloud) and can be use to find the one with best ping.
    /// </summary>
    /// <remarks>
    /// When a client uses a Name Server to fetch the list of available regions, the RealtimeClient will create a RegionHandler
    /// and provide it via the OnRegionListReceived callback, as soon as the list is available. No pings were sent for Best Region selection yet.
    ///
    /// Your logic can decide to either connect to one of those regional servers, or it may use PingMinimumOfRegions to test
    /// which region provides the best ping. Alternatively the client may be set to connect to the Best Region (lowest ping), one or
    /// more regions get pinged.
    /// Not all regions will be pinged. As soon as the results are final, the client will connect to the best region,
    /// so you can check the ping results when connected to the Master Server.
    ///
    /// Regions gets pinged 5 times (RegionPinger.Attempts).
    /// Out of those, the worst rtt is discarded and the best will be counted two times for a weighted average.
    ///
    /// Usually UDP will be used to ping the Master Servers. In WebGL, WSS is used instead.
    ///
    /// It makes sense to make clients "sticky" to a region when one gets selected.
    /// This can be achieved by storing the SummaryToCache value, once pinging was done.
    /// When the client connects again, the previous SummaryToCache helps limiting the number of regions to ping.
    /// In best case, only the previously selected region gets re-pinged and if the current ping is not much worse, this one region is used again.
    /// </remarks>
    public class RegionHandler
    {
        /// <summary>The implementation of PhotonPing to use for region pinging (Best Region detection).</summary>
        /// <remarks>Defaults to null, which means the Type is set automatically.</remarks>
        public static Type PingImplementation;

        /// <summary>A list of region names for the Photon Cloud. Set by the result of OpGetRegions().</summary>
        /// <remarks>
        /// Implement IConnectionCallbacks and register for the callbacks to get OnRegionListReceived callbacks.
        /// You can also put a "case OperationCode.GetRegions:" into your OnOperationResponse method to notice when the result is available.
        /// </remarks>
        public List<Region> EnabledRegions { get; protected internal set; }


        /// <summary>Comma separated string of regions available to this client.</summary>
        public string AvailableRegionCodes
        {
            private set;
            get;
        }

        private Region bestRegionCache;

        /// <summary>
        /// When PingMinimumOfRegions was called and completed, the BestRegion is identified by best ping.
        /// </summary>
        public Region BestRegion
        {
            get
            {
                if (this.EnabledRegions == null)
                {
                    return null;
                }

                if (this.bestRegionCache != null)
                {
                    return this.bestRegionCache;
                }

                this.EnabledRegions.Sort((a, b) => a.Ping.CompareTo(b.Ping));
                
                // in some locations, clients will get very similar results to various regions.
                // in those places, it is best to select alphabetical from those with very similar ping.
                int similarPingCutoff = (int)(this.EnabledRegions[0].Ping * pingSimilarityFactor);
                Region firstFromSimilar = this.EnabledRegions[0];
                foreach (Region region in this.EnabledRegions)
                {
                    if (region.Ping <= similarPingCutoff && region.Code.CompareTo(firstFromSimilar.Code) < 0)
                    {
                        firstFromSimilar = region;
                    }
                }

                this.bestRegionCache = firstFromSimilar;
                return this.bestRegionCache;
            }
        }

        /// <summary>
        /// This value summarizes the results of pinging currently available regions (after PingMinimumOfRegions finished).
        /// </summary>
        /// <remarks>
        /// This value should be stored in the client by the game logic.
        /// When connecting again, use it as previous summary to speed up pinging regions and to make the best region sticky for the client.
        /// </remarks>
        public string SummaryToCache
        {
            get
            {
                if (this.BestRegion != null && this.BestRegion.Ping < RegionPinger.MaxMillisecondsPerPing)
                {
                    return $"{this.BestRegion.Code};{this.BestRegion.Ping};{this.AvailableRegionCodes}";
                }

                return this.AvailableRegionCodes;
            }
        }

        /// <summary>Provides a list of regions and their pings as string.</summary>
        public string GetResults()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("Summary: {0}\n", this.SummaryToCache);
            foreach (RegionPinger region in this.pingerList)
            {
                sb.AppendLine(region.GetResults());
            }

            string summaryString = this.previousSummaryProvided ?? "N/A";
            sb.AppendFormat("Previous summary: {0}", summaryString);

            return sb.ToString();
        }

        /// <summary>Initializes the regions of this RegionHandler with values provided from the Name Server (as OperationResponse for OpGetRegions).</summary>
        public void SetRegions(OperationResponse opGetRegions)
        {
            if (opGetRegions.OperationCode != OperationCode.GetRegions)
            {
                return;
            }

            if (opGetRegions.ReturnCode != ErrorCode.Ok)
            {
                return;
            }

            string[] regions = opGetRegions[ParameterCode.Region] as string[];
            string[] servers = opGetRegions[ParameterCode.Address] as string[];
            if (regions == null || servers == null || regions.Length != servers.Length)
            {
                //TODO: log error
                //Debug.LogError("The region arrays from Name Server are not ok. Must be non-null and same length. " + (regions == null) + " " + (servers == null) + "\n" + opGetRegions.ToStringFull());
                return;
            }

            this.bestRegionCache = null;
            this.EnabledRegions = new List<Region>(regions.Length);

            for (int i = 0; i < regions.Length; i++)
            {
                Region tmp = new Region(regions[i], servers[i]);
                if (string.IsNullOrEmpty(tmp.Code))
                {
                    continue;
                }

                this.EnabledRegions.Add(tmp);
            }

            Array.Sort(regions);
            this.AvailableRegionCodes = string.Join(",", regions);
        }

        private readonly List<RegionPinger> pingerList = new List<RegionPinger>();
        private Action<RegionHandler> onCompleteCall;
        private int previousPing;
        private string previousSummaryProvided;


        /// <summary>If the previous Best Region's ping is now higher by this much, ping all regions and find a new Best Region.</summary>
        private float rePingFactor = 1.2f;
        
        /// <summary>How much higher a region's ping can be from the absolute best, to be considered the Best Region (by ping and name).</summary>
        private float pingSimilarityFactor = 1.2f;

        /// <summary>If the region from a previous BestRegionSummary now has a ping higher than this limit, all regions get pinged again to find a better. Default: 90ms.</summary>
        /// <remarks>
        /// Pinging all regions takes time, which is why a BestRegionSummary gets stored.
        /// If that is available, the Best Region becomes sticky and is used again.
        /// This limit introduces an exception: Should the pre-defined best region have a ping worse than this, all regions are considered.
        /// </remarks>
        public int BestRegionSummaryPingLimit = 90;


        /// <summary>If non-zero, this port will be used to ping Master Servers on.</summary>
        protected internal static ushort UdpPortToPing;

        /// <summary>True if the available regions are being pinged currently.</summary>
        public bool IsPinging { get; private set; }

        /// <summary>True if the pinging of regions is being aborted.</summary>
        /// <see cref="Abort"/>
        public bool Aborted { get; private set; }

        #if SUPPORTED_UNITY
        private MonoBehaviourEmpty emptyMonoBehavior;
        #endif

        /// <summary>Creates a new RegionHandler.</summary>
        /// <param name="masterServerUdpPort">If non-zero, this port will be used to ping Master Servers on.</param>
        public RegionHandler(ushort masterServerUdpPort = 0)
        {
            UdpPortToPing = masterServerUdpPort;
        }


        /// <summary>Starts the process of pinging the available regions.</summary>
        /// <remarks>Uses PingMinimumOfRegions without a summary.</remarks>
        /// <param name="onCompleteCallback">Provide a method to call when all ping results are available. Aborting the pings will also cancel the callback.</param>
        /// <returns>If pining the regions gets started now. False if the current state prevent this.</returns>
        public bool PingAvailableRegions(Action<RegionHandler> onCompleteCallback)
        {
            return this.PingMinimumOfRegions(onCompleteCallback, null);
        }

        /// <summary>Starts the process of pinging a subset of the available regions.</summary>
        /// <param name="onCompleteCallback">Provide a method to call when all ping results are available. Aborting the pings will also cancel the callback.</param>
        /// <param name="previousSummary">A BestRegionSummary from an earlier RegionHandler run. This makes a selected best region "sticky" and keeps ping times lower.</param>
        /// <returns>If pining the regions gets started now. False if the current state prevent this.</returns>
        public bool PingMinimumOfRegions(Action<RegionHandler> onCompleteCallback, string previousSummary)
        {
            if (this.EnabledRegions == null || this.EnabledRegions.Count == 0)
            {
                //TODO: log error
                //Debug.LogError("No regions available. Maybe all got filtered out or the AppId is not correctly configured.");
                return false;
            }

            if (this.IsPinging)
            {
                //TODO: log warning
                //Debug.LogWarning("PingMinimumOfRegions() skipped, because this RegionHandler is already pinging some regions.");
                return false;
            }

            this.Aborted = false;
            this.IsPinging = true;
            this.previousSummaryProvided = previousSummary;

            #if SUPPORTED_UNITY
            if (this.emptyMonoBehavior != null)
            {
                this.emptyMonoBehavior.SelfDestroy();
            }
            this.emptyMonoBehavior = MonoBehaviourEmpty.BuildInstance(nameof(RegionHandler));
            this.emptyMonoBehavior.onCompleteCall = onCompleteCallback;
            this.onCompleteCall = emptyMonoBehavior.CompleteOnMainThread;
            #else
            this.onCompleteCall = onCompleteCallback;
            #endif

            if (string.IsNullOrEmpty(previousSummary))
            {
                return this.PingEnabledRegions();
            }

            string[] values = previousSummary.Split(';');
            if (values.Length < 3)
            {
                return this.PingEnabledRegions();
            }

            int prevBestRegionPing;
            bool secondValueIsInt = Int32.TryParse(values[1], out prevBestRegionPing);
            if (!secondValueIsInt)
            {
                return this.PingEnabledRegions();
            }

            string prevBestRegionCode = values[0];
            string prevAvailableRegionCodes = values[2];


            if (string.IsNullOrEmpty(prevBestRegionCode))
            {
                return this.PingEnabledRegions();
            }
            if (string.IsNullOrEmpty(prevAvailableRegionCodes))
            {
                return this.PingEnabledRegions();
            }
            if (!this.AvailableRegionCodes.Equals(prevAvailableRegionCodes) || !this.AvailableRegionCodes.Contains(prevBestRegionCode))
            {
                return this.PingEnabledRegions();
            }
            if (prevBestRegionPing >= RegionPinger.PingWhenFailed)
            {
                return this.PingEnabledRegions();
            }

            // let's check only the preferred region to detect if it's still "good enough"
            this.previousPing = prevBestRegionPing;


            Region preferred = this.EnabledRegions.Find(r => r.Code.Equals(prevBestRegionCode));
            RegionPinger singlePinger = new RegionPinger(preferred, this.OnPreferredRegionPinged);

            lock (this.pingerList)
            {
                this.pingerList.Clear();
                this.pingerList.Add(singlePinger);
            }

            singlePinger.Start();
            return true;
        }

        /// <summary>Calling this will stop pinging the regions and suppress the onComplete callback.</summary>
        public void Abort()
        {
            if (this.Aborted)
            {
                return;
            }

            this.Aborted = true;
            lock (this.pingerList)
            {
                foreach (RegionPinger pinger in this.pingerList)
                {
                    pinger.Abort();
                }
            }

            #if SUPPORTED_UNITY
            if (this.emptyMonoBehavior != null)
            {
                this.emptyMonoBehavior.SelfDestroy();
            }
            #endif
        }

        private void OnPreferredRegionPinged(Region preferredRegion)
        {
            if (preferredRegion.Ping > this.BestRegionSummaryPingLimit || preferredRegion.Ping > this.previousPing * this.rePingFactor)
            {
                this.PingEnabledRegions();
            }
            else
            {
                this.IsPinging = false;
                this.onCompleteCall(this);
            }
        }


        /// <summary>Privately used to ping regions if the current best one isn't as fast as earlier.</summary>
        /// <returns>If pinging can be started.</returns>
        private bool PingEnabledRegions()
        {
            if (this.EnabledRegions == null || this.EnabledRegions.Count == 0)
            {
                //TODO: log
                //Debug.LogError("No regions available. Maybe all got filtered out or the AppId is not correctly configured.");
                return false;
            }

            lock (this.pingerList)
            {
                this.pingerList.Clear();

                foreach (Region region in this.EnabledRegions)
                {
                    RegionPinger rp = new RegionPinger(region, this.OnRegionDone);
                    this.pingerList.Add(rp);
                    rp.Start(); // TODO: check return value
                }
            }

            return true;
        }

        private void OnRegionDone(Region region)
        {
            lock (this.pingerList)
            {
                if (this.IsPinging == false)
                {
                    return;
                }

                this.bestRegionCache = null;
                foreach (RegionPinger pinger in this.pingerList)
                {
                    if (!pinger.Done)
                    {
                        return;
                    }
                }

                this.IsPinging = false;
            }

            if (!this.Aborted)
            {
                this.onCompleteCall(this);
            }
        }
    }

    /// <summary>Wraps the ping attempts and workflow for a single region.</summary>
    public class RegionPinger
    {
        /// <summary>How often to ping a region.</summary>
        public static int Attempts = 5;
        /// <summary>How long to wait maximum for a response.</summary>
        public static int MaxMillisecondsPerPing = 800; // enter a value you're sure some server can beat (have a lower rtt)
        /// <summary>Ping result when pinging failed.</summary>
        public static int PingWhenFailed = Attempts * MaxMillisecondsPerPing;

        /// <summary>Current ping attempt count.</summary>
        public int CurrentAttempt = 0;
        /// <summary>True if all attempts are done or timed out.</summary>
        public bool Done { get; private set; }
        /// <summary>Set to true to abort pining this region.</summary>
        public bool Aborted { get; internal set; }


        private Action<Region> onDoneCall;
        private PhotonPing ping;
        private List<int> rttResults;
        private Region region;
        private string regionAddress;


        /// <summary>Initializes a RegionPinger for the given region.</summary>
        public RegionPinger(Region region, Action<Region> onDoneCallback)
        {
            this.region = region;
            this.region.Ping = PingWhenFailed;
            this.Done = false;
            this.onDoneCall = onDoneCallback;
        }

        /// <summary>Selects the best fitting ping implementation or uses the one set in RegionHandler.PingImplementation.</summary>
        /// <returns>PhotonPing instance to use.</returns>
        private PhotonPing GetPingImplementation()
        {
            PhotonPing ping = null;

            // using each type explicitly in the conditional code, makes sure Unity doesn't strip the class / constructor.

            #if NATIVE_SOCKETS || NO_SOCKET
            if (RegionHandler.PingImplementation == null || RegionHandler.PingImplementation == typeof(PingNativeDynamic))
            {
                ping = new PingNativeDynamic();
            }
            #elif UNITY_WEBGL
            if (RegionHandler.PingImplementation == null || RegionHandler.PingImplementation == typeof(PingHttp))
            {
                ping = new PingHttp();
            }
            #else
            if (RegionHandler.PingImplementation == null || RegionHandler.PingImplementation == typeof(PingMono))
            {
                ping = new PingMono();
            }
            #endif

            if (ping == null)
            {
                if (RegionHandler.PingImplementation != null)
                {
                    ping = (PhotonPing)Activator.CreateInstance(RegionHandler.PingImplementation);
                }
            }

            return ping;
        }


        /// <summary>
        /// Starts the ping routine for the assigned region.
        /// </summary>
        /// <remarks>
        /// Pinging runs in a ThreadPool worker item or (if needed) in a Thread.
        /// WebGL runs pinging on the Main Thread as coroutine.
        /// </remarks>
        /// <returns>True unless Aborted.</returns>
        public bool Start()
        {
            // all addresses for Photon region servers will contain a :port ending. this needs to be removed first.
            // PhotonPing.StartPing() requires a plain (IP) address without port or protocol-prefix (on all but Windows 8.1 and WebGL platforms).
            string address = this.region.HostAndPort;
            int indexOfColon = address.LastIndexOf(':');
            if (indexOfColon > 1)
            {
                address = address.Substring(0, indexOfColon);
            }
            this.regionAddress = ResolveHost(address);


            this.ping = this.GetPingImplementation();


            this.Done = false;
            this.CurrentAttempt = 0;
            this.rttResults = new List<int>(Attempts);

            if (this.Aborted)
            {
                return false;
            }

            #if PING_VIA_COROUTINE
            MonoBehaviourEmpty.BuildInstance("RegionPing_" + this.region.Code).StartCoroutineAndDestroy(this.RegionPingCoroutine());
            #else
            bool queued = false;
            #if !NETFX_CORE
            try
            {
                queued = ThreadPool.QueueUserWorkItem(o => this.RegionPingThreaded());
            }
            catch
            {
                queued = false;
            }
            #endif
            if (!queued)
            {
                Log.Error("RegionPinger.Start() failed. Could not queue region pinging. Please contact us.");
                return false;
            }
            #endif


            return true;
        }

        /// <summary>Calling this will stop pinging the regions and cancel the onComplete callback.</summary>
        protected internal void Abort()
        {
            this.Aborted = true;
            if (this.ping != null)
            {
                this.ping.Dispose();
            }
        }

        /// <summary>Pings the region. To be called by a thread.</summary>
        protected internal bool RegionPingThreaded()
        {
            this.region.Ping = PingWhenFailed;

            int rttSum = 0;
            int replyCount = 0;


            Stopwatch sw = new Stopwatch();
            for (this.CurrentAttempt = 0; this.CurrentAttempt < Attempts; this.CurrentAttempt++)
            {
                if (this.Aborted)
                {
                    break;
                }

                sw.Reset();
                sw.Start();

                try
                {
                    this.ping.StartPing(this.regionAddress);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("RegionPinger.RegionPingThreaded() caught exception for ping.StartPing(). Exception: " + e + " Source: " + e.Source + " Message: " + e.Message);
                    break;
                }


                while (!this.ping.Done())
                {
                    if (sw.ElapsedMilliseconds >= MaxMillisecondsPerPing)
                    {
                        // if ping.Done() did not become true in MaxMillisecondsPerPing, ping.Successful is false and we apply MaxMillisecondsPerPing as rtt below
                        break;
                    }
                    #if !NETFX_CORE
                    System.Threading.Thread.Sleep(1);
                    #endif
                }


                sw.Stop();
                int rtt = this.ping.Successful ? (int)sw.ElapsedMilliseconds : MaxMillisecondsPerPing;   // if the reply didn't match the sent ping
                this.rttResults.Add(rtt);

                rttSum += rtt;
                replyCount++;
                this.region.Ping = (int)((rttSum) / replyCount);

                #if !NETFX_CORE
                int i = 4;
                while (!this.ping.Done() && i > 0)
                {
                    i--;
                    System.Threading.Thread.Sleep(100);
                }
                System.Threading.Thread.Sleep(10);
                #endif
            }


            //Debug.Log("Done: "+ this.region.Code);
            this.Done = true;
            this.ping.Dispose();

            if (this.rttResults.Count > 1 && replyCount > 0)
            {
                int bestRtt = this.rttResults.Min();
                int worstRtt = this.rttResults.Max();
                int weighedRttSum = rttSum - worstRtt + bestRtt;
                this.region.Ping = (int)(weighedRttSum / replyCount); // now, we can create a weighted ping value
            }

            this.onDoneCall(this.region);
            return false;
        }


        #if SUPPORTED_UNITY

        /// <remarks>
        /// Affected by frame-rate of app, as this Coroutine checks the socket for a result once per frame.
        /// </remarks>
        protected internal IEnumerator RegionPingCoroutine()
        {
            this.region.Ping = PingWhenFailed;

            int rttSum = 0;
            int replyCount = 0;


            Stopwatch sw = new Stopwatch();
            for (this.CurrentAttempt = 0; this.CurrentAttempt < Attempts; this.CurrentAttempt++)
            {
                if (this.Aborted)
                {
                    yield return null;
                }

                sw.Reset();
                sw.Start();

                try
                {
                    this.ping.StartPing(this.regionAddress);
                }
                catch (Exception e)
                {
                    Debug.Log("RegionPinger.RegionPingCoroutine() caught exception for ping.StartPing(). Exception: " + e + " Source: " + e.Source + " Message: " + e.Message);
                    break;
                }


                while (!this.ping.Done())
                {
                    if (sw.ElapsedMilliseconds >= MaxMillisecondsPerPing)
                    {
                        // if ping.Done() did not become true in MaxMilliseconsPerPing, ping.Successful is false and we apply MaxMilliseconsPerPing as rtt below
                        break;
                    }

                    yield return new WaitForSecondsRealtime(0.01f); // keep this loop tight, to avoid adding local lag to rtt.
                }


                sw.Stop();
                int rtt = this.ping.Successful ? (int)sw.ElapsedMilliseconds : MaxMillisecondsPerPing; // if the reply didn't match the sent ping
                this.rttResults.Add(rtt);


                rttSum += rtt;
                replyCount++;
                this.region.Ping = (int)((rttSum) / replyCount);

                int i = 4;
                while (!this.ping.Done() && i > 0)
                {
                    i--;
                    yield return new WaitForSeconds(0.1f);
                }

                yield return new WaitForSeconds(0.1f);
            }


            //Debug.Log("Done: "+ this.region.Code);
            this.Done = true;
            this.ping.Dispose();

            if (this.rttResults.Count > 1 && replyCount > 0)
            {
                int bestRtt = this.rttResults.Min();
                int worstRtt = this.rttResults.Max();
                int weighedRttSum = rttSum - worstRtt + bestRtt;
                this.region.Ping = (int)(weighedRttSum / replyCount); // now, we can create a weighted ping value
            }

            this.onDoneCall(this.region);
            yield return null;
        }
        #endif


        /// <summary>Gets this region's results as string summary.</summary>
        public string GetResults()
        {
            return string.Format("{0}: {1} ({2})", this.region.Code, this.region.Ping, this.rttResults.ToStringFull());
        }

        /// <summary>
        /// Attempts to resolve a hostname into an IP string or returns empty string if that fails.
        /// </summary>
        /// <remarks>
        /// To be compatible with most platforms, the address family is checked like this:<br/>
        /// if (ipAddress.AddressFamily.ToString().Contains("6")) // ipv6...
        /// </remarks>
        /// <param name="hostName">Hostname to resolve.</param>
        /// <returns>IP string or empty string if resolution fails</returns>
        public static string ResolveHost(string hostName)
        {

			if (hostName.StartsWith("wss://"))
			{
				hostName = hostName.Substring(6);
			}
			if (hostName.StartsWith("ws://"))
			{
				hostName = hostName.Substring(5);
			}

            string ipv4Address = string.Empty;

            try
            {
                #if UNITY_WSA || NETFX_CORE || UNITY_WEBGL
                return hostName;
                #else

                IPAddress[] address = Dns.GetHostAddresses(hostName);
                if (address.Length == 1)
                {
                    return address[0].ToString();
                }

                // if we got more addresses, try to pick a IPv6 one
                // checking ipAddress.ToString() means we don't have to import System.Net.Sockets, which is not available on some platforms (Metro)
                for (int index = 0; index < address.Length; index++)
                {
                    IPAddress ipAddress = address[index];
                    if (ipAddress != null)
                    {
                        if (ipAddress.ToString().Contains(":"))
                        {
                            return ipAddress.ToString();
                        }
                        if (string.IsNullOrEmpty(ipv4Address))
                        {
                            ipv4Address = address.ToString();
                        }
                    }
                }
                #endif
            }
            catch (System.Exception e)
            {
                System.Diagnostics.Debug.WriteLine("RegionPinger.ResolveHost() caught an exception for Dns.GetHostAddresses(). Exception: " + e + " Source: " + e.Source + " Message: " + e.Message);
            }

            return ipv4Address;
        }
    }

    #if SUPPORTED_UNITY
    internal class MonoBehaviourEmpty : MonoBehaviour
    {
        internal Action<RegionHandler> onCompleteCall;
        private RegionHandler obj;

        public static MonoBehaviourEmpty BuildInstance(string id = null)
        {
            GameObject go = new GameObject(id ?? nameof(MonoBehaviourEmpty));
            DontDestroyOnLoad(go);

            return go.AddComponent<MonoBehaviourEmpty>();
        }

        public void SelfDestroy()
        {
            Destroy(this.gameObject);
        }

        void Update()
        {
            if (this.obj != null)
            {
                this.onCompleteCall(obj);
                this.obj = null;
                this.onCompleteCall = null;
                this.SelfDestroy();
            }
        }

        public void CompleteOnMainThread(RegionHandler obj)
        {
            this.obj = obj;
        }

        public void StartCoroutineAndDestroy(IEnumerator coroutine)
        {
            StartCoroutine(Routine());

            IEnumerator Routine()
            {
                yield return coroutine;
                this.SelfDestroy();
            }
        }
    }
    #endif
}
