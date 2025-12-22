using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Engine;

namespace Game {
    public class EditMineralPathfinderDialog : Dialog {
        public enum SortOrder {
            DisplayOrderAscending,
            DisplayOrderDescending,
            ContentsAscending,
            ContentsDescending,
            NameAscending,
            NameDescending
        }

        public enum FilterMethod {
            All,
            Name,
            Contents,
            Category,
            Favorite
        }

        public SubsystemMineralPathfinderBlockBehavior m_blockBehavior;
        public ComponentPlayer m_componentPlayer;
        public MineralPathfinderBlockData m_data;

        public ButtonWidget m_sortButton;
        public ButtonWidget m_filterButton;
        public CanvasWidget m_selectorContainer;
        public MPSelectiveFlexPanelWidgetInt m_selectorPanel;
        public ButtonWidget m_selectButton;
        public RectangleWidget m_selectButtonIcon;
        public ButtonWidget m_addToFavoritesButton;
        public ButtonWidget m_viewDetailsButton;
        public ButtonWidget m_manuallyInputButton;
        public ButtonWidget m_viewListButton;
        public RectangleWidget m_addToFavoritesButtonIcon;
        public LabelWidget m_maxResultGroupCountSliderLabel;
        public SliderWidget m_maxResultGroupCountSlider;
        public LabelWidget m_scanRangeSliderLabel;
        public SliderWidget m_scanRangeSlider;
        public CheckboxWidget m_showIndicatorCheckbox;
        public LabelWidget m_messageLabel;

        public SortOrder m_sortOrder = SortOrder.DisplayOrderAscending;
        public FilterMethod m_filter = FilterMethod.All;
        public string m_filterString;
        public DateTime m_messageHideTime = DateTime.MinValue;

        public static Color SelectedColor = new(10, 70, 0, 90);

        public static float[] ScanRanges = [
            1f,
            16f,
            32f,
            64f,
            128f,
            256f,
            512f,
            float.PositiveInfinity
        ];

        public const string fName = "EditMineralPathfinderDialog";

        public EditMineralPathfinderDialog(SubsystemMineralPathfinderBlockBehavior blockBehavior,
            ComponentPlayer componentPlayer,
            MineralPathfinderBlockData data) {
            m_blockBehavior = blockBehavior;
            m_componentPlayer = componentPlayer;
            m_data = data;
            LoadContents(this, ContentManager.Get<XElement>("Dialogs/EditMineralPathfinderDialog"));
            m_sortButton = Children.Find<ButtonWidget>("SortButton");
            m_filterButton = Children.Find<ButtonWidget>("FilterButton");
            m_selectorContainer = Children.Find<CanvasWidget>("SelectorContainer");
            m_selectorPanel = m_selectorContainer.Children.Find<MPSelectiveFlexPanelWidgetInt>("SelectorPanel");
            m_selectButton = Children.Find<ButtonWidget>("SelectButton");
            m_selectButtonIcon = m_selectButton.Children.Find<RectangleWidget>("SelectButtonIcon");
            m_addToFavoritesButton = Children.Find<ButtonWidget>("AddToFavoritesButton");
            m_addToFavoritesButtonIcon = m_addToFavoritesButton.Children.Find<RectangleWidget>("AddToFavoritesButtonIcon");
            m_viewDetailsButton = Children.Find<ButtonWidget>("ViewDetailsButton");
            m_manuallyInputButton = Children.Find<ButtonWidget>("ManuallyInputButton");
            m_viewListButton = Children.Find<ButtonWidget>("ViewListButton");
            m_maxResultGroupCountSliderLabel = Children.Find<LabelWidget>("MaxResultGroupCountSliderLabel");
            m_maxResultGroupCountSlider = Children.Find<SliderWidget>("MaxResultGroupCountSlider");
            m_scanRangeSliderLabel = Children.Find<LabelWidget>("ScanRangeSliderLabel");
            m_scanRangeSlider = Children.Find<SliderWidget>("ScanRangeSlider");
            m_showIndicatorCheckbox = Children.Find<CheckboxWidget>("ShowIndicatorCheckbox");
            m_messageLabel = Children.Find<LabelWidget>("MessageLabel");
            m_selectorPanel.ItemWidgetFactory = blockValue => new MineralPathfinderBlockSlotWidget {
                BlockValue = blockValue > 0 ? blockValue : 0,
                Subtexture = blockValue switch {
                    -1 => ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/Sleep"),
                    -2 => ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/SkullAndBones"),
                    _ => null
                },
                BackgroundColor = blockValue switch {
                    -1 => m_data.SleepSelected ? SelectedColor : Color.Transparent,
                    -2 => m_data.DeathSelected ? SelectedColor : Color.Transparent,
                    _ => (Terrain.ExtractContents(blockValue) == blockValue
                        ? m_data.ContentsTargets.Contains(blockValue)
                        : m_data.ValueTargets.Contains(blockValue))
                        ? SelectedColor
                        : Color.Transparent
                },
                IsStarVisible = m_blockBehavior.FavoriteTargets.Contains(blockValue)
            };
            m_selectorPanel.SelectionChanged += SelectorSelectionChanged;
            m_selectorPanel.ItemClicked += blockValue => {
                if (blockValue == m_selectorPanel.SelectedItem) {
                    AddOrRemoveTarget(blockValue);
                }
            };
            m_maxResultGroupCountSliderLabel.Text = m_data.MaxResultGroupCount.ToString();
            m_maxResultGroupCountSlider.Value = m_data.MaxResultGroupCount;
            m_scanRangeSliderLabel.Text = m_data.ScanRange == float.PositiveInfinity ? "∞" : m_data.ScanRange.ToString(CultureInfo.InvariantCulture);
            m_scanRangeSlider.Value = ScanRanges.IndexOf(m_data.ScanRange);
            m_showIndicatorCheckbox.IsChecked = m_data.ShowIndicator;
            int selectorRowCount = SettingsManager.UIScale switch {
                >= 1f => 3,
                >= 0.85f => 4,
                _ => 5
            };
            m_selectorContainer.Size = new Vector2(372f, selectorRowCount * 72f - 40f);
            Size = new Vector2(680f, selectorRowCount * 72f + 94f);
            ResetSelector();
        }

