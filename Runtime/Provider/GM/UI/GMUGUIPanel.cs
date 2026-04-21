#if JULYGF_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JulyCore.Provider.GM
{
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class NonDrawingGraphic : Graphic
    {
        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }

    public sealed class GMUGUIPanel : MonoBehaviour
    {
        #region Theme

        static readonly Color s_bg     = new Color32(18, 18, 24, 255);
        static readonly Color s_header = new Color32(24, 24, 32, 255);
        static readonly Color s_card   = new Color32(30, 32, 40, 255);
        static readonly Color s_input  = new Color32(40, 42, 54, 255);
        static readonly Color s_tabN   = new Color32(38, 40, 50, 255);
        static readonly Color s_tabA   = new Color32(56, 130, 246, 255);
        static readonly Color s_sep    = new Color32(48, 50, 64, 255);
        static readonly Color s_run    = new Color32(46, 160, 67, 255);
        static readonly Color s_close  = new Color32(180, 50, 50, 255);
        static readonly Color s_text   = new Color32(220, 222, 230, 255);
        static readonly Color s_dim    = new Color32(140, 144, 160, 255);
        static readonly Color s_accent = new Color32(100, 180, 255, 255);

        #endregion

        IReadOnlyList<GMCategoryInfo> _categories;
        Font _font;
        int _activeTab;
        ScrollRect _scroll;
        readonly List<TabSlot> _tabs = new();
        readonly List<RectTransform> _pages = new();

        public GameObject Blocker;
        public bool IsVisible => gameObject.activeSelf;

        struct TabSlot
        {
            public Image Bg;
            public Text Label;
        }

        #region Public API

        public static GMUGUIPanel Create(Transform parent, IReadOnlyList<GMCategoryInfo> categories)
        {
            var rt = NewRT("GMPanel", parent);
            Stretch(rt);

            var panel = rt.gameObject.AddComponent<GMUGUIPanel>();
            panel._categories = categories;
            panel.Build();
            rt.gameObject.SetActive(false);
            return panel;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (Blocker) Blocker.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (Blocker) Blocker.SetActive(false);
        }

        #endregion

        #region Build

        static readonly ColorBlock s_tint = MakeTint();
        Font CachedFont => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        void Build()
        {
            var root = (RectTransform)transform;

            var bg = Img(root.gameObject, s_bg);
            bg.raycastTarget = true;

            const float hH = 100f, tH = 72f, sH = 2f;

            BuildHeader(root, hH);
            BuildTabBar(root, hH, tH);
            Img(TopStrip(NewRT("Sep", root), hH + tH, sH).gameObject, s_sep);
            BuildContent(root, hH + tH + sH);

            if (_categories.Count > 0)
                SelectTab(0);
        }

        void BuildHeader(RectTransform root, float h)
        {
            var hdr = TopStrip(NewRT("Header", root), 0, h);
            Img(hdr.gameObject, s_header);

            var trt = NewRT("Title", hdr);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(28, 0);
            trt.offsetMax = new Vector2(-(h + 8), 0);
            var title = Txt(trt, "GM Console", 42, s_text, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;

            var crt = NewRT("Close", hdr);
            crt.anchorMin = new Vector2(1, 0);
            crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(1, 0.5f);
            crt.offsetMin = new Vector2(-h, 10);
            crt.offsetMax = new Vector2(-10, -10);
            var cImg = Img(crt.gameObject, s_close);
            var cBtn = crt.gameObject.AddComponent<Button>();
            cBtn.targetGraphic = cImg;
            cBtn.colors = s_tint;
            cBtn.onClick.AddListener(Hide);

            var xTxt = Txt(NewRT("X", crt), "\u2715", 38, Color.white, FontStyle.Bold);
            Stretch(xTxt.rectTransform);
            xTxt.alignment = TextAnchor.MiddleCenter;
        }

        void BuildTabBar(RectTransform root, float top, float h)
        {
            var srt = TopStrip(NewRT("TabScroll", root), top, h);

            var vp = NewRT("Viewport", srt);
            Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            vp.gameObject.AddComponent<NonDrawingGraphic>().raycastTarget = true;

            var ct = NewRT("Content", vp);
            ct.anchorMin = new Vector2(0, 0);
            ct.anchorMax = new Vector2(0, 1);
            ct.pivot = new Vector2(0, 0.5f);
            ct.sizeDelta = Vector2.zero;

            var hlg = ct.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(20, 20, 6, 6);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            ct.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var sr = srt.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.viewport = vp;
            sr.content = ct;

            for (int i = 0; i < _categories.Count; i++)
            {
                int idx = i;
                var tab = NewRT($"Tab{i}", ct);
                var le = tab.gameObject.AddComponent<LayoutElement>();

                var tabImg = Img(tab.gameObject, s_tabN);
                var tabTxt = Txt(NewRT("L", tab), _categories[i].Category, 32, s_dim);
                Stretch(tabTxt.rectTransform);
                tabTxt.alignment = TextAnchor.MiddleCenter;

                le.preferredWidth = Mathf.Max(100, tabTxt.preferredWidth + 48);
                le.preferredHeight = h - 12;

                var btn = tab.gameObject.AddComponent<Button>();
                btn.targetGraphic = tabImg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SelectTab(idx));

                _tabs.Add(new TabSlot { Bg = tabImg, Label = tabTxt });
            }
        }

        void BuildContent(RectTransform root, float top)
        {
            var srt = NewRT("Scroll", root);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = new Vector2(0, -top);

            var vp = NewRT("Viewport", srt);
            Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            vp.gameObject.AddComponent<NonDrawingGraphic>().raycastTarget = true;

            var sr = srt.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.viewport = vp;
            _scroll = sr;

            for (int i = 0; i < _categories.Count; i++)
            {
                var page = NewRT($"Page{i}", vp);
                page.anchorMin = new Vector2(0, 1);
                page.anchorMax = new Vector2(1, 1);
                page.pivot = new Vector2(0.5f, 1);
                page.sizeDelta = Vector2.zero;

                var vlg = page.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 16;
                vlg.padding = new RectOffset(20, 20, 16, 20);
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                page.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                foreach (var cmd in _categories[i].Commands)
                    BuildCard(page, cmd);

                page.gameObject.SetActive(false);
                _pages.Add(page);
            }
        }

        void BuildCard(RectTransform parent, GMCommandInfo cmd)
        {
            var card = NewRT("Card", parent);
            Img(card.gameObject, s_card);

            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var nrt = NewRT("Name", card);
            nrt.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            Txt(nrt, cmd.DisplayName, 34, s_accent, FontStyle.Bold);

            Func<object>[] bindings = Array.Empty<Func<object>>();

            if (cmd.Params.Length > 0)
            {
                var sepRt = NewRT("Sep", card);
                sepRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
                Img(sepRt.gameObject, s_sep);

                bindings = new Func<object>[cmd.Params.Length];
                for (int i = 0; i < cmd.Params.Length; i++)
                    bindings[i] = AddParam(card, cmd.Params[i]);
            }

            var row = NewRT("RunRow", card);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            var rhlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rhlg.childAlignment = TextAnchor.MiddleRight;
            rhlg.childForceExpandWidth = false;
            rhlg.childForceExpandHeight = false;

            NewRT("Spacer", row).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var brt = NewRT("Run", row);
            var ble = brt.gameObject.AddComponent<LayoutElement>();
            ble.preferredWidth = 150;
            ble.preferredHeight = 56;
            var bImg = Img(brt.gameObject, s_run);
            var bBtn = brt.gameObject.AddComponent<Button>();
            bBtn.targetGraphic = bImg;
            bBtn.colors = s_tint;
            bBtn.onClick.AddListener(() => Exec(cmd, bindings));

            var bTxt = Txt(NewRT("L", brt), "\u6267\u884c", 32, Color.white, FontStyle.Bold);
            Stretch(bTxt.rectTransform);
            bTxt.alignment = TextAnchor.MiddleCenter;
        }

        #endregion

        #region Param Widgets

        Func<object> AddParam(RectTransform parent, GMParamInfo p)
        {
            var row = NewRT("P", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var lrt = NewRT("Label", row);
            lrt.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
            Txt(lrt, p.DisplayName, 32, s_dim);

            if (p.ParamType == typeof(bool))
                return BoolWidget(row, p.DefaultValue is true);
            if (p.ParamType.IsEnum)
                return EnumWidget(row, p);
            return InputWidget(row, p);
        }

        Func<object> InputWidget(RectTransform parent, GMParamInfo p)
        {
            var rt = NewRT("Input", parent);
            rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Img(rt.gameObject, s_input);

            var area = NewRT("Area", rt);
            Stretch(area);
            area.offsetMin = new Vector2(12, 4);
            area.offsetMax = new Vector2(-12, -4);
            area.gameObject.AddComponent<RectMask2D>();

            var txt = Txt(area, "", 32, s_text);
            Stretch(txt.rectTransform);
            txt.supportRichText = false;

            var ph = Txt(NewRT("PH", area), "...", 32,
                new Color(s_dim.r, s_dim.g, s_dim.b, 0.5f));
            Stretch(ph.rectTransform);
            ph.fontStyle = FontStyle.Italic;

            var inp = rt.gameObject.AddComponent<InputField>();
            inp.textComponent = txt;
            inp.placeholder = ph;
            inp.text = p.DefaultValue?.ToString() ?? "";

            if (p.ParamType == typeof(int))
                inp.contentType = InputField.ContentType.IntegerNumber;
            else if (p.ParamType == typeof(float))
                inp.contentType = InputField.ContentType.DecimalNumber;

            return () => ParseInput(p, inp.text);
        }

        Func<object> BoolWidget(RectTransform parent, bool init)
        {
            var state = new[] { init };

            var rt = NewRT("Bool", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;
            var img = Img(rt.gameObject, init ? s_accent : s_input);

            var lbl = Txt(NewRT("L", rt), init ? "ON" : "OFF", 32,
                init ? Color.white : s_dim, FontStyle.Bold);
            Stretch(lbl.rectTransform);
            lbl.alignment = TextAnchor.MiddleCenter;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                state[0] = !state[0];
                img.color = state[0] ? s_accent : s_input;
                lbl.text = state[0] ? "ON" : "OFF";
                lbl.color = state[0] ? Color.white : s_dim;
            });

            return () => (object)state[0];
        }

        Func<object> EnumWidget(RectTransform parent, GMParamInfo p)
        {
            var names = Enum.GetNames(p.ParamType);
            var sel = new[] { Mathf.Max(0, Array.IndexOf(names, p.DefaultValue?.ToString())) };
            var imgs = new List<Image>();
            var txts = new List<Text>();

            var rt = NewRT("Enum", parent);
            rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var ort = NewRT($"E{i}", rt);
                var oImg = Img(ort.gameObject, i == sel[0] ? s_tabA : s_input);
                var oTxt = Txt(NewRT("L", ort), names[i], 28,
                    i == sel[0] ? Color.white : s_dim);
                Stretch(oTxt.rectTransform);
                oTxt.alignment = TextAnchor.MiddleCenter;

                imgs.Add(oImg);
                txts.Add(oTxt);

                var ob = ort.gameObject.AddComponent<Button>();
                ob.targetGraphic = oImg;
                ob.transition = Selectable.Transition.None;
                ob.onClick.AddListener(() =>
                {
                    sel[0] = idx;
                    for (int j = 0; j < imgs.Count; j++)
                    {
                        imgs[j].color = j == idx ? s_tabA : s_input;
                        txts[j].color = j == idx ? Color.white : s_dim;
                    }
                });
            }

            return () => Enum.Parse(p.ParamType, names[sel[0]]);
        }

        #endregion

        #region Interaction

        void SelectTab(int idx)
        {
            if (idx < 0 || idx >= _categories.Count) return;

            _activeTab = idx;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Bg.color = i == idx ? s_tabA : s_tabN;
                _tabs[i].Label.color = i == idx ? Color.white : s_dim;
            }

            for (int i = 0; i < _pages.Count; i++)
                _pages[i].gameObject.SetActive(i == idx);

            if (idx < _pages.Count)
            {
                _scroll.content = _pages[idx];
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        void Exec(GMCommandInfo cmd, Func<object>[] bindings)
        {
            try
            {
                var args = new object[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                    args[i] = bindings[i]();
                cmd.Invoke(args);
                Debug.Log($"[GM] Executed: {cmd.DisplayName}");
                if (cmd.CloseAfter) Hide();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GM] Error executing {cmd.DisplayName}: {e.Message}");
            }
        }

        static object ParseInput(GMParamInfo p, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return p.DefaultValue;
            var t = p.ParamType;
            if (t == typeof(int)) return int.TryParse(raw, out var iv) ? iv : p.DefaultValue;
            if (t == typeof(float)) return float.TryParse(raw, out var fv) ? fv : p.DefaultValue;
            if (t == typeof(string)) return raw;
            return p.DefaultValue;
        }

        #endregion

        #region Helpers

        static RectTransform NewRT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static RectTransform TopStrip(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, -top);
            return rt;
        }

        static Image Img(GameObject go, Color c)
        {
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        Text Txt(RectTransform rt, string text, int size, Color c,
            FontStyle style = FontStyle.Normal)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = CachedFont;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = style;
            t.text = text;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static ColorBlock MakeTint(float hi = 1.15f, float lo = 0.85f)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(hi, hi, hi, 1);
            cb.pressedColor = new Color(lo, lo, lo, 1);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            return cb;
        }

        #endregion
    }
}
#endif
