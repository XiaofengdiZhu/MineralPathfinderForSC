using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;

namespace Game {
    public class EditSelectedBlockValuesListDialog : Dialog {
        public SubsystemMineralPathfinderBlockBehavior m_blockBehavior;
        public MineralPathfinderBlockData m_data;
        public Action<bool> m_handler;

        public bool m_isChanged;

        public MPListPanelWidgetInt m_listPanel;
        public ButtonWidget m_removeButton;
        public ButtonWidget m_okButton;

        public EditSelectedBlockValuesListDialog(SubsystemMineralPathfinderBlockBehavior blockBehavior,
            MineralPathfinderBlockData data,
            Action<bool> handler) {
            m_blockBehavior = blockBehavior;
            m_data = data;
            m_handler = handler;
            LoadContents(this, ContentManager.Get<XElement>("Dialogs/EditSelectedBlockValuesListDialog"));
            m_listPanel = Children.Find<MPListPanelWidgetInt>("ListPanel");
            m_removeButton = Children.Find<ButtonWidget>("RemoveButton");
            m_okButton = Children.Find<ButtonWidget>("OkButton");
            m_listPanel.ItemWidgetFactory = blockValue => new StackPanelWidget {
                Direction = LayoutDirection.Horizontal,
                VerticalAlignment = WidgetAlignment.Center,
                Children = {
                    new BlockIconWidget { Value = blockValue, Light = 15, Size = new Vector2(64f) },
                    new CanvasWidget { Size = new Vector2(12f, 0) },
                    new LabelWidget {
                        Text = $"{BlocksManager.Blocks[Terrain.ExtractContents(blockValue)].GetDisplayName(null, blockValue)} ({blockValue})",
                        VerticalAlignment = WidgetAlignment.Center
                    }
                }
            };
            m_listPanel.SelectionChanged += () => {
                if (m_listPanel.SelectedItem != 0) {
                    m_removeButton.IsEnabled = true;
                }
            };
            HashSet<int> toAdd1 = [];
            toAdd1.UnionWith(data.ContentsTargets);
            toAdd1.UnionWith(data.ValueTargets);
            int[] toAdd2 = toAdd1.ToArray();
            Array.Sort(toAdd2);
            m_listPanel.AddItems(toAdd2);
        }

        public override void Update() {
            if (m_removeButton.IsClicked
                && m_listPanel.SelectedItem != 0) {
                m_isChanged = true;
                int blockValue = m_listPanel.SelectedItem;
                m_listPanel.RemoveItem(blockValue);
                if (Terrain.ExtractContents(blockValue) == blockValue) {
                    m_data.m_contentsTargets ??= new HashSet<int>(m_data.ContentsTargets);
                    m_data.ContentsTargets.Remove(blockValue);
                }
                else {
                    m_data.m_valueTargets ??= new HashSet<int>();
                    m_data.ValueTargets.Remove(blockValue);
                }
            }
            if (Input.Cancel
                || Input.Back
                || m_okButton.IsClicked
                || (Input.Tap.HasValue && !HitTest(Input.Tap.Value))) {
                DialogsManager.HideDialog(this);
                m_handler(m_isChanged);
            }
        }
    }
}