using UnityEngine.Rendering.Universal;

public class LevelWrapRenderFeature : ScriptableRendererFeature {
    private LevelWrapRenderPass wrapPass;

    public override void Create() {
        wrapPass = new LevelWrapRenderPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        renderer.EnqueuePass(wrapPass);
    }
}