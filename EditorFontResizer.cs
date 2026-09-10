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
        public string name;
        private GUIStyle _style;

        public int FontSize
        {
            get => _config.ContainsKey(name) ? _config[name] : (_style != null ? _style.fontSize : 12);
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

    private void OnEnable()
    {
        _config = ReadDictionary(ConfigPath);
        if (_config.TryGetValue("uitoolkit.globalFontSize", out int savedUITSize))
        {
            _uitoolkitFontSize = savedUITSize;
        }

        _initialized = false;
        EditorApplication.update += ContinuousUIToolkitScale;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ContinuousUIToolkitScale;
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
        foreach (var x in typeof(EditorStyles).GetProperties(flags))
        {
            var s = TryGetGUIStyle(x, null);
            if (s == null || trackedStyles.Contains(s)) continue;

            trackedStyles.Add(s);
            _editorStyles.Add(new StyleInfo("editor." + x.Name, s));
        }

        _guiStyles = new List<StyleInfo>();
        if (GUI.skin != null)
        {
            foreach (var x in GUI.skin.GetType().GetProperties())
            {
                var s = TryGetGUIStyle(x, GUI.skin);
                if (s == null || trackedStyles.Contains(s)) continue;

                trackedStyles.Add(s);
                _guiStyles.Add(new StyleInfo("gui." + x.Name, s));
            }

            _customStyles = new List<StyleInfo>();
            foreach (var s in GUI.skin.customStyles)
            {
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

            foreach (var styleName in hiddenProjectStyles)
            {
                GUIStyle s = GUI.skin.FindStyle(styleName);
                if (s != null && !trackedStyles.Contains(s))
                {
                    trackedStyles.Add(s);
                    _customStyles.Add(new StyleInfo("custom." + styleName, s));
                }
            }
        }

        _initialized = true;
    }

    private GUIStyle TryGetGUIStyle(PropertyInfo x, object item)
    {
        if (string.IsNullOrEmpty(x.Name) || x.PropertyType != typeof(GUIStyle)) return null;

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
    /// Bezpośrednie skalowanie w strukturze VisualElement (bez udziału pliku USS z dysku)
    /// </summary>
    private void ContinuousUIToolkitScale()
    {
        ApplyUIToolkitFontScalingDirect();
    }

    private void ApplyUIToolkitFontScalingDirect()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window == null || window.rootVisualElement == null) continue;

            // Szybka zmiana stylu w głębi drzewa domyślnego dla elementów tekstowych
            var labels = window.rootVisualElement.Query<Label>().Build();
            foreach (var label in labels)
            {
                label.style.fontSize = _uitoolkitFontSize;
            }

            var buttons = window.rootVisualElement.Query<Button>().Build();
            foreach (var btn in buttons)
            {
                btn.style.fontSize = _uitoolkitFontSize;
            }

            var textElements = window.rootVisualElement.Query(className: "unity-text-element").Build();
            foreach (var txt in textElements)
            {
                txt.style.fontSize = _uitoolkitFontSize;
            }
        }
    }

    private static void RepaintAllWindows()
    {
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            w.Repaint();
        }
    }

    private bool Header(string name)
    {
        if (!_foldouts.ContainsKey(name))
        {
            _foldouts.Add(name, true);
        }
        GUILayout.Space(5);
        bool foldout = EditorGUILayout.Foldout(!_foldouts[name], name, true);
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
        var width = GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth);
        using (new GUILayout.HorizontalScope(style, width))
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

            if (_config.ContainsKey("editor.miniLabel"))
            {
                int delta = DrawRow("Global IMGUI Zoom", _config["editor.miniLabel"].ToString(), _oddBG);
                if (delta != 0)
                {
                    if (_editorStyles != null) foreach (var style in _editorStyles) style.FontSize += delta;
                    if (_guiStyles != null) foreach (var style in _guiStyles) style.FontSize += delta;
                    if (_customStyles != null) foreach (var style in _customStyles) style.FontSize += delta;
                    ApplyChanges();
                }
            }

            if (_editorStyles != null && Header("Editor Styles"))
            {
                foreach (var style in _editorStyles)
                {
                    FontSizeRow(style, rowCount % 2 == 0);
                    ++rowCount;
                }
            }
            if (_guiStyles != null && Header("GUI Skins"))
            {
                foreach (var style in _guiStyles)
                {
                    FontSizeRow(style, rowCount % 2 == 0);
                    ++rowCount;
                }
            }
            if (_customStyles != null && Header("Custom Styles"))
            {
                foreach (var style in _customStyles)
                {
                    FontSizeRow(style, rowCount % 2 == 0);
                    ++rowCount;
                }
            }
        }
    }
}

#endif
