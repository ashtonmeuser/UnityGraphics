using System.Collections.Generic;
using System.Net.Mime;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    // Render all tiled-based deferred lights.
    internal class GBufferPass : ScriptableRenderPass
    {
        static readonly int s_CameraNormalsTextureID = Shader.PropertyToID("_CameraNormalsTexture");
        static ShaderTagId s_ShaderTagLit = new ShaderTagId("Lit");
        static ShaderTagId s_ShaderTagSimpleLit = new ShaderTagId("SimpleLit");
        static ShaderTagId s_ShaderTagUnlit = new ShaderTagId("Unlit");
        static ShaderTagId s_ShaderTagUniversalGBuffer = new ShaderTagId("UniversalGBuffer");
        static ShaderTagId s_ShaderTagUniversalMaterialType = new ShaderTagId("UniversalMaterialType");

        ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Render GBuffer");

        DeferredLights m_DeferredLights;

        static ShaderTagId[] s_ShaderTagValues;
        static RenderStateBlock[] s_RenderStateBlocks;

        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;

        public GBufferPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference, DeferredLights deferredLights)
        {
            base.profilingSampler = new ProfilingSampler(nameof(GBufferPass));
            base.renderPassEvent = evt;

            m_DeferredLights = deferredLights;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            m_RenderStateBlock.stencilState = stencilState;
            m_RenderStateBlock.stencilReference = stencilReference;
            m_RenderStateBlock.mask = RenderStateMask.Stencil;

            s_ShaderTagValues = new ShaderTagId[4];
            s_ShaderTagValues[0] = s_ShaderTagLit;
            s_ShaderTagValues[1] = s_ShaderTagSimpleLit;
            s_ShaderTagValues[2] = s_ShaderTagUnlit;
            s_ShaderTagValues[3] = new ShaderTagId(); // Special catch all case for materials where UniversalMaterialType is not defined or the tag value doesn't match anything we know.

            s_RenderStateBlocks = new RenderStateBlock[4];
            s_RenderStateBlocks[0] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialLit);
            s_RenderStateBlocks[1] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialSimpleLit);
            s_RenderStateBlocks[2] = DeferredLights.OverwriteStencil(m_RenderStateBlock, (int)StencilUsage.MaterialMask, (int)StencilUsage.MaterialUnlit);
            s_RenderStateBlocks[3] = s_RenderStateBlocks[0];
        }

        public void Dispose()
        {
            if (m_DeferredLights.GbufferAttachments != null)
            {
                foreach (var attachment in m_DeferredLights.GbufferAttachments)
                    attachment?.Release();
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RTHandle[] gbufferAttachments = m_DeferredLights.GbufferAttachments;

            if (cmd != null)
            {
                var allocateGbufferDepth = true;
                if (m_DeferredLights.UseRenderPass && (m_DeferredLights.DepthCopyTexture != null && m_DeferredLights.DepthCopyTexture.rt != null))
                {
                    m_DeferredLights.GbufferAttachments[m_DeferredLights.GbufferDepthIndex] = m_DeferredLights.DepthCopyTexture;
                    allocateGbufferDepth = false;
                }
                // Create and declare the render targets used in the pass
                for (int i = 0; i < gbufferAttachments.Length; ++i)
                {
                    // Lighting buffer has already been declared with line ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(), ...) in DeferredRenderer.Setup
                    if (i == m_DeferredLights.GBufferLightingIndex)
                        continue;

                    // Normal buffer may have already been created if there was a depthNormal prepass before.
                    // DepthNormal prepass is needed for forward-only materials when SSAO is generated between gbuffer and deferred lighting pass.
                    if (i == m_DeferredLights.GBufferNormalSmoothnessIndex && m_DeferredLights.HasNormalPrepass)
                        continue;

                    if (i == m_DeferredLights.GbufferDepthIndex && !allocateGbufferDepth)
                        continue;

                    // No need to setup temporaryRTs if we are using input attachments as they will be Memoryless
                    if (m_DeferredLights.UseRenderPass && i != m_DeferredLights.GBufferShadowMask && i != m_DeferredLights.GBufferRenderingLayers && (i != m_DeferredLights.GbufferDepthIndex && !m_DeferredLights.HasDepthPrepass))
                        continue;

                    RenderTextureDescriptor gbufferSlice = cameraTextureDescriptor;
                    gbufferSlice.depthBufferBits = 0; // make sure no depth surface is actually created
                    gbufferSlice.stencilFormat = GraphicsFormat.None;
                    gbufferSlice.graphicsFormat = m_DeferredLights.GetGBufferFormat(i);
                    RenderingUtils.ReAllocateIfNeeded(ref m_DeferredLights.GbufferAttachments[i], gbufferSlice, FilterMode.Point, TextureWrapMode.Clamp, name: DeferredLights.k_GBufferNames[i]);
                    cmd.SetGlobalTexture(m_DeferredLights.GbufferAttachments[i].name, m_DeferredLights.GbufferAttachments[i].nameID);
                }
            }

            if (m_DeferredLights.UseRenderPass)
                m_DeferredLights.UpdateDeferredInputAttachments();

            ConfigureTarget(m_DeferredLights.GbufferAttachments, m_DeferredLights.DepthAttachment, m_DeferredLights.GbufferFormats);

            // We must explicitly specify we don't want any clear to avoid unwanted side-effects.
            // ScriptableRenderer will implicitly force a clear the first time the camera color/depth targets are bound.
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            PassData data = new PassData();
            data.renderingData = renderingData;
            var cmd = renderingData.commandBuffer;
            data.filteringSettings = m_FilteringSettings;
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                data.deferredLights = m_DeferredLights;

                // User can stack several scriptable renderers during rendering but deferred renderer should only lit pixels added by this gbuffer pass.
                // If we detect we are in such case (camera is in overlay mode), we clear the highest bits of stencil we have control of and use them to
                // mark what pixel to shade during deferred pass. Gbuffer will always mark pixels using their material types.


                ref CameraData cameraData = ref renderingData.cameraData;
                ShaderTagId lightModeTag = s_ShaderTagUniversalGBuffer;
                data.drawingSettings = CreateDrawingSettings(lightModeTag, ref data.renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

                ExecutePass(context, data);
            }
        }

        static void ExecutePass(ScriptableRenderContext context, PassData data, bool useRenderGraph = false)
        {
            if (data.deferredLights.IsOverlay)
            {
                data.deferredLights.ClearStencilPartial(data.renderingData.commandBuffer);
                context.ExecuteCommandBuffer(data.renderingData.commandBuffer);
                data.renderingData.commandBuffer.Clear();
            }

            NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(s_ShaderTagValues, Allocator.Temp);
            NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(s_RenderStateBlocks, Allocator.Temp);

            context.DrawRenderers(data.renderingData.cullResults, ref data.drawingSettings, ref data.filteringSettings, s_ShaderTagUniversalMaterialType, false, tagValues, stateBlocks);

            tagValues.Dispose();
            stateBlocks.Dispose();

            // Render objects that did not match any shader pass with error shader
            RenderingUtils.RenderObjectsWithError(context, ref data.renderingData.cullResults, data.renderingData.cameraData.camera, data.filteringSettings, SortingCriteria.None);

            // If any sub-system needs camera normal texture, make it available.
            // Input attachments will only be used when this is not needed so safe to skip in that case
            if (!data.deferredLights.UseRenderPass)
                data.renderingData.commandBuffer.SetGlobalTexture(s_CameraNormalsTextureID, useRenderGraph ? data.gbuffer[data.deferredLights.GBufferNormalSmoothnessIndex] : data.deferredLights.GbufferAttachments[data.deferredLights.GBufferNormalSmoothnessIndex]);
        }

        class PassData
        {
            public TextureHandle[] gbuffer;
            public TextureHandle depth;

            public RenderingData renderingData;

            public DeferredLights deferredLights;
            public FilteringSettings filteringSettings;
            public DrawingSettings drawingSettings;
        }

        internal void Render(TextureHandle cameraColor, TextureHandle cameraDepth,
            ref RenderingData renderingData, ref UniversalRenderer.RenderGraphFrameResources frameResources)
        {
            RenderGraph graph = renderingData.renderGraph;

            using (var builder = graph.AddRenderPass<PassData>("GBuffer Pass", out var passData, m_ProfilingSampler))
            {
                passData.gbuffer = frameResources.gbuffer = m_DeferredLights.GbufferTextureHandles;
                for (int i = 0; i < m_DeferredLights.GBufferSliceCount; i++)
                {
                    var gbufferSlice = renderingData.cameraData.cameraTargetDescriptor;
                    gbufferSlice.depthBufferBits = 0; // make sure no depth surface is actually created
                    gbufferSlice.stencilFormat = GraphicsFormat.None;

                    if (i != m_DeferredLights.GBufferLightingIndex)
                    {
                        gbufferSlice.graphicsFormat = m_DeferredLights.GetGBufferFormat(i);
                        frameResources.gbuffer[i] = UniversalRenderer.CreateRenderGraphTexture(graph, gbufferSlice, DeferredLights.k_GBufferNames[i], true);
                    }
                    else
                    {
                        frameResources.gbuffer[i] = cameraColor;
                    }
                    passData.gbuffer[i] = builder.UseColorBuffer(frameResources.gbuffer[i], i);
                }

                passData.deferredLights = m_DeferredLights;
                passData.depth = builder.UseDepthBuffer(cameraDepth, DepthAccess.Write);

                passData.renderingData = renderingData;

                builder.AllowPassCulling(false);

                passData.filteringSettings = m_FilteringSettings;
                ShaderTagId lightModeTag = s_ShaderTagUniversalGBuffer;
                passData.drawingSettings = CreateDrawingSettings(lightModeTag, ref passData.renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

                builder.SetRenderFunc((PassData data, RenderGraphContext context) =>
                {
                    ExecutePass(context.renderContext, data, true);
                    for (int i = 0; i < data.gbuffer.Length; i++)
                    {
                        if (i != data.deferredLights.GBufferLightingIndex)
                            data.renderingData.commandBuffer.SetGlobalTexture(DeferredLights.k_GBufferNames[i], data.gbuffer[i]);
                    }
                });

            }
        }
    }
}
