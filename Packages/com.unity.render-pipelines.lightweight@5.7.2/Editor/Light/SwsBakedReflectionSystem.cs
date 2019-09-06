using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEditor.Experimental.Rendering;
using System.IO;
using System;
using System.Text.RegularExpressions;
using UnityEngine.Experimental.Rendering;

// 目前贴图卷积在reflectionProbePostProcessor里实现
// 有特殊拍cubemap的需求可以参考这个。
// Author: Jun Gao
// Email: gzgaojun@corp.netease.com
//// Description: Reflection Probe Baking Utility

//namespace UnityEngine.Rendering.LWRP
//{
//    public class SwsBakedReflectionSystem : ScriptableBakedReflectionSystem
//    {
//        const string k_ProbeAssetFormat = "ReflectionProbe-{0}.exr";
//        const string k_ProbeAssetPattern = "ReflectionProbe-*.exr";
//        static readonly Regex k_ProbeAssetRegex = new Regex(@"ReflectionProbe-(?<index>\d+)\.exr");

//        protected Material m_convolveMaterial;
//        protected Matrix4x4[] m_faceWorldToViewMatrixMatrices = new Matrix4x4[6];
//        protected MipGenerator m_MipGenerator;

//        RenderTexture m_GgxIblSampleData;
//        int m_GgxIblMaxSampleCount = 89;   // Width
//        const int k_GgxIblMipCountMinusOne = 6;    // Height (UNITY_SPECCUBE_LOD_STEPS)

//        ComputeShader m_ComputeGgxIblSampleDataCS;
//        int m_ComputeGgxIblSampleDataKernel = -1;

//        ComputeShader m_BuildProbabilityTablesCS;
//        int m_ConditionalDensitiesKernel = -1;
//        int m_MarginalRowDensitiesKernel = -1;


//        //[InitializeOnLoadMethod]
//        static void Initialize()
//        {
//            ScriptableBakedReflectionSystemSettings.system = new SwsBakedReflectionSystem();
//        }

//        SwsBakedReflectionSystem() : base(1)
//        {
//            m_ComputeGgxIblSampleDataCS = (ComputeShader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.lightweight/Editor/Light/ComputeGgxIblSampleData.compute", typeof(ComputeShader));
//            m_ComputeGgxIblSampleDataKernel = m_ComputeGgxIblSampleDataCS.FindKernel("ComputeGgxIblSampleData");
//            m_BuildProbabilityTablesCS = (ComputeShader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.lightweight/Editor/Light/BuildProbabilityTables.compute", typeof(ComputeShader));
//            m_ConditionalDensitiesKernel = m_BuildProbabilityTablesCS.FindKernel("ComputeConditionalDensities");
//            m_MarginalRowDensitiesKernel = m_BuildProbabilityTablesCS.FindKernel("ComputeMarginalRowDensities");

//        }
//        #region SystemCallback
//        // Called by BakeAllReflectionProbes Button
//        public override bool BakeAllReflectionProbes()
//        {
//            Debug.Log("Bake all reflection probes");
//            //return true;
//            //if (!AreAllOpenedSceneSaved())
//            //    return false;

//            DeleteCubemapAssets(true);
//            //var bakedProbes = HDProbeSystem.bakedProbes;

//            return BakeProbes();
//        }
//        public override void Clear()
//        {
//            if (!AreAllOpenedSceneSaved())
//            {
//                return;
//            }
//            DeleteCubemapAssets(false);
//        }
//        #endregion
//        #region AssetAndIO
//        // sort by instance id, following the unity way 
//        public static List<ReflectionProbe> GetReflectionProbesNeedBake()
//        {
//            var probes =  GameObject.FindObjectsOfType<ReflectionProbe>();
//            var bakingProbes = new List<ReflectionProbe>();
//            foreach (ReflectionProbe p in probes)
//            {
//                if (p.mode == ReflectionProbeMode.Baked)
//                {
//                    bakingProbes.Add(p);
//                }
//            }
//            bakingProbes.Sort((p1, p2) => p1.GetInstanceID().CompareTo(p2.GetInstanceID()));
//            return bakingProbes;
//        }

