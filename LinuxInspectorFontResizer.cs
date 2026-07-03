#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class LinuxInspectorFontResizer : EditorWindow
{
    [MenuItem("Window/Linux Inspector Font Resizer")]
    public static void ShowWindow() => GetWindow<LinuxInspectorFontResizer>("Inspector Resizer");

    private int targetFontSize = 18;
    private bool autoHook = true;

    void OnGUI()
    {
        targetFontSize = EditorGUILayout.IntSlider("Czcionka w Inspektorze", targetFontSize, 12, 32);
        autoHook = EditorGUILayout.Toggle("Automatyczny monitoring", autoHook);

        if (GUILayout.Button("Zastosuj dla Inspektora", GUILayout.Height(40)))
        {
            ApplyFontScaleToInspector();
        }
    }

    private void OnEnable()
    {
        EditorApplication.update -= MonitorInspector;
        EditorApplication.update += MonitorInspector;
    }

    private void OnDestroy()
    {
        EditorApplication.update -= MonitorInspector;
    }

    private void MonitorInspector()
    {
        if (!autoHook) return;
        ApplyFontScaleToInspector();
    }

    private void ApplyFontScaleToInspector()
    {
        var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        
        foreach (var win in allWindows)
        {
            if (win == null) continue;
            
            // Celujemy TYLKO w główne systemowe okno Inspektora Unity
            if (win.GetType().Name.Contains("InspectorWindow"))
            {
                VisualElement root = win.rootVisualElement;
                if (root == null) continue;

                // 1. WYMUSZENIE CZCIONKI DLA KONTENERÓW IMGUI (Tam generują się właściwości nodów)
                root.Query<IMGUIContainer>().ForEach(container => {
                    container.style.fontSize = targetFontSize;
                });

                // 2. WYMUSZENIE DLA CAŁEJ RESZTY ELEMENTÓW TEKSTOWYCH W OKNIE
                root.Query<TextElement>().ForEach(te => {
                    te.style.fontSize = targetFontSize;
                });
                
                root.Query<Label>().ForEach(l => {
                    l.style.fontSize = targetFontSize;
                });

                // 3. RETUSZ STYLU EDYTORA (Zabezpieczenie dla surowych etykiet tekstowych)
                if (EditorStyles.label != null && EditorStyles.label.fontSize != targetFontSize)
                {
                    EditorStyles.label.fontSize = targetFontSize;
                    EditorStyles.textField.fontSize = targetFontSize;
                    EditorStyles.numberField.fontSize = targetFontSize;
                    EditorStyles.popup.fontSize = targetFontSize;
                }

                win.Repaint();
            }
        }
    }
}
#endif