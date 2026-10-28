using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using TextCraft.src.Core.Save;
using TextCraft.src.Rendering.UI;

namespace TextCraft.src.UI
{
    /// <summary>
    /// “开始游戏”界面：列出存档（下拉框）+ 创建新世界（名称/种子输入框）。
    ///
    /// 存档信息来自 <see cref="SaveList"/>（扫描 Save/&lt;世界名&gt;/worldData.xml），
    /// 不改动存档格式；创建新世界复用现有的 <c>LoadWorldEventArg</c> 流程。
    /// </summary>
    public class UIWorldSelectPanel : UIPanel
    {
        private readonly UIDropdown _saveDropdown;
        private readonly UILabel _saveInfo;
        private readonly UITextField _nameField;
        private readonly UITextField _seedField;
        private readonly UILabel _message;
        private readonly UIButton _loadButton;
        private readonly UIButton _createButton;

        private List<SaveSlotInfo> _saves = new();

        public UIWorldSelectPanel()
        {
            Name = "worldSelect";
            Layer = UILayers.Panel;
            SpriteKey = UISkin.White;
            Color = new Vector4(0.04f, 0.05f, 0.08f, 0.94f);
            // 全屏铺满（否则默认尺寸只有 160x32，卡片会挤在左上角）
            UILayout.Stretch(this, UIAnchorPreset.StretchAll, 0f, 0f, 0f, 0f);

            var theme = UIContext.Theme;
            var font = theme?.Font;
            var fontSmall = theme?.FontSmall;

            // 标题
            var title = new UILabel("开始游戏")
            {
                Name = "title",
                Font = theme?.FontTitle,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                Shadow = true,
            };
            UILayout.Place(title, UIAnchorPreset.Top, 0f, 34f, 1000f, 66f);
            Add(title);

            // 卡片
            var card = new UIPanel { Name = "card", SpriteKey = UISkin.Window };
            UILayout.Place(card, UIAnchorPreset.Center, 0f, 26f, 620f, 424f);
            Add(card);

            var section1 = new UILabel("选择存档")
            {
                Name = "savesTitle",
                Font = font,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorAccent),
            };
            UILayout.Place(section1, UIAnchorPreset.TopLeft, 24f, 20f, 300f, 26f);
            card.Add(section1);

            _saveDropdown = new UIDropdown { Name = "saveList", Font = fontSmall, Placeholder = "（还没有存档）" };
            UILayout.Place(_saveDropdown, UIAnchorPreset.TopLeft, 24f, 50f, 572f, 38f);
            _saveDropdown.SelectionChanged += _ => RefreshSelectedSaveInfo();
            card.Add(_saveDropdown);

            _saveInfo = new UILabel
            {
                Name = "saveInfo",
                Font = fontSmall,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(_saveInfo, UIAnchorPreset.TopLeft, 24f, 94f, 572f, 22f);
            card.Add(_saveInfo);

            _loadButton = new UIButton { Name = "loadButton", Text = "读取并进入", Font = font };
            UILayout.Place(_loadButton, UIAnchorPreset.TopRight, -24f, 122f, 190f, 40f);
            _loadButton.Clicked += _ => RequestLoadSelected();
            card.Add(_loadButton);

            var divider = new UIImage { Name = "divider" };
            divider.Sprite = theme?.Sprite(UISkin.Divider);
            UILayout.Place(divider, UIAnchorPreset.TopLeft, 24f, 176f, 572f, 2f);
            card.Add(divider);

            var section2 = new UILabel("创建新世界")
            {
                Name = "createTitle",
                Font = font,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorAccent),
            };
            UILayout.Place(section2, UIAnchorPreset.TopLeft, 24f, 190f, 300f, 26f);
            card.Add(section2);