//        static bool AreAllOpenedSceneSaved()
//        {
//            for (int i = 0, c = SceneManager.sceneCount; i < c; ++i)
//            {
//                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
//                    return false;
//            }
//            return true;
//        }

        
//        void DeleteCubemapAssets(bool deleteUnusedOnly)
//        {
//            var probes = new List<ReflectionProbe>();
//            var indices = new List<int>();
//            var scenes = new List<Scene>();
//            GetProbeIDsForAllScenes(probes, indices, scenes);
//            //for (int i = 0; i< probes.Count;i++)
//            //{
//            //    Debug.Log($"Probe {probes[i]} -- ID {indices[i]} -- scene {scenes[i].name}");
//            //}

//            var indicesSet = new HashSet<int>(indices);
//            List<string> toDeleteAsset = new List<string>();

//            // Look for baked assets in scene folders
//            for (int sceneIndex = 0, sceneCount = SceneManager.sceneCount; sceneIndex < sceneCount; ++sceneIndex)
//            {
//                var scene = SceneManager.GetSceneAt(sceneIndex);
//                var sceneFolder = GetBakedTextureDirectory(scene);
//                if (!Directory.Exists(sceneFolder))
//                {
//                    continue;
//                }

//                var files = Directory.GetFiles(
//                    sceneFolder,
//                    ProbeAssetPattern()
//                );
//                for (int fileI = 0; fileI < files.Length; ++fileI)
//                {
//                    if (!TryParseBakedProbeAssetFileName(files[fileI], out int fileIndex))
//                    {
//                        continue;
//                    }

//                    // This file is a baked asset for a destroyed game object
//                    // We can destroy it
//                    if (!indicesSet.Contains(fileIndex) && deleteUnusedOnly
//                        // Or we delete all assets
//                        || !deleteUnusedOnly)
//                    {
//                        toDeleteAsset.Add(files[fileI]);
//                    }
//                }
//            }
//            DeleteAllAssetsIn(toDeleteAsset);
//        }

//        public static string GetBakedTextureDirectory(SceneManagement.Scene scene)
//        {
//            var scenePath = scene.path;
//            if (string.IsNullOrEmpty(scenePath))
//                return string.Empty;

//            var cacheDirectoryName = Path.GetFileNameWithoutExtension(scenePath);
//            var cacheDirectory = Path.Combine(Path.GetDirectoryName(scenePath), cacheDirectoryName);
//            return cacheDirectory;
//        }

//        public static string GetBakedTextureFilePath(
//            int index,
//            SceneManagement.Scene scene
//        )
//        {
//            var cacheDirectory = GetBakedTextureDirectory(scene);
//            var targetFile = Path.Combine(
//                cacheDirectory,
//                string.Format(k_ProbeAssetFormat, index)
//            );
//            return targetFile;
//        }

//        static void GetProbeIDsForAllScenes(List<ReflectionProbe> outProbes, List<int> outIndices, List<Scene> outScenes)
//        {
//            if (outProbes == null)
//                throw new ArgumentNullException("outProbes");
//            if (outIndices == null)
//                throw new ArgumentNullException("outIndices");
//            if (outScenes == null)
//                throw new ArgumentNullException("outScenes");
//            var lastCount = outProbes.Count;
//            var probes = GetReflectionProbesNeedBake();
//            for (int i = 0; i < SceneManager.sceneCount; ++i)
//            {
//                var scene = SceneManager.GetSceneAt(i);
//                foreach (ReflectionProbe p in probes)
//                {
//                    if (p.gameObject.scene == scene)
//                    {
//                        outProbes.Add(p);
//                        outIndices.Add(p.GetInstanceID());
//                    }
//                }
//                for (int j = 0, c = outProbes.Count - lastCount; j < c; ++j)
//                {
//                    outScenes.Add(scene);
//                }            
//            }
//        }

//        string ProbeAssetPattern() // remove this later
//        {
//            return string.Format("ReflectionProbe-*.exr");
//        }

//        public static void CreateParentDirectoryIfMissing(string path)
//        {
//            var fileInfo = new FileInfo(path);
//            if (!fileInfo.Directory.Exists)
//                fileInfo.Directory.Create();
//        }

//        bool TryParseBakedProbeAssetFileName(string filename, out int index)
//        {
//            var match = k_ProbeAssetRegex.Match(filename);
//            if (!match.Success)
//            {
//                index = 0;
//                return false;
//            }

//            index = int.Parse(match.Groups["index"].Value);
//            return true;
//        }

//        static void DeleteAllAssetsIn(List<string> toDeleteAsset)
//        {
//            if (toDeleteAsset.Count == 0)
//                return;

