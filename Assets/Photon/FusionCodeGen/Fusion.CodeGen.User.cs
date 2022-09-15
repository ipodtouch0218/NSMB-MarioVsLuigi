#if FUSION_WEAVER && FUSION_WEAVER_ILPOSTPROCESSOR
namespace Fusion.CodeGen {
  static partial class ILWeaverSettings {

    static partial void OverrideNetworkProjectConfigPath(ref string path) {
    }
  }
}
#endif