        public override void Update() {
            if (m_sortButton.IsClicked) {
                DialogsManager.ShowDialog(
                    m_componentPlayer.GuiWidget,
                    new ListSelectionDialog(
                        LanguageControl.Get(fName, "1"),
                        (SortOrder[])Enum.GetValues(typeof(SortOrder)),
                        56f,
                        o => o is SortOrder sortOrder
                            ? new LabelWidget {
                                Text = LanguageControl.Get(fName, "SortOrder", sortOrder.ToString()),
                                Color = sortOrder == m_sortOrder ? new Color(50, 150, 35) : Color.White,
                                HorizontalAlignment = WidgetAlignment.Center,
                                VerticalAlignment = WidgetAlignment.Center
                            }
                            : null,
                        o => {
                            if (o is SortOrder sortOrder) {
                                m_sortOrder = sortOrder;
                                ResetSelector();
                            }
                        }
                    )
                );
            }
            if (m_filterButton.IsClicked) {
                List<string> items = ["All", "Name", "Contents", "Favorite"];
                items.AddRange(BlocksManager.Categories);
                DialogsManager.ShowDialog(
                    m_componentPlayer.GuiWidget,
                    new ListSelectionDialog(
                        LanguageControl.Get(fName, "2"),
                        items,
                        56f,
                        o => o is string str
                            ? new LabelWidget {
                                Text = str switch {
                                    "All" => LanguageControl.Get(fName, "FilterMethod", "All"),
                                    "Name" => LanguageControl.Get(fName, "FilterMethod", "Name"),
                                    "Contents" => LanguageControl.Get(fName, "FilterMethod", "Contents"),
                                    "Favorite" => LanguageControl.Get(fName, "FilterMethod", "Favorite"),
                                    _ => LanguageControl.Get("BlocksManager", str)
                                },
                                Color = str switch {
                                    "Favorite" => new Color(255, 255, 0),
                                    "Minerals" => new Color(128, 128, 128),
                                    "Electrics" => new Color(128, 140, 255),
                                    "Plants" => new Color(64, 160, 64),
                                    "Weapons" => new Color(255, 128, 112),
                                    _ => Color.White
                                },
                                HorizontalAlignment = WidgetAlignment.Center,
                                VerticalAlignment = WidgetAlignment.Center
                            }
                            : null,
                        o => {
                            if (o is string str) {
                                switch (str) {
                                    case "All":
                                        m_filter = FilterMethod.All;
                                        m_filterString = null;
                                        ResetSelector();
                                        break;
                                    case "Name": PopupSearchDialog(FilterMethod.Name); break;
                                    case "Contents": PopupSearchDialog(FilterMethod.Contents); break;
                                    case "Favorite":
                                        m_filter = FilterMethod.Favorite;
                                        m_filterString = null;
                                        ResetSelector();
                                        break;
                                    default:
                                        m_filter = FilterMethod.Category;
                                        m_filterString = str;
                                        ResetSelector();
                                        break;
                                }
                            }
                        }
                    )
                );
            }
            int selectedBlockValue = m_selectorPanel.SelectedItem;
            if (selectedBlockValue != 0) {
                if (m_selectButton.IsClicked) {
                    AddOrRemoveTarget(selectedBlockValue);
                }
                if (m_addToFavoritesButton.IsClicked) {
                    if (m_blockBehavior.FavoriteTargets.Remove(selectedBlockValue)) {
                        m_addToFavoritesButtonIcon.FillColor = Color.Yellow;
                        if (m_selectorPanel.SelectedIndex.HasValue
                            && m_selectorPanel.m_widgetsByIndex.TryGetValue(m_selectorPanel.SelectedIndex.Value, out Widget widget)
                            && widget is MineralPathfinderBlockSlotWidget slotWidget) {
                            slotWidget.IsStarVisible = false;
                        }
                    }
                    else if (m_blockBehavior.FavoriteTargets.Add(selectedBlockValue)) {
                        m_addToFavoritesButtonIcon.FillColor = Color.Gray;
                        if (m_selectorPanel.SelectedIndex.HasValue
                            && m_selectorPanel.m_widgetsByIndex.TryGetValue(m_selectorPanel.SelectedIndex.Value, out Widget widget)
                            && widget is MineralPathfinderBlockSlotWidget slotWidget) {
                            slotWidget.IsStarVisible = true;
                        }
                    }
                }
                if (m_viewDetailsButton.IsClicked) {
                    switch (selectedBlockValue) {
                        case -1: DisplayMessage(LanguageControl.Get(fName, "7")); break;
                        case -2: DisplayMessage(LanguageControl.Get(fName, "8")); break;
                        default:
                            ScreensManager.SwitchScreen(
                                BlocksManager.Blocks[Terrain.ExtractContents(selectedBlockValue)].GetBlockDescriptionScreen(selectedBlockValue),
                                selectedBlockValue,
                                new[] { selectedBlockValue }
                            ); break;
                    }
                }
            }
            if (m_manuallyInputButton.IsClicked) {
                m_selectorPanel.IsDrawEnabled = false;
                DialogsManager.ShowDialog(
                    m_componentPlayer.GuiWidget,
                    new ManuallyInputBlockValueDialog(
                        selectedBlockValue > 0 ? selectedBlockValue : GrassBlock.Index,
                        value => {
                            if (value != 0) {
                                int contents = Terrain.ExtractContents(value);
                                if (contents == value) {
                                    m_data.m_contentsTargets ??= new HashSet<int>(m_data.ContentsTargets);
                                    if (m_data.ContentsTargets.Add(contents)) {
                                        UpdateSelectorState(contents, true);
                                    }
                                }
                                else {
                                    m_data.m_valueTargets ??= new HashSet<int>();
                                    if (m_data.ValueTargets.Add(value)) {
                                        UpdateSelectorState(value, true);
                                    }
                                }
                            }
                            m_selectorPanel.IsDrawEnabled = true;
                            m_selectorPanel.ScrollToItem(value);
                        }
                    )
                );
            }
            if (m_viewListButton.IsClicked) {
                m_selectorPanel.IsDrawEnabled = false;
                DialogsManager.ShowDialog(
                    m_componentPlayer.GuiWidget,
                    new EditSelectedBlockValuesListDialog(
                        m_blockBehavior,
                        m_data,
                        changed => {
                            if (changed) {
                                ResetSelector();
                            }
                            m_selectorPanel.IsDrawEnabled = true;
                        }
                    )
                );
            }
            m_data.MaxResultGroupCount = (int)m_maxResultGroupCountSlider.Value;
            m_data.ScanRange = ScanRanges[(int)m_scanRangeSlider.Value];
            m_data.ShowIndicator = m_showIndicatorCheckbox.IsChecked;
            m_maxResultGroupCountSliderLabel.Text = m_data.MaxResultGroupCount.ToString();
            m_scanRangeSliderLabel.Text = m_data.ScanRange == float.PositiveInfinity ? "∞" : m_data.ScanRange.ToString(CultureInfo.InvariantCulture);
            if (m_messageLabel.IsVisible
                && DateTime.Now >= m_messageHideTime) {
                m_messageLabel.Text = string.Empty;
                m_messageLabel.IsVisible = false;
            }
            if (Input.Cancel) {
                DialogsManager.HideDialog(this);
            }
        }