//            AssetDatabase.StartAssetEditing();
//            foreach (string path in toDeleteAsset)
//            {
//                AssetDatabase.DeleteAsset(path);
//            }  
//            AssetDatabase.StopAssetEditing();
//        }

//        internal static void ImportAssetAt(ReflectionProbe probe, string file)
//        {

//            var importer = AssetImporter.GetAtPath(file) as TextureImporter;
//            if (importer == null)
//                return;
//            importer.sRGBTexture = false;
//            importer.filterMode = FilterMode.Bilinear;
//            importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            
//            importer.mipmapEnabled = false;
//            importer.textureCompression = TextureImporterCompression.Uncompressed; // ? TextureImporterCompression.Compressed
//            importer.textureShape = TextureImporterShape.TextureCube;
            
//            importer.SaveAndReimport();
//        }
//        #endregion
//        #region TextureUtls
//        public static RenderTexture CreateReflectionProbeRenderTarget(int cubemapSize)
//        {
//            return new RenderTexture(cubemapSize, cubemapSize, 1, GraphicsFormat.R16G16B16A16_SFloat)
//            {
//                dimension = TextureDimension.Cube,
//                enableRandomWrite = true,
//                useMipMap = true,
//                autoGenerateMips = false
//            };
//        }

//        static Texture2D FlipTexture(Texture2D original)
//        {
//            TextureFormat format = original.format;
//            Texture2D flipped = new Texture2D(original.width, original.height, format, false);

//            int xN = original.width;
//            int yN = original.height;


//            for (int i = 0; i < xN; i++)
//            {
//                for (int j = 0; j < yN; j++)
//                {
//                    flipped.SetPixel(i, yN - j - 1, original.GetPixel(i, j));
//                }
//            }
//            flipped.Apply();

//            return flipped;
//        }

//        public static Texture2D CopyRenderTextureToTexture2D(RenderTexture source)
//        {
//            TextureFormat format = TextureFormat.RGBAFloat;
//            switch (source.format)
//            {
//                case RenderTextureFormat.ARGBFloat: format = TextureFormat.RGBAFloat; break;
//                case RenderTextureFormat.ARGBHalf: format = TextureFormat.RGBAHalf; break;
//                default:
//                    Assert.IsFalse(true, "Unmanaged format");
//                    break;
//            }

//            switch (source.dimension)
//            {
//                case TextureDimension.Cube:
//                    {
//                        var resolution = source.width;
//                        var result = new Texture2D(resolution * 6, resolution, format, false);

//                        var offset = 0;
//                        for (var i = 0; i < 6; ++i)
//                        {
//                            Graphics.SetRenderTarget(source, 0, (CubemapFace)i);
//                            result.ReadPixels(new Rect(0, 0, resolution, resolution), offset, 0);
//                            offset += resolution;
//                            Graphics.SetRenderTarget(null);
//                        }
//                        result.Apply();

//                        return result;
//                    }
//                case TextureDimension.Tex2D:
//                    {
//                        var resolution = source.width;
//                        var result = new Texture2D(resolution, resolution, format, false);

//                        Graphics.SetRenderTarget(source, 0);
//                        result.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
//                        result.Apply();
//                        Graphics.SetRenderTarget(null);

//                        return result;
//                    }
//                default:
//                    throw new ArgumentException();
//            }
//        }

//        public static void WriteTextureFileToDisk(Texture target, string filePath)
//        {
//            var rt = target as RenderTexture;
//            var cube = target as Cubemap;
//            if (rt != null)
//            {
//                var t2D = CopyRenderTextureToTexture2D(rt);
//                var ft2d = FlipTexture(t2D);
//                var bytes = ft2d.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
//                CreateParentDirectoryIfMissing(filePath);
//                File.WriteAllBytes(filePath, bytes);
//                return;
//            }
//            else if (cube != null)
//            {
//                var t2D = new Texture2D(cube.width * 6, cube.height, GraphicsFormat.R16G16B16A16_SFloat, TextureCreationFlags.None);
//                var cmd = new CommandBuffer { name = "CopyCubemapToTexture2D" };
//                for (int i = 0; i < 6; ++i)
//                {
//                    cmd.CopyTexture(
//                        cube, i, 0, 0, 0, cube.width, cube.height,
//                        t2D, 0, 0, cube.width * i, 0
//                    );
//                }
//                Graphics.ExecuteCommandBuffer(cmd);
//                var bytes = t2D.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
//                CreateParentDirectoryIfMissing(filePath);
//                File.WriteAllBytes(filePath, bytes);
//                return;
//            }
//            throw new ArgumentException();
//        }
//        #endregion
//        #region GameObjectUtls 
//        static Camera NewRenderingCamera()
//        {
//            var go = new GameObject("__Render Camera");
//            var camera = go.AddComponent<Camera>();
//            camera.cameraType = CameraType.Reflection;
//            //go.AddComponent<HDAdditionalCameraData>();

