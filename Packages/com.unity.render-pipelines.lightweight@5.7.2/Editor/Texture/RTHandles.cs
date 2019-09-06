using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering
{
    public static class RTHandles
    {
        static RTHandleSystem s_DefaultInstance = new RTHandleSystem();

        public static int maxWidth { get { return s_DefaultInstance.GetMaxWidth(); } }
        public static int maxHeight { get { return s_DefaultInstance.GetMaxHeight(); } }

        public static RTHandleSystem.RTHandle Alloc(
            int width,
            int height,
            int slices = 1,
            DepthBits depthBufferBits = DepthBits.None,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            TextureDimension dimension = TextureDimension.Tex2D,
            bool enableRandomWrite = false,
            bool useMipMap = false,
            bool autoGenerateMips = true,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            MSAASamples msaaSamples = MSAASamples.None,
            bool bindTextureMS = false,
            bool useDynamicScale = false,
            bool xrInstancing = false,
            RenderTextureMemoryless memoryless = RenderTextureMemoryless.None,
            string name = ""
            )
        {
            return s_DefaultInstance.Alloc(
                width,
                height,
                slices,
                depthBufferBits,
                colorFormat,
                filterMode,
                wrapMode,
                dimension,
                enableRandomWrite,
                useMipMap,
                autoGenerateMips,
                isShadowMap,
                anisoLevel,
                mipMapBias,
                msaaSamples,
                bindTextureMS,
                useDynamicScale,
                xrInstancing,
                memoryless,
                name
                );
        }

        public static RTHandleSystem.RTHandle Alloc(
            Vector2 scaleFactor,
            int slices = 1,
            DepthBits depthBufferBits = DepthBits.None,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            TextureDimension dimension = TextureDimension.Tex2D,
            bool enableRandomWrite = false,
            bool useMipMap = false,
            bool autoGenerateMips = true,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            bool enableMSAA = false,
            bool bindTextureMS = false,
            bool useDynamicScale = false,
            bool xrInstancing = false,
            RenderTextureMemoryless memoryless = RenderTextureMemoryless.None,
            string name = ""
            )
        {
            return s_DefaultInstance.Alloc(
                scaleFactor,
                slices,
                depthBufferBits,
                colorFormat,
                filterMode,
                wrapMode,
                dimension,
                enableRandomWrite,
                useMipMap,
                autoGenerateMips,
                isShadowMap,
                anisoLevel,
                mipMapBias,
                enableMSAA,
                bindTextureMS,
                useDynamicScale,
                xrInstancing,
                memoryless,
                name
                );
        }

        public static RTHandleSystem.RTHandle Alloc(
            ScaleFunc scaleFunc,
            int slices = 1,
            DepthBits depthBufferBits = DepthBits.None,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_SRGB,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            TextureDimension dimension = TextureDimension.Tex2D,
            bool enableRandomWrite = false,
            bool useMipMap = false,
            bool autoGenerateMips = true,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            bool enableMSAA = false,
            bool bindTextureMS = false,
            bool useDynamicScale = false,
            bool xrInstancing = false,
            RenderTextureMemoryless memoryless = RenderTextureMemoryless.None,
            string name = ""
            )
        {
            return s_DefaultInstance.Alloc(
                scaleFunc,
                slices,
                depthBufferBits,
                colorFormat,
                filterMode,
                wrapMode,
                dimension,
                enableRandomWrite,
                useMipMap,
                autoGenerateMips,
                isShadowMap,
                anisoLevel,
                mipMapBias,
                enableMSAA,
                bindTextureMS,
                useDynamicScale,
                xrInstancing,
                memoryless,
                name
                );
        }

        public static void Initialize(
            int width,
            int height,
            bool scaledRTsupportsMSAA,
            MSAASamples scaledRTMSAASamples
            )
        {
            s_DefaultInstance.Initialize(
                width,
                height,
                scaledRTsupportsMSAA,
                scaledRTMSAASamples
                );
        }

        public static void Release(RTHandleSystem.RTHandle rth)
        {
            s_DefaultInstance.Release(rth);
        }

        public static void ResetReferenceSize(
            int width,
            int height,
            MSAASamples msaaSamples
            )
        {
            s_DefaultInstance.ResetReferenceSize(
                width,
                height,
                msaaSamples
                );
        }

        public static void SetHardwareDynamicResolutionState(bool hwDynamicResRequested)
        {
            s_DefaultInstance.SetHardwareDynamicResolutionState(hwDynamicResRequested);
        }

        public static void SetReferenceSize(
            int width,
            int height,
            MSAASamples msaaSamples
            )
        {
            s_DefaultInstance.SetReferenceSize(
                width,
                height,
                msaaSamples
                );
        }
    }
}
