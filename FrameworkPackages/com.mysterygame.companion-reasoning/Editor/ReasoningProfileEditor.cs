using UnityEditor;
namespace CompanionFramework.Editor
{
    [CustomEditor(typeof(ReasoningProfile))]
    public sealed class ReasoningProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var errors = ((ReasoningProfile)target).ValidateConfiguration();
            foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (errors.Count == 0) EditorGUILayout.HelpBox(
                "Profile is structurally valid. The host must still validate evidence IDs and disclosure content.", MessageType.Info);
        }
    }
}