//            return camera;
//        }

//        static Light NewEclipseLight()
//        {
//            var go = new GameObject("__Eclipse Light");
//            go.transform.rotation = Quaternion.Euler(50, -30, 0);
//            var dirLight = go.AddComponent<Light>();
//            dirLight.intensity = 0;
//            dirLight.type = LightType.Directional;
//            return dirLight;
//        }
//        #endregion
//        #region CoreRendering
//        internal static void RenderAndWriteToFile(
//           ReflectionProbe probe, string targetFile,
//           RenderTexture cubeRT
//       )
//        {
//            RenderProbe(probe, cubeRT, (uint)StaticEditorFlags.ReflectionProbeStatic);
//            CreateParentDirectoryIfMissing(targetFile);
//            WriteTextureFileToDisk(cubeRT, targetFile);

//        }

//        public static void RenderProbe(
//            ReflectionProbe probe,
//            Texture target,
//            uint staticFlags = 0
//        )
//        {
//            // Argument checking
//            if (target == null)
//                throw new ArgumentNullException(nameof(target));

//            var rtTarget = target as RenderTexture;
//            var cubeTarget = target as Cubemap;
//            switch (target.dimension)
//            {
//                case TextureDimension.Tex2D:
//                    if (rtTarget == null)
//                        throw new ArgumentException("'target' must be a RenderTexture when rendering into a 2D texture");
//                    break;
//                case TextureDimension.Cube:
//                    break;
//                default:
//                    throw new ArgumentException("Rendering into a target of dimension "
//                        + $"{target.dimension} is not supported");
//            }

//            var camera = NewRenderingCamera();
//            try
//            {
                
//                camera.nearClipPlane = probe.nearClipPlane;
//                camera.farClipPlane = probe.farClipPlane;
//                camera.cullingMask = probe.cullingMask;
//                camera.allowHDR = probe.hdr;
//                camera.fieldOfView = 90;
//                camera.aspect = 1;
//                camera.transform.position = probe.transform.position;
//                camera.transform.rotation = Quaternion.identity;
//                //camera.projectionMatrix = Matrix4x4.Perspective(90, 1, camera.nearClipPlane, camera.farClipPlane) * Matrix4x4.Scale(new Vector3(1, -1, 1));
//                //camera.ResetCullingMatrix();
//                camera.clearFlags = (CameraClearFlags)probe.clearFlags; // 1 skybox 2 color, 3-5 will be error
//                camera.backgroundColor = probe.backgroundColor;
//                camera.cameraType = CameraType.Reflection;
                
             
//                switch (target.dimension)
//                {
//                    case TextureDimension.Tex2D:
//                        {
//#if DEBUG
//                            Debug.LogWarning(
//                                "A static flags bitmask was provided but this is ignored when rendering into a Tex2D"
//                            );
//#endif
//                            Assert.IsNotNull(rtTarget);
//                            camera.targetTexture = rtTarget;
//                            camera.Render();
//                            camera.targetTexture = null;
                            
//                            target.IncrementUpdateCount();
//                            break;
//                        }
//                    case TextureDimension.Cube: // reflection probe bake system use this path
//                        {
//                            Assert.IsTrue(rtTarget != null || cubeTarget != null);

//                            var canHandleStaticFlags = false;
//#if UNITY_EDITOR
//                            canHandleStaticFlags = true;
//#endif
//                            // ReSharper disable ConditionIsAlwaysTrueOrFalse
//                            if (canHandleStaticFlags && staticFlags != 0)
//                            // ReSharper restore ConditionIsAlwaysTrueOrFalse
//                            {
//#if UNITY_EDITOR
//                                UnityEditor.Rendering.EditorCameraUtils.RenderToCubemap(
//                                    camera,
//                                    rtTarget,
//                                    -1,
//                                    UnityEditor.StaticEditorFlags.ReflectionProbeStatic
//                                );
//                                //camera.RenderToCubemap(rtTarget);
//#endif
//                            }
//                            //else
//                            //{
//                            //    // ReSharper disable ConditionIsAlwaysTrueOrFalse
//                            //    if (!canHandleStaticFlags && staticFlags != 0)
//                            //    // ReSharper restore ConditionIsAlwaysTrueOrFalse
//                            //    {
//                            //        Debug.LogWarning(
//                            //            "A static flags bitmask was provided but this is ignored in player builds"
//                            //        );
//                            //    }

