using System;
using System.Collections.Generic;
using RetroRally.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RetroRally.Game
{
    /// <summary>One row of a menu: an action, or a setting with a value that left/right changes.</summary>
    public sealed class MenuItem
    {
        public string Id;
        public string Label;
        public string Value;
    }

    /// <summary>Everything a menu screen shows.</summary>
    public sealed class MenuModel
    {
        public bool Logo;
        public string Title;
        public string TitleStyle;
        public string Subtitle;
        public readonly List<(string Label, string Value, bool Highlight)> Body = new List<(string, string, bool)>();
        public readonly List<MenuItem> Items = new List<MenuItem>();
        public int Selected;
        public string Hint;
        /// <summary>A light dim lets the court show through (pause), a heavy one hides it (title).</summary>
        public bool LightDim;

        public MenuItem Add(string id, string label, string value = null)
        {
            var item = new MenuItem { Id = id, Label = label, Value = value };
            Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// Binds the UI Toolkit HUD (Hud.uxml): the match scores, prompts and banners, and a generic
    /// menu renderer for the title, settings, records, pause and results screens. Menu rows never
    /// take keyboard focus; the director reads the keys and the mouse clicks rows.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        const float FloaterLifetime = 1.1f;

        sealed class Floater
        {
            public Label Label;
            public Vector2 Position;
            public float Age;
        }

        VisualElement root;
        VisualElement playHud;
        Label nameLeft;
        Label nameRight;
        Label target;
        readonly Label[] scores = new Label[2];
        readonly VisualElement[] effectLists = new VisualElement[2];
        Label prompt;
        Label banner;
        VisualElement floaters;
        VisualElement menu;
        VisualElement menuDim;
        VisualElement logo;
        Label menuTitle;
        Label menuSubtitle;
        VisualElement menuBody;
        VisualElement menuItems;
        Label menuHint;
        Label sound;
        IVisualElementScheduledItem bannerHide;
        readonly List<Floater> active = new List<Floater>();
        readonly List<VisualElement> rows = new List<VisualElement>();
        readonly int[] shownScores = { -1, -1 };

        /// <summary>A menu row was clicked with the mouse (its index).</summary>
        public event Action<int> ItemClicked;

        /// <summary>The menu currently shown, or null.</summary>
        public MenuModel Menu { get; private set; }

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            playHud = root.Q("play-hud");
            nameLeft = root.Q<Label>("name-left");
            nameRight = root.Q<Label>("name-right");
            target = root.Q<Label>("target");
            scores[0] = root.Q<Label>("score-left");
            scores[1] = root.Q<Label>("score-right");
            effectLists[0] = root.Q("effects-left");
            effectLists[1] = root.Q("effects-right");
            prompt = root.Q<Label>("prompt");
            banner = root.Q<Label>("banner");
            floaters = root.Q("floaters");
            menu = root.Q("menu");
            menuDim = root.Q("menu-dim");
            logo = root.Q("logo");
            menuTitle = root.Q<Label>("menu-title");
            menuSubtitle = root.Q<Label>("menu-subtitle");
            menuBody = root.Q("menu-body");
            menuItems = root.Q("menu-items");
            menuHint = root.Q<Label>("menu-hint");
            sound = root.Q<Label>("sound");
        }

        // ---- Match HUD ----

        public void ShowPlayHud(bool visible) => SetVisible(playHud, visible);

        public void SetPlayers(string left, string right, string goal)
        {
            nameLeft.text = left;
            nameRight.text = right;
            target.text = goal;
        }

        public void SetScore(int left, int right)
        {
            SetScoreLabel(0, left);
            SetScoreLabel(1, right);
        }

        void SetScoreLabel(int index, int value)
        {
            if (shownScores[index] == value)
            {
                return;
            }
            bool pop = shownScores[index] >= 0 && value > shownScores[index];
            shownScores[index] = value;
            Label label = scores[index];
            label.text = value.ToString();
            if (pop)
            {
                label.AddToClassList("score--pop");
                label.schedule.Execute(() => label.RemoveFromClassList("score--pop")).ExecuteLater(260);
            }
        }

        /// <summary>Lists the timed power-ups on one side as chips with seconds left.</summary>
        public void SetEffects(Side side, IReadOnlyList<PaddleEffect> effects)
        {
            VisualElement list = effectLists[(int)side];
            int count = 0;
            foreach (PaddleEffect effect in effects)
            {
                if (effect.Target != side)
                {
                    continue;
                }
                Label chip;
                if (count < list.childCount)
                {
                    chip = (Label)list[count];
                }
                else
                {
                    chip = new Label { pickingMode = PickingMode.Ignore };
                    chip.AddToClassList("chip");
                    list.Add(chip);
                }
                Color color = Palette.Of(effect.Kind);
                chip.text = $"{PowerUpTuning.Title(effect.Kind)}  {Mathf.CeilToInt(effect.Remaining)}";
                chip.style.color = color;
                chip.style.borderTopColor = chip.style.borderBottomColor = chip.style.borderLeftColor = chip.style.borderRightColor = color;
                count++;
            }
            while (list.childCount > count)
            {
                list.RemoveAt(list.childCount - 1);
            }
        }

        public void SetPrompt(string text)
        {
            prompt.text = text ?? string.Empty;
            SetVisible(prompt, !string.IsNullOrEmpty(text));
        }

        /// <summary>Shows a big centred message for a moment.</summary>
        public void Banner(string text, string style = null, int milliseconds = 1100)
        {
            banner.text = Display(text);
            banner.EnableInClassList("banner--left", style == "left");
            banner.EnableInClassList("banner--right", style == "right");
            banner.AddToClassList("banner--show");
            bannerHide?.Pause();
            bannerHide = banner.schedule.Execute(() => banner.RemoveFromClassList("banner--show"));
            bannerHide.ExecuteLater(milliseconds);
        }

        public void HideBanner()
        {
            bannerHide?.Pause();
            banner.RemoveFromClassList("banner--show");
        }

        public string BannerText => banner.ClassListContains("banner--show") ? banner.text : null;

        /// <summary>A word that rises from a point on the court and fades.</summary>
        public void Float(Vector2 world, string text, Color color)
        {
            Camera view = Camera.main;
            if (root?.panel == null || view == null || active.Count > 20)
            {
                return;
            }
            Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, view);
            var label = new Label(Display(text)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("display");
            label.AddToClassList("floater");
            label.style.color = color;
            floaters.Add(label);
            var floater = new Floater { Label = label, Position = position };
            active.Add(floater);
            Place(floater);
        }

        public void ClearFloaters()
        {
            foreach (Floater floater in active)
            {
                floater.Label.RemoveFromHierarchy();
            }
            active.Clear();
        }

        public void SetSound(string text) => sound.text = text;

        // ---- Menus ----

        public void ShowMenu(MenuModel model)
        {
            Menu = model;
            SetVisible(menu, true);
            menuDim.EnableInClassList("menu__dim--light", model.LightDim);
            SetVisible(logo, model.Logo);
            menuTitle.text = Display(model.Title);
            SetVisible(menuTitle, !string.IsNullOrEmpty(model.Title));
            menuTitle.EnableInClassList("menu__title--left", model.TitleStyle == "left");
            menuTitle.EnableInClassList("menu__title--right", model.TitleStyle == "right");
            menuSubtitle.text = model.Subtitle ?? string.Empty;
            SetVisible(menuSubtitle, !string.IsNullOrEmpty(model.Subtitle));

            menuBody.Clear();
            foreach ((string label, string value, bool highlight) in model.Body)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("body-line");
                var left = new Label(label) { pickingMode = PickingMode.Ignore };
                left.AddToClassList("body-line__label");
                var right = new Label(value) { pickingMode = PickingMode.Ignore };
                right.AddToClassList("body-line__value");
                right.EnableInClassList("body-line__value--new", highlight);
                line.Add(left);
                line.Add(right);
                menuBody.Add(line);
            }
            SetVisible(menuBody, model.Body.Count > 0);

            menuItems.Clear();
            rows.Clear();
            for (int i = 0; i < model.Items.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("item");
                var label = new Label { name = "label", pickingMode = PickingMode.Ignore };
                label.AddToClassList("item__label");
                var value = new Label { name = "value", pickingMode = PickingMode.Ignore };
                value.AddToClassList("item__value");
                row.Add(label);
                row.Add(value);
                row.AddManipulator(new Clickable(() => ItemClicked?.Invoke(index)));
                menuItems.Add(row);
                rows.Add(row);
            }
            menuHint.text = model.Hint ?? string.Empty;
            RefreshMenu();
        }

        /// <summary>Redraws the rows' text and the highlight after a value or the selection changed.</summary>
        public void RefreshMenu()
        {
            if (Menu == null)
            {
                return;
            }
            for (int i = 0; i < rows.Count && i < Menu.Items.Count; i++)
            {
                MenuItem item = Menu.Items[i];
                rows[i].Q<Label>("label").text = item.Label;
                rows[i].Q<Label>("value").text = item.Value != null ? $"<  {item.Value}  >" : string.Empty;
                rows[i].EnableInClassList("item--selected", i == Menu.Selected);
            }
        }

        public void HideMenu()
        {
            Menu = null;
            SetVisible(menu, false);
        }

        // ---- Each frame ----

        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Floater floater = active[i];
                floater.Age += Time.unscaledDeltaTime;
                if (floater.Age >= FloaterLifetime)
                {
                    floater.Label.RemoveFromHierarchy();
                    active.RemoveAt(i);
                    continue;
                }
                Place(floater);
            }
        }

        static void Place(Floater floater)
        {
            float t = floater.Age / FloaterLifetime;
            floater.Label.style.left = floater.Position.x;
            floater.Label.style.top = floater.Position.y - 50f * Mathf.Sqrt(t);
            floater.Label.style.opacity = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            float pop = t < 0.12f ? 1f + (0.12f - t) * 4f : 1f;
            floater.Label.style.scale = new Scale(new Vector3(pop, pop, 1f));
        }

        /// <summary>
        /// Text in the Retro display face is shown in lower case: its capital C, G and U are hard
        /// to tell apart from E and H, while its lower case reads well.
        /// </summary>
        public static string Display(string text) => (text ?? string.Empty).ToLowerInvariant();

        static void SetVisible(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);
    }
}
