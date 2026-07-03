#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public class ShaderGraphPopupFixer : EditorWindow {
    [MenuItem("Window/Shader Graph Node Menu Fixer")]
    public static void ShowWindow() => GetWindow<ShaderGraphPopupFixer>("SG Popup Fixer");

    private int fontSize = 18;
    private bool isActive = false;

    void OnGUI() {
        fontSize = EditorGUILayout.IntSlider("Wielkość czcionki", fontSize, 12, 40);
        
        GUI.color = isActive ? Color.green : Color.white;
        if (GUILayout.Button(isActive ? "Monitoring Aktywny" : "Uruchom Monitoring Popupu", GUILayout.Height(40))) {
            isActive = true;
            // Rejestrujemy funkcję, która co 500ms sprawdza obecność okna
            EditorApplication.update -= SearchAndFix;
            EditorApplication.update += SearchAndFix;
        }
        GUI.color = Color.white;

        if (GUILayout.Button("Zatrzymaj")) {
            isActive = false;
            EditorApplication.update -= SearchAndFix;
        }
    }

    void SearchAndFix() {
        // Okno "Create Node" nazywa się wewnętrznuie "SearchWindow" lub "ObjectSelector"
        // Ale najłatwiej znaleźć je po prostu przeszukując wszystkie kontenery VisualElement w systemie
        var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        
        foreach (var win in allWindows) {
            // Szukamy okna, które ma w nazwie "Search" lub jest częścią ShaderGraph
            if (win.GetType().Name.Contains("Search") || win.GetType().Name.Contains("Graph")) {
                VisualElement root = win.rootVisualElement;
                
                // Szukamy Labeli wewnątrz okna wyszukiwania
                root.Query<Label>().ForEach(l => {
                    l.style.fontSize = fontSize;
                    l.style.height = fontSize + 10; // Zwiększamy wysokość wiersza, żeby tekst nie nachodził
                    l.style.unityTextAlign = TextAnchor.MiddleLeft;
                });

                // Powiększamy TextField (pole wyszukiwania na górze)
                root.Query<TextField>().ForEach(tf => {
                    tf.style.fontSize = fontSize + 2;
                    tf.style.height = fontSize + 15;
                });
            }
        }
    }
}
#endif