//                            //    if (rtTarget != null)
//                            //        camera.RenderToCubemap(rtTarget);
//                            //    if (cubeTarget != null)
//                            //        camera.RenderToCubemap(cubeTarget);
//                            //}

//                            target.IncrementUpdateCount();
//                            break;
//                        }
//                }
//            }
//            finally
//            {
//                CoreUtils.Destroy(camera.gameObject);
//            }
//        }
        

//        public static bool BakeProbes()//IList<ReflectionProbe> bakedProbes)
//        {
//            if (!(RenderPipelineManager.currentPipeline is LightweightRenderPipeline lwrp))
//            {
//                Debug.LogError("事务所环境图生成系统只与专用LWRP兼容, " +
//                    "请切换渲染管线");
//                return false;
//            }
//            Debug.Log("事务所环境图生成系统开始烘焙");
//            var probes = GetReflectionProbesNeedBake();
//            var cubemapSize = 512; // hardcode for now
//            var eclipseLight = NewEclipseLight(); // light with intensity of zero
//            RenderSettings.sun = eclipseLight;
//            var cubeRT = CreateReflectionProbeRenderTarget(cubemapSize);

//            // Render and write the result to disk
//            for (int i = 0; i < probes.Count; ++i)
//            {
//                var probe = probes[i];
//                var bakedTexturePath = GetBakedTextureFilePath(i, probe.gameObject.scene);
//                RenderAndWriteToFile(probe, bakedTexturePath, cubeRT);
//            }

//            // AssetPipeline bug
//            // Sometimes, the baked texture reference is destroyed during 'AssetDatabase.StopAssetEditing()'
//            //   thus, the reference to the baked texture in the probe is lost
//            // Although, importing twice the texture seems to workaround the issue
//            for (int j = 0; j < 2; ++j)
//            {
//                AssetDatabase.StartAssetEditing();
//                for (int i = 0; i < probes.Count; ++i)
//                {
//                    var probe = probes[i];
//                    var bakedTexturePath = GetBakedTextureFilePath(i, probe.gameObject.scene);
//                    AssetDatabase.ImportAsset(bakedTexturePath);
//                    ImportAssetAt(probe, bakedTexturePath);
//                }
//                AssetDatabase.StopAssetEditing();
//            }

//            AssetDatabase.StartAssetEditing();
//            for (int i = 0; i < probes.Count; ++i)
//            {
//                var probe = probes[i];
//                var bakedTexturePath = GetBakedTextureFilePath(i, probe.gameObject.scene);

//                // Get or create the baked texture asset for the probe
//                var bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(bakedTexturePath);
//                Assert.IsNotNull(bakedTexture, "The baked texture was imported before, " +
//                    "so it must exists in AssetDatabase");
                  
//                // Update import settings
//                ImportAssetAt(probe, bakedTexturePath);
//                probe.bakedTexture = bakedTexture;
//                EditorUtility.SetDirty(probe);
//            }
//            AssetDatabase.StopAssetEditing();
//            cubeRT.Release();
//            RenderSettings.sun = null;
//            CoreUtils.Destroy(eclipseLight.gameObject);
//            return true;
//        }


//        #endregion
//    }
//}

//static void FlipRT(RenderTexture cubeRT, RenderTexture flipCubeRT)
//{
//    CommandBuffer cmd = CommandBufferPool.Get("FlipCommand");

//    ComputeShader flipCompute = (ComputeShader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.lightweight/Editor/Light/FlipRenderTexture.compute", typeof(ComputeShader));
//    int kernalID = flipCompute.FindKernel("CSMain");
//    cmd.SetComputeTextureParam(flipCompute, kernalID, "Src", cubeRT);
//    cmd.SetComputeTextureParam(flipCompute, kernalID, "Dst", flipCubeRT);
//    cmd.DispatchCompute(flipCompute, kernalID, 16,16,1);
//    Graphics.ExecuteCommandBuffer(cmd);
//    cmd.Release();

//}

