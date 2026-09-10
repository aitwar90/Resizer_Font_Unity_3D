#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class EditorFontResizer : EditorWindow
{
    [MenuItem("Window/Editor Font Resizer")]
    public static void Open()
    {
        var win = GetWindow<EditorFontResizer>("Editor Font Resizer");
        win.minSize = new Vector2(250f, 150f);
    }

    private const string ConfigPath = "EditorFontResizer.cfg";
    private static Dictionary<string, int> _config = new Dictionary<string, int>();

    private class StyleInfo
    {
        public readonly string name;
        private readonly GUIStyle _style;

        public int FontSize
        {
            get => _config.TryGetValue(name, out int val) ? val : (_style != null ? _style.fontSize : 12);
            set
            {
                if (value > 0)
                {
                    _config[name] = value;
                    if (_style != null)
                    {
                        _style.fontSize = value;
                    }
                }
            }
        }

        public StyleInfo(string name, GUIStyle style)
        {
            this.name = name;
            _style = style;
            InitSize();
        }

        private void InitSize()
        {
            int defaultSize = 12;
            if (_style != null)
            {
                if (_style.fontSize > 0) defaultSize = _style.fontSize;
                else if (GUI.skin != null && GUI.skin.font != null) defaultSize = GUI.skin.font.fontSize;
            }

            if (!_config.ContainsKey(name))
            {
                _config.Add(name, defaultSize);
            }
            
            if (_style != null)
            {
                _style.fontSize = _config[name];
            }
        }
    }

    private List<StyleInfo> _editorStyles;
    private List<StyleInfo> _guiStyles;
    private List<StyleInfo> _customStyles;

    private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
    private GUIStyle _evenBG;
    private GUIStyle _oddBG;
    private Vector2 _scroll;

    private int _uitoolkitFontSize = 13;
    private bool _initialized;

    // OPTYMALIZACJA: Cache czasu dla odpychania UI Toolkit
    private double _lastScaleTime;
    private const double ScaleInterval = 0.5; // Odświeżaj UI Toolkit max 2 razy na sekundę zamiast 60-144 razy!

    private void OnEnable()
    {
        _config = ReadDictionary(ConfigPath);
        if (_config.TryGetValue("uitoolkit.globalFontSize", out int savedUITSize))
        {
            _uitoolkitFontSize = savedUITSize;
        }

        _initialized = false;
        EditorApplication.update += ThrottledUIToolkitScale;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ThrottledUIToolkitScale;
        SaveConfig();
    }

    private void InitStyles()
    {
        if (_evenBG != null) return;

        _evenBG = new GUIStyle("CN EntryBackEven")
        {
            contentOffset = Vector2.zero,
            clipping = TextClipping.Clip,
            margin = new RectOffset(),
            padding = new RectOffset()
        };

        _oddBG = new GUIStyle("CN EntryBackOdd")
        {
            contentOffset = Vector2.zero,
            clipping = TextClipping.Clip,
            margin = new RectOffset(),
            padding = new RectOffset()
        };
    }

    private void InitProperties()
    {
        if (_initialized) return;

        var trackedStyles = new HashSet<GUIStyle>();

        _editorStyles = new List<StyleInfo>();
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty;
        
        PropertyInfo[] editorProperties = typeof(EditorStyles).GetProperties(flags);
        for (int i = 0; i < editorProperties.Length; i++)
        {
            var s = TryGetGUIStyle(editorProperties[i], null);
            if (s == null || trackedStyles.Contains(s)) continue;

            trackedStyles.Add(s);
            _editorStyles.Add(new StyleInfo("editor." + editorProperties[i].Name, s));
        }

        if (GUI.skin != null)
        {
            _guiStyles = new List<StyleInfo>();
            PropertyInfo[] guiProperties = GUI.skin.GetType().GetProperties();
            for (int i = 0; i < guiProperties.Length; i++)
            {
                var s = TryGetGUIStyle(guiProperties[i], GUI.skin);
                if (s == null || trackedStyles.Contains(s)) continue;

                trackedStyles.Add(s);
                _guiStyles.Add(new StyleInfo("gui." + guiProperties[i].Name, s));
            }

            _customStyles = new List<StyleInfo>();
            GUIStyle[] custom = GUI.skin.customStyles;
            for (int i = 0; i < custom.Length; i++)
            {
                var s = custom[i];
                if (s == null || string.IsNullOrEmpty(s.name) || trackedStyles.Contains(s)) continue;

                trackedStyles.Add(s);
                _customStyles.Add(new StyleInfo("custom." + s.name, s));
            }

            string[] hiddenProjectStyles = {
                "ProjectBrowserGridLabel", "PR Label", "TV Line", "ControlLabel",
                "ObjectField", "ObjectFieldThumb", "TextField", "NumberField",
                "LayerMaskField", "IN TitleText", "VariableField", "miniLabel",
                "ExposedParameterText", "HeaderLabel", "Foldout", "WordWrappedLabel",
                "MiniLabel", "CN Message", "SearchTextField", "TV Selection",
                "TV LineBold", "ProjectBrowserHeaderBgComment"
            };

            for (int i = 0; i < hiddenProjectStyles.Length; i++)
            {
                GUIStyle s = GUI.skin.FindStyle(hiddenProjectStyles[i]);
                if (s != null && !trackedStyles.Contains(s))
                {
                    trackedStyles.Add(s);
                    _customStyles.Add(new StyleInfo("custom." + hiddenProjectStyles[i], s));
                }
            }
        }

        _initialized = true;
    }

    private GUIStyle TryGetGUIStyle(PropertyInfo x, object item)
    {
        if (x.PropertyType != typeof(GUIStyle)) return null;

        try
        {
            return (GUIStyle)x.GetValue(item, null);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, int> ReadDictionary(string path)
    {
        var dict = new Dictionary<string, int>();
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists) return dict;

        using (var fileStream = fileInfo.OpenRead())
        using (var reader = new StreamReader(fileStream, Encoding.UTF8))
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;
                string[] pair = line.Split(':');
                if (pair.Length == 2 && int.TryParse(pair[1], out int val))
                {
                    dict[pair[0]] = val;
                }
            }
        }
        return dict;
    }

    private static void WriteDictionary(Dictionary<string, int> dict, string path)
    {
        using (var file = new StreamWriter(path, false, Encoding.UTF8))
        {
            foreach (var pair in dict)
            {
                file.WriteLine("{0}:{1}", pair.Key, pair.Value);
            }
        }
    }

    private void SaveConfig()
    {
        _config["uitoolkit.globalFontSize"] = _uitoolkitFontSize;
        WriteDictionary(_config, ConfigPath);
    }

    private void ApplyChanges()
    {
        SaveConfig();
        ApplyUIToolkitFontScalingDirect();
        RepaintAllWindows();
    }

    /// <summary>
    /// OPTYMALIZACJA: Ograniczenie częstotliwości skanowania UI z 60-144 FPS do 2 razy na sekundę (Throttling).
    /// Eliminujemy ścinanie edytora i GC Spike na słabszych laptopach.
    /// </summary>
    private void ThrottledUIToolkitScale()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - _lastScaleTime >= ScaleInterval)
        {
            _lastScaleTime = currentTime;
            ApplyUIToolkitFontScalingDirect();
        }
    }

    private void ApplyUIToolkitFontScalingDirect()
    {
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            EditorWindow window = windows[i];
            if (window == null || window.rootVisualElement == null) continue;

            VisualElement root = window.rootVisualElement;

            // Zamiast tworzyć listy przez .Build(), bezpośrednio po obiekcie
            root.Query<Label>().ForEach(l => l.style.fontSize = _uitoolkitFontSize);
            root.Query<Button>().ForEach(b => b.style.fontSize = _uitoolkitFontSize);
            root.Query(className: "unity-text-element").ForEach(t => t.style.fontSize = _uitoolkitFontSize);
        }
    }

    private static void RepaintAllWindows()
    {
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            windows[i].Repaint();
        }
    }

    private bool Header(string name)
    {
        if (!_foldouts.TryGetValue(name, out bool val))
        {
            val = true;
            _foldouts.Add(name, true);
        }
        GUILayout.Space(5);
        bool foldout = EditorGUILayout.Foldout(!val, name, true);
        _foldouts[name] = !foldout;
        return foldout;
    }

    private void FontSizeRow(StyleInfo styleInfo, bool even)
    {
        int delta = DrawRow(styleInfo.name, styleInfo.FontSize.ToString(), even ? _evenBG : _oddBG);
        if (delta != 0)
        {
            styleInfo.FontSize += delta;
            ApplyChanges();
        }
    }

    private int DrawRow(string name, string size, GUIStyle style)
    {
        float viewWidth = EditorGUIUtility.currentViewWidth;
        using (new GUILayout.HorizontalScope(style, GUILayout.MaxWidth(viewWidth)))
        {
            GUILayout.Label(name);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(25))) return -1;

            using (new EditorGUI.DisabledGroupScope(true))
            {
                GUILayout.Label(size, EditorStyles.miniButtonMid, GUILayout.Width(35));
            }

            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(25))) return 1;
        }
        return 0;
    }

    private void OnGUI()
    {
        InitStyles();
        InitProperties();

        int rowCount = 0;
        using (var scope = new GUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scope.scrollPosition;

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload Config", EditorStyles.miniButtonLeft))
                {
                    _config = ReadDictionary(ConfigPath);
                    _initialized = false;
                    InitProperties();
                    ApplyChanges();
                }
                if (GUILayout.Button("Force UI Toolkit Refresh", EditorStyles.miniButtonRight))
                {
                    ApplyUIToolkitFontScalingDirect();
                }
            }

            GUILayout.Space(8);

            GUILayout.Label("Unity 6 (UI Toolkit Windows - Hierarchy/Project)", EditorStyles.boldLabel);
            int uitDelta = DrawRow("UI Toolkit Hierarchy/Inspector Size", _uitoolkitFontSize.ToString(), _oddBG);
            if (uitDelta != 0)
            {
                _uitoolkitFontSize = Mathf.Max(8, _uitoolkitFontSize + uitDelta);
                ApplyChanges();
            }

            GUILayout.Space(10);
            GUILayout.Label("Legacy IMGUI Styles", EditorStyles.boldLabel);

            if (_config.TryGetValue("editor.miniLabel", out int miniLabelSize))
            {
                int delta = DrawRow("Global IMGUI Zoom", miniLabelSize.ToString(), _oddBG);
                if (delta != 0)
                {
                    if (_editorStyles != null) for (int i = 0; i < _editorStyles.Count; i++) _editorStyles[i].FontSize += delta;
                    if (_guiStyles != null) for (int i = 0; i < _guiStyles.Count; i++) _guiStyles[i].FontSize += delta;
                    if (_customStyles != null) for (int i = 0; i < _customStyles.Count; i++) _customStyles[i].FontSize += delta;
                    ApplyChanges();
                }
            }

            if (_editorStyles != null && Header("Editor Styles"))
            {
                for (int i = 0; i < _editorStyles.Count; i++)
                {
                    FontSizeRow(_editorStyles[i], rowCount % 2 == 0);
                    ++rowCount;
                }
            }
            if (_guiStyles != null && Header("GUI Skins"))
            {
                for (int i = 0; i < _guiStyles.Count; i++)
                {
                    FontSizeRow(_guiStyles[i], rowCount % 2 == 0);
                    ++rowCount;
                }
            }
            if (_customStyles != null && Header("Custom Styles"))
            {
                for (int i = 0; i < _customStyles.Count; i++)
                {
                    FontSizeRow(_customStyles[i], rowCount % 2 == 0);
                    ++rowCount;
                }
            }
        }
    }
}

#endif