        public void PopupSearchDialog(FilterMethod filterMethod) {
            string title = LanguageControl.Get(fName, "FilterMethod", filterMethod.ToString());
            string text = null;
            int maximumLength = 4;
            switch (filterMethod) {
                case FilterMethod.Contents: {
                    if (int.TryParse(m_filterString, out _)) {
                        text = m_filterString;
                    }
                    break;
                }
                case FilterMethod.Name: {
                    title += LanguageControl.Get(fName, "4");
                    if (!int.TryParse(m_filterString, out _)) {
                        text = m_filterString;
                    }
                    maximumLength = 512;
                    break;
                }
                default: return;
            }
            DialogsManager.ShowDialog(
                m_componentPlayer.GuiWidget,
                new TextBoxDialog(
                    title,
                    text,
                    maximumLength,
                    str => {
                        str = str?.Trim();
                        if (string.IsNullOrEmpty(str)) {
                            return;
                        }
                        if (m_filter == FilterMethod.Contents) {
                            if (int.TryParse(str, out int num)
                                && num > 0
                                && num < 1024) {
                                m_filter = filterMethod;
                                m_filterString = str;
                                ResetSelector();
                            }
                            else {
                                DialogsManager.ShowDialog(
                                    m_componentPlayer.GuiWidget,
                                    new MessageDialog(LanguageControl.Error, LanguageControl.Get(fName, "3"), LanguageControl.Ok, null, null)
                                );
                            }
                        }
                        else {
                            m_filter = filterMethod;
                            m_filterString = str;
                            ResetSelector();
                        }
                    }
                )
            );
        }

