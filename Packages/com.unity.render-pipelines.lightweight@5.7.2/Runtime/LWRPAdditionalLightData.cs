namespace UnityEngine.Rendering.LWRP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class LWRPAdditionalLightData : MonoBehaviour
    {
        [Tooltip("Controls the usage of pipeline settings.")]
        [SerializeField] bool m_UsePipelineSettings = true;
        [SerializeField] float m_ShadowDistance = 10.0f;
        [SerializeField] bool m_ShadowLayerDistanceSurport = false;
        [SerializeField] float[] m_ShadowLayersDistance = new float[32];

        public bool usePipelineSettings
        {
            get { return m_UsePipelineSettings; }
            set { m_UsePipelineSettings = value; }
        }

        public float shadowDistance
        {
           get { return m_ShadowDistance; }
        }
        public bool shadowLayerDistanceSurport
        {
            get { return m_ShadowLayerDistanceSurport; }
        }
        public float[] shadowLayerDistance
        {
            get { return m_ShadowLayersDistance; }
        }
        public static void InitDefaultHDAdditionalLightData(LWRPAdditionalLightData lightData)
        {
            var light = lightData.gameObject.GetComponent<Light>();
            // modify light if needed
        }
    }
}
