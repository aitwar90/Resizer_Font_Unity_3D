#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class LinuxGraphFontResizer : EditorWindow
{
    [MenuItem("Window/Linux VFX-SG Font Resizer")]
    public static void ShowWindow() => GetWindow<LinuxGraphFontResizer>("Graph Font Resizer");

    private int targetFontSize = 18;
    private bool autoHookSearch = true;

    void OnGUI()
    {
        targetFontSize = EditorGUILayout.IntSlider("Wielkość czcionki (Graph)", targetFontSize, 12, 32);
        autoHookSearch = EditorGUILayout.Toggle("Automatyczny monitoring popupów", autoHookSearch);

        if (GUILayout.Button("Zastosuj dla otwartych okien", GUILayout.Height(40)))
        {
            ApplyFontScaleToGraphs(false);
        }
    }

    private void OnEnable()
    {
        EditorApplication.update -= MonitorPopups;
        EditorApplication.update += MonitorPopups;
    }

    private void OnDestroy()
    {
        EditorApplication.update -= MonitorPopups;
    }

    private void MonitorPopups()
    {
        if (!autoHookSearch) return;
        ApplyFontScaleToGraphs(true);
    }

    private void ApplyFontScaleToGraphs(bool onlyPopups)
    {
        var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        
        foreach (var win in allWindows)
        {
            if (win == null) continue;
            string winName = win.GetType().Name;
            
            bool isPopup = winName.Contains("Search") || winName.Contains("Selector") || 
                           winName.Contains("Popup") || winName.Contains("Filter") || 
                           winName.Contains("Menu");
            
            bool isGraph = winName.Contains("VFX") || winName.Contains("ShaderGraph") || winName.Contains("Graph");
            
            // Łapiemy główne, systemowe okno Inspektora Unity
            bool isInspector = winName.Contains("InspectorWindow");

            if (onlyPopups && !isPopup) continue;
            if (!isGraph && !isPopup && !isInspector) continue;

            VisualElement root = win.rootVisualElement;
            if (root == null) continue;

            // --- 1. GLOBALNY ROZMIAR BAZOWY DLA KORZENIA OKNA ---
            root.style.fontSize = targetFontSize;

            // --- 2. GŁĘBOKIE I AGRESYWNE SKALOWANIE TEKSTU ---
            // Łapie zwykłe labele, pola tekstowe, opisy oraz dynamiczne kontenery w Inspektorze i Grafie
            root.Query<TextElement>().ForEach(te => {
                te.style.fontSize = targetFontSize;
            });
            
            root.Query<Label>().ForEach(l => {
                l.style.fontSize = targetFontSize;
            });

            // --- 3. OBSŁUGA POPUPOW (To, co działa idealnie) ---
            if (isPopup || isGraph)
            {
                root.Query<VisualElement>(className: "unity-search-window-entry").ForEach(entry => {
                    entry.style.fontSize = targetFontSize;
                    entry.style.height = targetFontSize + 12;
                });
            }

            // --- 4. WYMUSZENIE REGUŁ USS DLA INSPEKTORA NOWĄ METODĄ KASKADOWĄ ---
            if (isInspector)
            {
                // Bezpośrednia kwerenda do wszystkich możliwych kontenerów pól danych w Inspektorze Unity
                root.Query<VisualElement>().ForEach(el => {
                    // Jeśli element ma w klasie USS powiązanie z tekstem, labelem lub inputem pola - wymuszamy rozmiar
                    if (el.ClassListContains("unity-label") || 
                        el.ClassListContains("unity-base-field__label") || 
                        el.ClassListContains("unity-base-field__input") ||
                        el.ClassListContains("unity-text-element"))
                    {
                        el.style.fontSize = targetFontSize;
                    }
                });
            }

            win.Repaint();
        }
    }
}
#endif