        public void ResetSelector() {
            m_selectorPanel.SelectedItem = 0;
            m_selectorPanel.ClearItems();
            HashSet<int> toAdd1;
            switch (m_filter) {
                case FilterMethod.Name: {
                    Regex filterRegex = new(m_filterString, RegexOptions.IgnoreCase);
                    toAdd1 = GetBaseBlockValues(value => filterRegex.IsMatch(
                            BlocksManager.Blocks[Terrain.ExtractContents(value)].GetDisplayName(null, value)
                        )
                    );
                    break;
                }
                case FilterMethod.Contents:
                    toAdd1 = int.TryParse(m_filterString, out int filterContents)
                        ? GetBaseBlockValues(value => Terrain.ExtractContents(value) == filterContents)
                        : []; break;
                case FilterMethod.Category: {
                    toAdd1 = GetBaseBlockValues(value => BlocksManager.Blocks[Terrain.ExtractContents(value)].GetCategory(value) == m_filterString);
                    break;
                }
                case FilterMethod.Favorite: {
                    toAdd1 = m_blockBehavior.FavoriteTargets;
                    break;
                }
                default: {
                    toAdd1 = GetBaseBlockValues();
                    break;
                }
            }
            IOrderedEnumerable<int> toAdd2 = toAdd1
                .OrderByDescending(value => Terrain.ExtractContents(value) == value
                    ? m_data.ContentsTargets.Contains(value)
                    : m_data.ValueTargets.Contains(value)
                )
                .ThenByDescending(value => m_blockBehavior.FavoriteTargets.Contains(value));
            toAdd2 = m_sortOrder switch {
                SortOrder.DisplayOrderDescending => toAdd2.ThenByDescending(value => BlocksManager.Blocks[Terrain.ExtractContents(value)].DisplayOrder
                ),
                SortOrder.ContentsAscending => toAdd2.ThenBy(Terrain.ExtractContents),
                SortOrder.ContentsDescending => toAdd2.ThenByDescending(Terrain.ExtractContents),
                SortOrder.NameAscending => toAdd2.ThenBy(value => BlocksManager.Blocks[Terrain.ExtractContents(value)].GetDisplayName(null, value)),
                SortOrder.NameDescending => toAdd2.ThenByDescending(value => BlocksManager.Blocks[Terrain.ExtractContents(value)]
                    .GetDisplayName(null, value)
                ),
                //DisplayOrderAscending
                _ => toAdd2.ThenBy(value => BlocksManager.Blocks[Terrain.ExtractContents(value)].DisplayOrder)
            };
            m_selectorPanel.AddItem(-1);
            m_selectorPanel.AddItem(-2);
            m_selectorPanel.AddItems(toAdd2);
        }

