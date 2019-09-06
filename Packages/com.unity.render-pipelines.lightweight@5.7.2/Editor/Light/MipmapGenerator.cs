using System;
using UnityEditor;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.LWRP
{
    using RTHandle = RTHandleSystem.RTHandle;

    public class MipGenerator
    {
        const int kKernelTex2D = 0;
        const int kKernelTex2DArray = 1;
        RTHandle[] m_TempColorTargets;

        ComputeShader m_DepthPyramidCS;
        ComputeShader m_ColorPyramidCS;
        Shader m_ColorPyramidPS;
        Material m_ColorPyramidPSMat;
        MaterialPropertyBlock m_PropertyBlock;

        int m_DepthDownsampleKernel;
        int[] m_ColorDownsampleKernel;
        int[] m_ColorDownsampleKernelCopyMip0;
        int[] m_ColorGaussianKernel;

        int[] m_SrcOffset;
        int[] m_DstOffset;

        #region
        public struct PackedMipChainInfo
        {
            public Vector2Int textureSize;
            public int mipLevelCount;
            public Vector2Int[] mipLevelSizes;
            public Vector2Int[] mipLevelOffsets;

            private bool m_OffsetBufferWillNeedUpdate;

            public void Allocate()
            {
                mipLevelOffsets = new Vector2Int[15];
                mipLevelSizes = new Vector2Int[15];
                m_OffsetBufferWillNeedUpdate = true;
            }

            // We pack all MIP levels into the top MIP level to avoid the Pow2 MIP chain restriction.
            // We compute the required size iteratively.
            // This function is NOT fast, but it is illustrative, and can be optimized later.
            public void ComputePackedMipChainInfo(Vector2Int viewportSize)
            {
                textureSize = viewportSize;
                mipLevelSizes[0] = viewportSize;
                mipLevelOffsets[0] = Vector2Int.zero;

                int mipLevel = 0;
                Vector2Int mipSize = viewportSize;

                do
                {
                    mipLevel++;

                    // Round up.
                    mipSize.x = Math.Max(1, (mipSize.x + 1) >> 1);
                    mipSize.y = Math.Max(1, (mipSize.y + 1) >> 1);

                    mipLevelSizes[mipLevel] = mipSize;

                    Vector2Int prevMipBegin = mipLevelOffsets[mipLevel - 1];
                    Vector2Int prevMipEnd = prevMipBegin + mipLevelSizes[mipLevel - 1];

                    Vector2Int mipBegin = new Vector2Int();

                    if ((mipLevel & 1) != 0) // Odd
                    {
                        mipBegin.x = prevMipBegin.x;
                        mipBegin.y = prevMipEnd.y;
                    }
                    else // Even
                    {
                        mipBegin.x = prevMipEnd.x;
                        mipBegin.y = prevMipBegin.y;
                    }

                    mipLevelOffsets[mipLevel] = mipBegin;

                    textureSize.x = Math.Max(textureSize.x, mipBegin.x + mipSize.x);
                    textureSize.y = Math.Max(textureSize.y, mipBegin.y + mipSize.y);

                } while ((mipSize.x > 1) || (mipSize.y > 1));

                mipLevelCount = mipLevel + 1;
                m_OffsetBufferWillNeedUpdate = true;
            }

            public ComputeBuffer GetOffsetBufferData(ComputeBuffer mipLevelOffsetsBuffer)
            {

                if (m_OffsetBufferWillNeedUpdate)
                {
                    mipLevelOffsetsBuffer.SetData(mipLevelOffsets);
                    m_OffsetBufferWillNeedUpdate = false;
                }

                return mipLevelOffsetsBuffer;
            }
        }
        #endregion
        public MipGenerator()
        {
            m_TempColorTargets = new RTHandle[kernelCount];
            m_DepthPyramidCS = (ComputeShader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.lightweight/Editor/Light/ComputeGgxIblSampleData.compute", typeof(ComputeShader));
            m_ColorPyramidCS = (ComputeShader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.lightweight/Editor/Light/ComputeGgxIblSampleData.compute", typeof(ComputeShader));

            m_DepthDownsampleKernel = m_DepthPyramidCS.FindKernel("KDepthDownsample8DualUav");
            m_ColorDownsampleKernel = InitColorKernel("KColorDownsample");
            m_ColorDownsampleKernelCopyMip0 = InitColorKernel("KColorDownsampleCopyMip0");
            m_ColorGaussianKernel = InitColorKernel("KColorGaussian");

            m_SrcOffset = new int[4];
            m_DstOffset = new int[4];
           // m_ColorPyramidPS = asset.renderPipelineResources.shaders.colorPyramidPS;
            //m_ColorPyramidPSMat = CoreUtils.CreateEngineMaterial(m_ColorPyramidPS);
            //m_PropertyBlock = new MaterialPropertyBlock();
        }

        public void Release()
        {
            for (int i = 0; i < kernelCount; ++i)
            {
                RTHandles.Release(m_TempColorTargets[i]);
                m_TempColorTargets[i] = null;
            }
        }

        private int kernelCount
        {
            get
            {
                return 1;
            }
        }

        int[] InitColorKernel(string name)
        {
            int[] colorKernels = new int[kernelCount];
            colorKernels[kKernelTex2D] = m_ColorPyramidCS.FindKernel(name);
            // not handle XR for now
            //if (TextureXR.useTexArray)
            //    colorKernels[kKernelTex2DArray] = m_ColorPyramidCS.FindKernel(name + "_Tex2DArray");

            return colorKernels;
        }

        public static void CheckRTCreated(RenderTexture rt)
        {
            // In some cases when loading a project for the first time in the editor, the internal resource is destroyed.
            // When used as render target, the C++ code will re-create the resource automatically. Since here it's used directly as an UAV, we need to check manually
            if (!rt.IsCreated())
                rt.Create();
        }

        // Generates an in-place depth pyramid
        // TODO: Mip-mapping depth is problematic for precision at lower mips, generate a packed atlas instead
        public void RenderMinDepthPyramid(CommandBuffer cmd, RenderTexture texture, PackedMipChainInfo info)
        {
            CheckRTCreated(texture);

            var cs = m_DepthPyramidCS;
            int kernel = m_DepthDownsampleKernel;

            // TODO: Do it 1x MIP at a time for now. In the future, do 4x MIPs per pass, or even use a single pass.
            // Note: Gather() doesn't take a LOD parameter and we cannot bind an SRV of a MIP level,
            // and we don't support Min samplers either. So we are forced to perform 4x loads.
            for (int i = 1; i < info.mipLevelCount; i++)
            {
                Vector2Int dstSize = info.mipLevelSizes[i];
                Vector2Int dstOffset = info.mipLevelOffsets[i];
                Vector2Int srcSize = info.mipLevelSizes[i - 1];
                Vector2Int srcOffset = info.mipLevelOffsets[i - 1];
                Vector2Int srcLimit = srcOffset + srcSize - Vector2Int.one;

                m_SrcOffset[0] = srcOffset.x;
                m_SrcOffset[1] = srcOffset.y;
                m_SrcOffset[2] = srcLimit.x;
                m_SrcOffset[3] = srcLimit.y;

                m_DstOffset[0] = dstOffset.x;
                m_DstOffset[1] = dstOffset.y;
                m_DstOffset[2] = 0;
                m_DstOffset[3] = 0;

                cmd.SetComputeIntParams(cs, "_SrcOffsetAndLimit", m_SrcOffset);
                cmd.SetComputeIntParams(cs, "_DstOffset", m_DstOffset);
                cmd.SetComputeTextureParam(cs, kernel, "_DepthMipChain", texture);

                cmd.DispatchCompute(cs, kernel, DivRoundUp(dstSize.x, 8), DivRoundUp(dstSize.y, 8), texture.volumeDepth);
            }
        }

        // Generates the Gaussian pyramid of source into destination
        // We can't do it in place as the color pyramid has to be read while writing to the color
        // buffer in some cases (e.g. refraction, distortion)
        // Returns the number of mips
        public int RenderColorGaussianPyramid(CommandBuffer cmd, Vector2Int size, Texture source, RenderTexture destination)
        {
            // Select between Tex2D and Tex2DArray versions of the kernels
            int kernelIndex = (source.dimension == TextureDimension.Tex2DArray) ? kKernelTex2DArray : kKernelTex2D;

            // Sanity check
            if (kernelIndex == kKernelTex2DArray)
            {
                Debug.Assert(source.dimension == destination.dimension, "MipGenerator source texture does not match dimension of destination!");
                Debug.Assert(m_ColorGaussianKernel.Length == kernelCount);
            }

            // Only create the temporary target on-demand in case the game doesn't actually need it
            if (m_TempColorTargets[kernelIndex] == null)
            {
                m_TempColorTargets[kernelIndex] = RTHandles.Alloc(
                    Vector2.one * 0.5f,
                    filterMode: FilterMode.Bilinear,
                    colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite: true,
                    useMipMap: false,
                    enableMSAA: false,
                    xrInstancing: kernelIndex == kKernelTex2DArray,
                    useDynamicScale: true,
                    name: "Temp Gaussian Pyramid Target"
                );
            }

            int srcMipLevel = 0;
            int srcMipWidth = size.x;
            int srcMipHeight = size.y;
            int slices = destination.volumeDepth;

            
            
            var cs = m_ColorPyramidCS;
            int downsampleKernel = m_ColorDownsampleKernel[kernelIndex];
            int downsampleKernelMip0 = m_ColorDownsampleKernelCopyMip0[kernelIndex];
            int gaussianKernel = m_ColorGaussianKernel[kernelIndex];

            while (srcMipWidth >= 8 || srcMipHeight >= 8)
            {
                int dstMipWidth = Mathf.Max(1, srcMipWidth >> 1);
                int dstMipHeight = Mathf.Max(1, srcMipHeight >> 1);

                cmd.SetComputeVectorParam(cs, "_Size", new Vector4(srcMipWidth, srcMipHeight, 0f, 0f));

                // First dispatch also copies src to dst mip0
                if (srcMipLevel == 0)
                {
                    cmd.SetComputeTextureParam(cs, downsampleKernelMip0, "_Source", source, 0);
                    cmd.SetComputeTextureParam(cs, downsampleKernelMip0, "_Mip0, destination", 0);
                    cmd.SetComputeTextureParam(cs, downsampleKernelMip0, "_Destination", m_TempColorTargets[kernelIndex]);
                    cmd.DispatchCompute(cs, downsampleKernelMip0, (dstMipWidth + 7) / 8, (dstMipHeight + 7) / 8, slices);
                }
                else
                {
                    cmd.SetComputeTextureParam(cs, downsampleKernel, "_Source", destination, srcMipLevel);
                    cmd.SetComputeTextureParam(cs, downsampleKernel, "_Destination", m_TempColorTargets[kernelIndex]);
                    cmd.DispatchCompute(cs, downsampleKernel, (dstMipWidth + 7) / 8, (dstMipHeight + 7) / 8, slices);
                }

                cmd.SetComputeVectorParam(cs, "_Size", new Vector4(dstMipWidth, dstMipHeight, 0f, 0f));
                cmd.SetComputeTextureParam(cs, gaussianKernel, "_Source", m_TempColorTargets[kernelIndex]);
                cmd.SetComputeTextureParam(cs, gaussianKernel, "_Destination", destination, srcMipLevel + 1);
                cmd.DispatchCompute(cs, gaussianKernel, (dstMipWidth + 7) / 8, (dstMipHeight + 7) / 8, slices);

                srcMipLevel++;
                srcMipWidth = srcMipWidth >> 1;
                srcMipHeight = srcMipHeight >> 1;
            }
            

            return srcMipLevel + 1;
        }

        public static int DivRoundUp(int x, int y)
        {
            return (x + y - 1) / y;
        }
    }

    
}
