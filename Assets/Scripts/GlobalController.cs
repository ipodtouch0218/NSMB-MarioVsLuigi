using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using NSMB.Utils;

public class GlobalController : Singleton<GlobalController> {

    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;

    public RenderTexture ndsTexture;
    public PlayerData[] characters;
    public Settings settings;
    public DiscordController discordController;
    public string controlsJson = null;

    public bool joinedAsSpectator = false, checkedForVersion;
    public DisconnectCause? disconnectCause = null;

    private int windowWidth, windowHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
        settings = GetComponent<Settings>();
        discordController = GetComponent<DiscordController>();
    }

    [Obsolete]
    void Start() {
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
        IntPtr hwnd = GetActiveWindow();
        // Get pointer to our replacement WndProc.
        WndProcDelegate newWndProc = new(WndProc);
        IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
        // Override Unity's WndProc with our custom one, and save the original WndProc (Unity's) so we can use it later for non-focus related messages.
        oldWndProcPtr = SetWindowLongPtr(hwnd, -4, newWndProcPtr); // (GWLP_WNDPROC == -4)
    }
    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
        if (msg == 0x00a4) {
            SendMessage(hWnd, 0x001F, 0, 0);
            return IntPtr.Zero;
        }
        if (msg == 0x0010) {
            //close
            Application.Quit();
            return IntPtr.Zero;
        }
        if (msg != 0x0117) return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);

        if (lParam.ToInt32() >> 16 == 1) {
            SendMessage(hWnd, 0x001F, 0, 0);
        }
        return IntPtr.Zero;
    }
#endif

    void Update() {
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

}