        public void SelectorSelectionChanged() {
            int blockValue = m_selectorPanel.SelectedItem;
            switch (blockValue) {
                case -1:
                    if (m_data.SleepSelected) {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/DoNotDisturbOn");
                        m_selectButtonIcon.FillColor = Color.Red;
                    }
                    else {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/AddCircle");
                        m_selectButtonIcon.FillColor = Color.White;
                    }
                    m_selectButton.IsEnabled = true;
                    m_addToFavoritesButtonIcon.FillColor = Color.Gray;
                    m_addToFavoritesButton.IsEnabled = false;
                    m_viewDetailsButton.IsEnabled = true;
                    break;
                case -2:
                    if (m_data.DeathSelected) {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/DoNotDisturbOn");
                        m_selectButtonIcon.FillColor = Color.Red;
                    }
                    else {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/AddCircle");
                        m_selectButtonIcon.FillColor = Color.White;
                    }
                    m_selectButton.IsEnabled = true;
                    m_addToFavoritesButtonIcon.FillColor = Color.Gray;
                    m_addToFavoritesButton.IsEnabled = false;
                    m_viewDetailsButton.IsEnabled = true;
                    break;
                case 0: break;
                default:
                    bool onlyContents = Terrain.ExtractContents(blockValue) == blockValue;
                    if (onlyContents ? m_data.ContentsTargets.Contains(blockValue) : m_data.ValueTargets.Contains(blockValue)) {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/DoNotDisturbOn");
                        m_selectButtonIcon.FillColor = Color.Red;
                    }
                    else {
                        m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/AddCircle");
                        m_selectButtonIcon.FillColor = Color.White;
                    }
                    m_selectButton.IsEnabled = true;
                    m_addToFavoritesButtonIcon.FillColor = m_blockBehavior.FavoriteTargets.Contains(blockValue) ? Color.Gray : Color.Yellow;
                    m_addToFavoritesButton.IsEnabled = true;
                    m_viewDetailsButton.IsEnabled = true;
                    break;
            }
        }

        public void AddOrRemoveTarget(int blockValue) {
            switch (blockValue) {
                case -1:
                    m_data.SleepSelected = !m_data.SleepSelected;
                    UpdateSelectorState(-1, m_data.SleepSelected);
                    break;
                case -2:
                    m_data.DeathSelected = !m_data.DeathSelected;
                    UpdateSelectorState(-2, m_data.DeathSelected);
                    break;
                case 0: break;
                default:
                    bool onlyContents = Terrain.ExtractContents(blockValue) == blockValue;
                    if (onlyContents) {
                        m_data.m_contentsTargets ??= new HashSet<int>(m_data.ContentsTargets);
                        if (m_data.ContentsTargets.Remove(blockValue)) {
                            UpdateSelectorState(blockValue, false);
                        }
                        else if (m_data.ContentsTargets.Add(blockValue)) {
                            UpdateSelectorState(blockValue, true);
                        }
                    }
                    else {
                        m_data.m_valueTargets ??= new HashSet<int>();
                        if (m_data.ValueTargets.Remove(blockValue)) {
                            UpdateSelectorState(blockValue, false);
                        }
                        else if (m_data.ValueTargets.Add(blockValue)) {
                            UpdateSelectorState(blockValue, true);
                        }
                    }
                    break;
            }
        }

        public void UpdateSelectorState(int blockValue, bool contains) {
            if (m_selectorPanel.SelectedItem != 0
                && m_selectorPanel.SelectedItem == blockValue) {
                if (contains) {
                    m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/DoNotDisturbOn");
                    m_selectButtonIcon.FillColor = Color.Red;
                }
                else {
                    m_selectButtonIcon.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/MineralPathfinder/AddCircle");
                    m_selectButtonIcon.FillColor = Color.White;
                }
            }
            int index = m_selectorPanel.m_items.IndexOf(blockValue);
            if (index >= 0
                && m_selectorPanel.m_widgetsByIndex.TryGetValue(index, out Widget widget)
                && widget is MineralPathfinderBlockSlotWidget slotWidget) {
                slotWidget.BackgroundColor = contains ? SelectedColor : Color.Transparent;
            }
            else {
                ResetSelector();
            }
        }

        public HashSet<int> GetBaseBlockValues(Func<int, bool> filter = null) {
            HashSet<int> result = [];
            if (filter == null) {
                result.UnionWith(m_blockBehavior.PlaceableBlockContents);
                result.UnionWith(m_blockBehavior.FavoriteTargets);
                result.UnionWith(m_data.ValueTargets);
            }
            else {
                foreach (int contents in m_blockBehavior.PlaceableBlockContents) {
                    if (filter(contents)) {
                        result.Add(contents);
                    }
                }
                foreach (int target in m_blockBehavior.FavoriteTargets) {
                    if (filter(target)) {
                        result.Add(target);
                    }
                }
                foreach (int target in m_data.ValueTargets) {
                    if (filter(target)) {
                        result.Add(target);
                    }
                }
            }
            return result;
        }

        public void DisplayMessage(string message) {
            m_messageLabel.Text = message;
            m_messageLabel.IsVisible = true;
            m_messageHideTime = DateTime.Now + TimeSpan.FromSeconds(4);
        }
    }
}