            var nameLabel = new UILabel("名称")
            {
                Name = "nameLabel",
                Font = font,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Place(nameLabel, UIAnchorPreset.TopLeft, 24f, 222f, 60f, 34f);
            card.Add(nameLabel);

            _nameField = new UITextField
            {
                Name = "nameField",
                Font = font,
                Placeholder = "新世界名称（英文/数字）",
                MaxLength = SaveList.MaxNameLength,
            };
            UILayout.Place(_nameField, UIAnchorPreset.TopLeft, 92f, 222f, 380f, 34f);
            _nameField.Submitted += _ => RequestCreate();
            card.Add(_nameField);

            var seedLabel = new UILabel("种子")
            {
                Name = "seedLabel",
                Font = font,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
            };
            UILayout.Place(seedLabel, UIAnchorPreset.TopLeft, 24f, 266f, 60f, 34f);
            card.Add(seedLabel);

            _seedField = new UITextField
            {
                Name = "seedField",
                Font = font,
                Placeholder = "留空 = 随机",
                MaxLength = 12,
                ContentType = UITextContentType.Integer,
            };
            UILayout.Place(_seedField, UIAnchorPreset.TopLeft, 92f, 266f, 240f, 34f);
            _seedField.Submitted += _ => RequestCreate();
            card.Add(_seedField);

            var seedHint = new UILabel("整数，可留空让系统随机")
            {
                Name = "seedHint",
                Font = fontSmall,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(seedHint, UIAnchorPreset.TopLeft, 342f, 266f, 254f, 34f);
            card.Add(seedHint);

            _message = new UILabel
            {
                Name = "message",
                Font = fontSmall,
                Align = UITextAlign.Left,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorDanger),
            };
            UILayout.Place(_message, UIAnchorPreset.TopLeft, 24f, 308f, 572f, 22f);
            card.Add(_message);

            _createButton = new UIButton { Name = "createButton", Text = "创建并进入", Font = font };
            UILayout.Place(_createButton, UIAnchorPreset.TopRight, -24f, 338f, 200f, 42f);
            _createButton.Clicked += _ => RequestCreate();
            card.Add(_createButton);

            var backButton = new UIButton { Name = "backButton", Text = "返回", Font = font };
            UILayout.Place(backButton, UIAnchorPreset.TopLeft, 24f, 338f, 130f, 42f);
            backButton.Clicked += _ => Back?.Invoke();
            card.Add(backButton);

            var hint = new UILabel("提示：Enter 可直接创建；存档保存在 Save\\世界名 下")
            {
                Name = "hint",
                Font = fontSmall,
                Align = UITextAlign.Center,
                VerticalAlign = UIVerticalAlign.Middle,
                TextColor = theme?.Color(UISkin.ColorTextDim),
            };
            UILayout.Place(hint, UIAnchorPreset.Bottom, 0f, -18f, 1000f, 22f);
            Add(hint);

            SetActive(false);
        }

        public event Action? Back;

        /// <summary>请求读取某个已有存档（种子由存档文件决定）。</summary>
        public event Action<string>? LoadRequested;

        /// <summary>请求创建新世界。</summary>
        public event Action<string, int>? CreateRequested;

        /// <summary>当前选中的存档（没有则为 null）。</summary>
        public SaveSlotInfo? SelectedSave
            => _saveDropdown.SelectedIndex >= 0 && _saveDropdown.SelectedIndex < _saves.Count
                ? _saves[_saveDropdown.SelectedIndex]
                : null;

        /// <summary>重新扫描存档目录并刷新下拉框。</summary>
        public void RefreshSaves()
        {
            _saves = SaveList.Enumerate();
            int visible = Math.Min(_saves.Count, _saveDropdown.MaxVisibleRows);
            var items = _saves.Take(visible).Select(s => s.Display).ToList();

            _saveDropdown.SetItems(items, 0);
            _loadButton.Interactable = _saves.Count > 0;

            if (_saves.Count == 0)
                _message.Text = "还没有存档，请在下面创建一个新世界";
            else if (_saves.Count > visible)
                _message.Text = $"共 {_saves.Count} 个存档，仅列出最近 {visible} 个";
            else
                _message.Text = "";

            if (string.IsNullOrWhiteSpace(_nameField.Text))
                _nameField.Text = SuggestWorldName();

            RefreshSelectedSaveInfo();
        }

        private void RefreshSelectedSaveInfo()
        {
            var slot = SelectedSave;
            if (slot == null)
            {
                _saveInfo.Text = "（无存档）";
                return;
            }

            string seed = slot.Seed == 0 ? "随机种子" : slot.Seed.ToString();
            _saveInfo.Text = $"世界：{slot.Name}    种子 {seed}    最后游玩 {slot.LastWriteTime:yyyy-MM-dd HH:mm}";
        }

        /// <summary>建议一个未被占用的世界名（world1/world2…）。</summary>
        private string SuggestWorldName()
        {
            for (int i = 1; i <= 999; i++)
            {
                string candidate = "world" + i;
                if (!SaveList.Exists(candidate)) return candidate;
            }
            return "world" + Environment.TickCount;
        }

        private void RequestLoadSelected()
        {
            var slot = SelectedSave;
            if (slot == null)
            {
                _message.Text = "请先选择一个存档";
                return;
            }

            _message.Text = "";
            LoadRequested?.Invoke(slot.Name);
        }

        private void RequestCreate()
        {
            string name = _nameField.Text.Trim();
            string? error = SaveList.ValidateName(name);
            if (error != null)
            {
                _message.Text = error;
                return;
            }

            if (SaveList.Exists(name))
            {
                _message.Text = $"存档 '{name}' 已存在，请换个名称";
                return;
            }

            int seed = 0;
            string seedText = _seedField.Text.Trim();
            if (seedText.Length > 0)
            {
                if (!int.TryParse(seedText, out seed))
                {
                    _message.Text = "种子必须是整数";
                    return;
                }
            }
            else
            {
                seed = Random.Shared.Next(1, int.MaxValue);
            }

            _message.Text = "";
            CreateRequested?.Invoke(name, seed);
        }
    }
}
