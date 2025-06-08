using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LevelWrapRenderPass : ScriptableRenderPass {

    public static float wrapAmount = 0f; // Set this from your LevelManager or similar

    private FilteringSettings filteringSettings;
    private ShaderTagId shaderTagId = new ShaderTagId("SRPDefaultUnlit");

    public LevelWrapRenderPass() {
        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

        // Draw everything in the default opaque queue
        filteringSettings = new FilteringSettings(RenderQueueRange.all);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
        if (Mathf.Approximately(wrapAmount, 0f)) {
            return;
        }

        Camera camera = renderingData.cameraData.camera;
        var cmd = CommandBufferPool.Get("Horizontal Wrap Pass");

        // We'll render to the same depth buffer, so re-use projection matrix
        Matrix4x4 originalViewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 originalProjectionMatrix = camera.projectionMatrix;

        var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);

        // Draw wrapped copies to the left and right
        for (int i = -1; i <= 1; i++) {
            if (i == 0) {
                continue;
            }

            float thisWrapAmount = wrapAmount * i;
            Matrix4x4 offsetMatrix = Matrix4x4.Translate(Vector3.right * thisWrapAmount);
            Matrix4x4 newViewMatrix = originalViewMatrix * offsetMatrix;

            camera.TryGetCullingParameters(out ScriptableCullingParameters shiftedCullingParams);

            // Culling plane
            for (int j = 0; j < shiftedCullingParams.cullingPlaneCount; j++) {
                Plane plane = shiftedCullingParams.GetCullingPlane(j);
                plane.distance += thisWrapAmount * plane.normal.x;
                shiftedCullingParams.SetCullingPlane(j, plane);
            }

            // Culling matrix.
            shiftedCullingParams.cullingMatrix = originalProjectionMatrix * Matrix4x4.Translate(Vector3.left * thisWrapAmount) * originalViewMatrix;

            // Re-cull
            CullingResults shiftedCullingResults = context.Cull(ref shiftedCullingParams);

            // Set new matrices
            cmd.SetViewProjectionMatrices(newViewMatrix, originalProjectionMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            RendererListParams renderParams = new RendererListParams {
                cullingResults = shiftedCullingResults,
                drawSettings = drawSettings,
                filteringSettings = filteringSettings,
            };
            RendererList rendererList = context.CreateRendererList(ref renderParams);
            cmd.DrawRendererList(rendererList);
        }

        // Reset matrices
        cmd.SetViewProjectionMatrices(originalViewMatrix, originalProjectionMatrix);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}