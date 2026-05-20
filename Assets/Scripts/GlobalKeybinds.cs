using NSMB.Addons;
using NSMB.UI.MainMenu.Submenus.Replays;
using Quantum;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace NSMB {
    public class GlobalKeybinds : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject graphy;

        public void Start() {
            Settings.Controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;
        }

        public void OnDestroy() {
            Settings.Controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
        }

        public void Update() {
            var keyboard = Keyboard.current;

            if (keyboard[Key.F3].wasPressedThisFrame) {
                var game = QuantumRunner.DefaultGame;
                if (game != null) {
                    Frame f = game.Frames.Predicted;
                    string dump = f.DumpFrame(Frame.DumpFlag_NoHeap);
                    string path = $"{Application.persistentDataPath}/dumps/frame_dump_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.txt";
                    Debug.Log($"[Debug] Created frame dump for frame {f.Number}- writing to {path}");
                    File.WriteAllText(path, dump);
                    PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                } else {
                    PlaySound(SoundEffect.UI_Error);
                }
            }

#if UNITY_STANDALONE
            if (keyboard[Key.F6].wasPressedThisFrame && !string.IsNullOrEmpty(Application.consoleLogPath)) {
                System.Diagnostics.Process.Start(Path.GetDirectoryName(Application.consoleLogPath));
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }

            if (keyboard[Key.F7].wasPressedThisFrame && !string.IsNullOrEmpty(ReplayListManager.ReplayDirectory)) {
                System.Diagnostics.Process.Start(ReplayListManager.ReplayDirectory);
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }
            
            if (keyboard[Key.F8].wasPressedThisFrame && GlobalController.Instance.addonManager && GlobalController.Instance.addonManager.isActiveAndEnabled && !string.IsNullOrEmpty(AddonManager.LocalFolderPath)) {
                System.Diagnostics.Process.Start(AddonManager.LocalFolderPath);
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }

            if (Debug.isDebugBuild && keyboard[Key.F9].wasPressedThisFrame) {
                if (Profiler.enabled) {
                    Profiler.enabled = false;
                    PlaySound(SoundEffect.Player_Sound_Powerdown);
                } else {
                    Profiler.maxUsedMemory = 256 * 1024 * 1024;
                    Profiler.logFile = "profile-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                }
            }
#endif
        }

        private void ToggleFpsMonitor(InputAction.CallbackContext context) {
            graphy.SetActive(!graphy.activeSelf);
        }

        private void PlaySound(SoundEffect sfx) {
            GlobalController.Instance.PlaySound(sfx);
        }
    }
}