using UnityEngine;
using UnityEngine.Rendering.LWRP;

namespace UnityEditor.Rendering.LWRP
{
    [CustomEditor(typeof(ForwardRendererData), true)]
    public class ForwardRendererDataEditor : ScriptableRendererDataEditor
    {
        private class Styles
        {
            public static readonly GUIContent RendererTitle = new GUIContent("Forward Renderer", "Custom Forward Renderer for LWRP.");
            public static readonly GUIContent OpaqueMask = new GUIContent("Default Layer Mask", "Controls which layers to globally include in the Custom Forward Renderer.");
            public static readonly GUIContent UIAfterLayerMask = new GUIContent("After PP UI Mask", "The ui with this mask will be rendered after Post process");
            public static readonly GUIContent OpaqueAfterLayerMask = new GUIContent("After PP Opaque Mask", "The opaque mesh with this mask will be rendered after Post process");
        }

        SerializedProperty m_OpaqueLayerMask;
        SerializedProperty m_TransparentLayerMask;
        SerializedProperty m_UIAfterPPLayerMask;
        SerializedProperty m_OpaqueAfterPPLayerMask;

        private void OnEnable()
        {
            m_OpaqueLayerMask = serializedObject.FindProperty("m_OpaqueLayerMask");
            m_TransparentLayerMask = serializedObject.FindProperty("m_TransparentLayerMask");
            m_UIAfterPPLayerMask = serializedObject.FindProperty("m_UIAfterPPLayerMask");
            m_OpaqueAfterPPLayerMask = serializedObject.FindProperty("m_OpaqueAfterPPLayerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Styles.RendererTitle, EditorStyles.boldLabel); // Title
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_OpaqueLayerMask, Styles.OpaqueMask);
            EditorGUILayout.PropertyField(m_UIAfterPPLayerMask, Styles.UIAfterLayerMask);
            EditorGUILayout.PropertyField(m_OpaqueAfterPPLayerMask, Styles.OpaqueAfterLayerMask);
            if (EditorGUI.EndChangeCheck())  // We copy the opaque mask to the transparent mask, later we might expose both
            {
                m_OpaqueLayerMask.intValue = m_OpaqueLayerMask.intValue & (~m_OpaqueAfterPPLayerMask.intValue);
                m_TransparentLayerMask.intValue = m_OpaqueLayerMask.intValue & (~m_UIAfterPPLayerMask.intValue);
            }
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI(); // Draw the base UI, contains ScriptableRenderFeatures list
        }
    }
}
