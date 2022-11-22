using UnityEngine;

namespace Fusion {

  static class FusionRuntimeCheck {

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeCheck() {
      RuntimeUnityFlagsSetup.Check_ENABLE_IL2CPP();
      RuntimeUnityFlagsSetup.Check_ENABLE_MONO();

      RuntimeUnityFlagsSetup.Check_UNITY_EDITOR();
      RuntimeUnityFlagsSetup.Check_UNITY_GAMECORE();
      RuntimeUnityFlagsSetup.Check_UNITY_SWITCH();
      RuntimeUnityFlagsSetup.Check_UNITY_WEBGL();
      RuntimeUnityFlagsSetup.Check_UNITY_XBOXONE();

      RuntimeUnityFlagsSetup.Check_NETFX_CORE();
      RuntimeUnityFlagsSetup.Check_NET_4_6();
      RuntimeUnityFlagsSetup.Check_NET_STANDARD_2_0();

      RuntimeUnityFlagsSetup.Check_UNITY_2019_4_OR_NEWER();
    }
  }
}