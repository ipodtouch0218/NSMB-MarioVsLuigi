using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using NSMB.Utils;

public class GlobalController : Singleton<GlobalController>, IInRoomCallbacks, ILobbyCallbacks {

    public PlayerColorSet[] skins;
    public Gradient rainbowGradient;

    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;

    public RenderTexture ndsTexture;
    public PlayerData[] characters;
    public Settings settings;
    public DiscordController DiscordController { get; private set; }
    public string controlsJson = null;

    public bool joinedAsSpectator = false, checkedForVersion;
    public DisconnectCause? disconnectCause = null;

    private int windowWidth, windowHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
        settings = GetComponent<Settings>();
        DiscordController = GetComponent<DiscordController>();

        PhotonNetwork.AddCallbackTarget(this);
    }


    [Obsolete]
    public void Start() {
        //Photon settings.
        PhotonPeer.RegisterType(typeof(NameIdPair), 69, NameIdPair.Serialize, NameIdPair.Deserialize);
        PhotonNetwork.SerializationRate = 30;
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.MaxResendsBeforeDisconnect = 15;

        InputSystem.controls.UI.DebugInfo.performed += (context) => {
            graphy.SetActive(!graphy.activeSelf);
        };

#if PLATFORM_STANDALONE_WIN && !UNITY_EDITOR
        try {
            ReplaceWinProc();
        } catch {}
#endif
    }

#if PLATFORM_STANDALONE_WIN
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);
    [DllImport("user32.dll")]
    static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    static IntPtr oldWndProcPtr;

    private static void ReplaceWinProc() {
        // get window that we're using
        IntPtr hwnd = GetActiveWindow();
        // Get pointer to our replacement WndProc.
        WndProcDelegate newWndProc = new(WndProc);
        IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
        // Override Unity's WndProc with our custom one, and save the original WndProc (Unity's) so we can use it later for non-focus related messages.
        oldWndProcPtr = SetWindowLongPtr(hwnd, -4, newWndProcPtr); // (GWLP_WNDPROC == -4)
    }
    private const uint WM_INITMENUPOPUP = 0x0117;
    private const uint WM_CLOSE = 0x0010;
    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {

        switch (msg) {
        case WM_INITMENUPOPUP: {
            //prevent menu bar (the one that appears when right click top bar, has "move" etc
            //from appearing, to avoid the game pausing when the menu bar is active

            //bit 16 = top menu bar
            if (lParam.ToInt32() >> 16 == 1) {
                //cancel the menu from popping up
                SendMessage(hWnd, 0x001F, 0, 0);
                return IntPtr.Zero;
            }
            break;
        }
        case WM_CLOSE: {
            //we're closing, pass back to our existing wndproc to avoid crashing
            SetWindowLongPtr(hWnd, -4, oldWndProcPtr);
            break;
        }
        }

        return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
    }
#endif

    public void Update() {
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        if (settings.ndsResolution && SceneManager.GetActiveScene().buildIndex != 0) {
            float aspect = (float) currentWidth / currentHeight;
            int targetHeight = 224;
            int targetWidth = (int) (targetHeight * (settings.fourByThreeRatio ? (4/3f) : aspect));
            if (ndsTexture == null || ndsTexture.width != targetWidth || ndsTexture.height != targetHeight) {
                if (ndsTexture != null)
                    ndsTexture.Release();
                ndsTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
                ndsTexture.filterMode = FilterMode.Point;
                ndsTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
            }
            ndsCanvas.SetActive(true);
        } else {
            ndsCanvas.SetActive(false);
        }

        //todo: this jitters to hell
#if UNITY_STANDALONE
        if (Screen.fullScreenMode == FullScreenMode.Windowed && Keyboard.current[Key.LeftShift].isPressed && (windowWidth != currentWidth || windowHeight != currentHeight)) {
            currentHeight = (int) (currentWidth * (9f / 16f));
            Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.Windowed);
        }
        windowWidth = currentWidth;
        windowHeight = currentHeight;
#endif
    }

    public void OnPlayerEnteredRoom(Player newPlayer) {
        NetworkUtils.nicknameCache.Remove(newPlayer.UserId);
    }

    public void OnPlayerLeftRoom(Player otherPlayer) {
        NetworkUtils.nicknameCache.Remove(otherPlayer.UserId);
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

    public void OnMasterClientSwitched(Player newMasterClient) { }

    public void OnJoinedLobby() {
        NetworkUtils.nicknameCache.Clear();
    }

    public void OnLeftLobby() {
        NetworkUtils.nicknameCache.Clear();
    }

    public void OnRoomListUpdate(List<RoomInfo> roomList) { }

    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) { }
}
