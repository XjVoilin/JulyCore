#if JULYGF_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JulyCore.Provider.GM
{
    public sealed class GMIMGUIPanel : MonoBehaviour
    {
        private IReadOnlyList<GMCategoryInfo> _categories;
        private bool _visible;
        private int _activeTab;
        private Vector2 _scrollPos;
        private readonly Dictionary<string, string> _paramValues = new();

        private float _lastScale;
        private bool _stylesInited;

        // drag-to-scroll
        private bool _dragging;
        private Vector2 _dragPrev;
        private Rect _scrollArea;

        // tab horizontal scroll
        private Vector2 _tabScrollPos;
        private Rect _tabArea;
        private bool _tabDragging;
        private float _tabDragPrev;

        // backgrounds
        private Texture2D _texBg;
        private Texture2D _texCard;
        private Texture2D _texHeader;
        private Texture2D _texInput;
        private Texture2D _texInputFocus;
        private Texture2D _texTabNormal;
        private Texture2D _texTabHover;
        private Texture2D _texTabActive;
        private Texture2D _texBtnNormal;
        private Texture2D _texBtnHover;
        private Texture2D _texBtnActive;
        private Texture2D _texRunNormal;
        private Texture2D _texRunHover;
        private Texture2D _texRunActive;
        private Texture2D _texCloseNormal;
        private Texture2D _texCloseHover;
        private Texture2D _texSeparator;

        // styles
        private GUIStyle _bgStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _cmdNameStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _tabNormal;
        private GUIStyle _tabActive;
        private GUIStyle _runBtnStyle;
        private GUIStyle _closeBtnStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _enumBtnStyle;
        private GUIStyle _separatorStyle;
        private GUIStyle _scrollStyle;

        private float Scale => Screen.height / 1080f;

        public bool IsVisible => _visible;
        public GameObject Blocker;

        public void Init(IReadOnlyList<GMCategoryInfo> categories)
        {
            _categories = categories;
        }

        public void Show()
        {
            _visible = true;
            if (Blocker) Blocker.SetActive(true);
        }

        public void Hide()
        {
            _visible = false;
            if (Blocker) Blocker.SetActive(false);
        }

        private void InitStyles(float scale)
        {
            if (_stylesInited && Mathf.Approximately(_lastScale, scale)) return;
            _stylesInited = true;
            _lastScale = scale;

            var fs = Mathf.RoundToInt(26 * scale);
            var titleFs = Mathf.RoundToInt(34 * scale);
            var cmdFs = Mathf.RoundToInt(28 * scale);

            // color palette
            var colBg       = new Color32(18, 18, 24, 245);
            var colHeader   = new Color32(24, 24, 32, 255);
            var colCard     = new Color32(30, 32, 40, 255);
            var colInput    = new Color32(40, 42, 54, 255);
            var colInputFoc = new Color32(50, 52, 68, 255);
            var colTabN     = new Color32(38, 40, 50, 255);
            var colTabH     = new Color32(50, 54, 68, 255);
            var colTabA     = new Color32(56, 130, 246, 255);
            var colBtnN     = new Color32(44, 46, 58, 255);
            var colBtnH     = new Color32(56, 60, 76, 255);
            var colBtnA     = new Color32(38, 40, 50, 255);
            var colRun      = new Color32(46, 160, 67, 255);
            var colRunH     = new Color32(56, 180, 80, 255);
            var colRunA     = new Color32(36, 140, 56, 255);
            var colClose    = new Color32(180, 50, 50, 255);
            var colCloseH   = new Color32(210, 60, 60, 255);
            var colSep      = new Color32(48, 50, 64, 255);
            var colText     = new Color32(220, 222, 230, 255);
            var colTextDim  = new Color32(140, 144, 160, 255);
            var colAccent   = new Color32(100, 180, 255, 255);

            _texBg         = Tex(colBg);
            _texCard       = Tex(colCard);
            _texHeader     = Tex(colHeader);
            _texInput      = Tex(colInput);
            _texInputFocus = Tex(colInputFoc);
            _texTabNormal  = Tex(colTabN);
            _texTabHover   = Tex(colTabH);
            _texTabActive  = Tex(colTabA);
            _texBtnNormal  = Tex(colBtnN);
            _texBtnHover   = Tex(colBtnH);
            _texBtnActive  = Tex(colBtnA);
            _texRunNormal  = Tex(colRun);
            _texRunHover   = Tex(colRunH);
            _texRunActive  = Tex(colRunA);
            _texCloseNormal = Tex(colClose);
            _texCloseHover = Tex(colCloseH);
            _texSeparator  = Tex(colSep);

            int R(int v) => Mathf.RoundToInt(v * scale);

            _bgStyle = new GUIStyle
            {
                normal = { background = _texBg }
            };

            _headerStyle = new GUIStyle
            {
                normal = { background = _texHeader },
                padding = new RectOffset(R(20), R(20), R(12), R(12))
            };

            _cardStyle = new GUIStyle
            {
                normal = { background = _texCard },
                padding = new RectOffset(R(16), R(16), R(12), R(12)),
                margin = new RectOffset(0, 0, 0, R(8))
            };

            _titleStyle = new GUIStyle
            {
                fontSize = titleFs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = (Color)colText }
            };

            _cmdNameStyle = new GUIStyle
            {
                fontSize = cmdFs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = (Color)colAccent }
            };

            _labelStyle = new GUIStyle
            {
                fontSize = fs,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(R(4), R(4), 0, 0),
                normal = { textColor = (Color)colTextDim }
            };

            _tabNormal = new GUIStyle
            {
                fontSize = fs,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(R(20), R(20), R(10), R(10)),
                margin = new RectOffset(0, R(6), 0, 0),
                normal = { background = _texTabNormal, textColor = (Color)colTextDim },
                hover = { background = _texTabHover, textColor = (Color)colText },
                active = { background = _texTabActive, textColor = Color.white }
            };

            _tabActive = new GUIStyle(_tabNormal)
            {
                normal = { background = _texTabActive, textColor = Color.white },
                hover = { background = _texTabActive, textColor = Color.white }
            };

            _runBtnStyle = new GUIStyle
            {
                fontSize = fs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(R(16), R(16), R(8), R(8)),
                normal = { background = _texRunNormal, textColor = Color.white },
                hover = { background = _texRunHover, textColor = Color.white },
                active = { background = _texRunActive, textColor = Color.white }
            };

            _closeBtnStyle = new GUIStyle
            {
                fontSize = cmdFs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _texCloseNormal, textColor = Color.white },
                hover = { background = _texCloseHover, textColor = Color.white },
                active = { background = _texCloseNormal, textColor = Color.white }
            };

            _textFieldStyle = new GUIStyle
            {
                fontSize = fs,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(R(10), R(10), R(8), R(8)),
                normal = { background = _texInput, textColor = (Color)colText },
                focused = { background = _texInputFocus, textColor = Color.white },
                hover = { background = _texInputFocus, textColor = (Color)colText }
            };
            _textFieldStyle.clipping = TextClipping.Clip;

            _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = fs };

            _enumBtnStyle = new GUIStyle
            {
                fontSize = fs,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(R(10), R(10), R(6), R(6)),
                normal = { background = _texBtnNormal, textColor = (Color)colTextDim },
                hover = { background = _texBtnHover, textColor = (Color)colText },
                active = { background = _texBtnActive, textColor = Color.white }
            };

            _separatorStyle = new GUIStyle
            {
                normal = { background = _texSeparator },
                fixedHeight = 1
            };

            _scrollStyle = new GUIStyle();
        }

        private void OnGUI()
        {
            if (!_visible || _categories == null || _categories.Count == 0) return;

            var scale = Scale;
            InitStyles(scale);

            var rowH = 52 * scale;

            // full screen background
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _bgStyle);

            // header bar
            var headerH = rowH + 24 * scale;
            GUI.Box(new Rect(0, 0, Screen.width, headerH), "", _headerStyle);

            GUILayout.BeginArea(new Rect(20 * scale, 0, Screen.width - 40 * scale, headerH));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label("GM Console", _titleStyle, GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", _closeBtnStyle, GUILayout.Width(rowH), GUILayout.Height(rowH)))
                Hide();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();

            // tabs — horizontal scrollable via GUI.BeginScrollView
            var tabY = headerH + 8 * scale;
            var tabH = rowH + 4 * scale;
            var tabAreaW = Screen.width - 40 * scale;
            var tabAreaX = 20 * scale;
            var tabGap = 6 * scale;

            float totalTabW = 0;
            for (int i = 0; i < _categories.Count; i++)
            {
                var s = (i == _activeTab ? _tabActive : _tabNormal);
                totalTabW += s.CalcSize(new GUIContent(_categories[i].Category)).x;
                if (i < _categories.Count - 1) totalTabW += tabGap;
            }

            _tabArea = new Rect(tabAreaX, tabY, tabAreaW, tabH);
            var tabContentRect = new Rect(0, 0, Mathf.Max(totalTabW, tabAreaW), tabH);

            HandleTabDragScroll();

            _tabScrollPos = GUI.BeginScrollView(_tabArea, _tabScrollPos, tabContentRect,
                GUIStyle.none, GUIStyle.none);
            float tabX = 0;
            for (int i = 0; i < _categories.Count; i++)
            {
                var style = i == _activeTab ? _tabActive : _tabNormal;
                var w = style.CalcSize(new GUIContent(_categories[i].Category)).x;
                if (GUI.Button(new Rect(tabX, 0, w, tabH), _categories[i].Category, style))
                {
                    _activeTab = i;
                    _scrollPos = Vector2.zero;
                }
                tabX += w + tabGap;
            }
            GUI.EndScrollView();

            // separator line
            var sepY = tabY + tabH + 4 * scale;
            GUI.Box(new Rect(0, sepY, Screen.width, 1), "", _separatorStyle);

            // content area
            var contentY = sepY + 8 * scale;
            var contentH = Screen.height - contentY - 16 * scale;
            _scrollArea = new Rect(16 * scale, contentY, Screen.width - 32 * scale, contentH);
            GUILayout.BeginArea(_scrollArea);

            HandleDragScroll();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, _scrollStyle);
            var cat = _categories[_activeTab];
            foreach (var cmd in cat.Commands)
                DrawCommand(cmd, scale, rowH);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawCommand(GMCommandInfo cmd, float scale, float rowH)
        {
            GUILayout.BeginVertical(_cardStyle);

            GUILayout.Label(cmd.DisplayName, _cmdNameStyle, GUILayout.Height(rowH));

            if (cmd.Params.Length > 0)
            {
                GUILayout.Space(4 * scale);
                GUILayout.Box("", _separatorStyle, GUILayout.ExpandWidth(true));
                GUILayout.Space(6 * scale);
            }

            for (int i = 0; i < cmd.Params.Length; i++)
            {
                var p = cmd.Params[i];
                var key = $"{cmd.DisplayName}_{i}";

                GUILayout.BeginHorizontal();
                GUILayout.Label(p.DisplayName, _labelStyle, GUILayout.Width(180 * scale), GUILayout.Height(rowH));

                if (p.ParamType == typeof(bool))
                {
                    _paramValues.TryGetValue(key, out var boolStr);
                    var boolVal = boolStr == "True";
                    var newVal = GUILayout.Toggle(boolVal, boolVal ? " ON" : " OFF", _toggleStyle,
                        GUILayout.Height(rowH));
                    _paramValues[key] = newVal.ToString();
                }
                else if (p.ParamType.IsEnum)
                {
                    _paramValues.TryGetValue(key, out var enumStr);
                    var names = Enum.GetNames(p.ParamType);
                    var currentIdx = Mathf.Max(0, Array.IndexOf(names, enumStr));
                    var newIdx = GUILayout.SelectionGrid(currentIdx, names, names.Length,
                        _enumBtnStyle, GUILayout.Height(rowH));
                    _paramValues[key] = names[newIdx];
                }
                else
                {
                    if (!_paramValues.ContainsKey(key))
                        _paramValues[key] = p.DefaultValue?.ToString() ?? "";
                    _paramValues[key] = GUILayout.TextField(_paramValues[key], _textFieldStyle,
                        GUILayout.Height(rowH));
                }

                GUILayout.EndHorizontal();

                if (i < cmd.Params.Length - 1)
                    GUILayout.Space(4 * scale);
            }

            GUILayout.Space(6 * scale);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("执行", _runBtnStyle, GUILayout.Height(rowH)))
                ExecuteCommand(cmd);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void ExecuteCommand(GMCommandInfo cmd)
        {
            try
            {
                var args = new object[cmd.Params.Length];
                for (int i = 0; i < cmd.Params.Length; i++)
                {
                    var p = cmd.Params[i];
                    var key = $"{cmd.DisplayName}_{i}";
                    _paramValues.TryGetValue(key, out var raw);
                    args[i] = ConvertParam(p, raw);
                }
                cmd.Invoke(args);
                Debug.Log($"[GM] Executed: {cmd.DisplayName}");
                if (cmd.CloseAfter) Hide();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GM] Error executing {cmd.DisplayName}: {e.Message}");
            }
        }

        private static object ConvertParam(GMParamInfo p, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return p.DefaultValue;

            var t = p.ParamType;
            if (t == typeof(int))    return int.TryParse(raw, out var iv) ? iv : p.DefaultValue;
            if (t == typeof(float))  return float.TryParse(raw, out var fv) ? fv : p.DefaultValue;
            if (t == typeof(string)) return raw;
            if (t == typeof(bool))   return raw == "True";
            if (t.IsEnum)            return Enum.TryParse(t, raw, out var ev) ? ev : p.DefaultValue;
            return p.DefaultValue;
        }

        private void HandleDragScroll()
        {
            var e = Event.current;
            var mousePos = e.mousePosition + _scrollArea.position;

            if (e.type == EventType.MouseDown && _scrollArea.Contains(mousePos))
            {
                _dragging = true;
                _dragPrev = e.mousePosition;
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                var delta = e.mousePosition - _dragPrev;
                _scrollPos.y -= delta.y;
                _dragPrev = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _dragging = false;
            }
        }

        private void HandleTabDragScroll()
        {
            var e = Event.current;

            if (e.type == EventType.MouseDown && _tabArea.Contains(e.mousePosition))
            {
                _tabDragging = true;
                _tabDragPrev = e.mousePosition.x;
            }
            else if (e.type == EventType.MouseDrag && _tabDragging)
            {
                var dx = e.mousePosition.x - _tabDragPrev;
                _tabScrollPos.x -= dx;
                _tabDragPrev = e.mousePosition.x;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _tabDragging = false;
            }
        }

        private static Texture2D Tex(Color32 col)
        {
            var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            var c = (Color)col;
            tex.SetPixels(new[] { c, c, c, c });
            tex.Apply();
            return tex;
        }
    }
}
#endif
