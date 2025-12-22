using System;
using System.Xml.Linq;
using Engine;

namespace Game {
    public class ManuallyInputBlockValueDialog : Dialog {
        public readonly BevelledRectangleWidget m_valueTextBoxBorder;
        public readonly TextBoxWidget m_valueTextBox;
        public readonly BevelledRectangleWidget m_contentTextBoxBorder;
        public readonly TextBoxWidget m_contentTextBox;
        public readonly BevelledRectangleWidget m_dataTextBoxBorder;
        public readonly TextBoxWidget m_dataTextBox;
        public readonly BlockIconWidget m_previewIcon;
        public readonly LabelWidget m_displayNameLabel;
        public readonly ButtonWidget m_okButton;
        public readonly ButtonWidget m_cancelButton;

        public int m_value;
        public Action<int> m_handler;

        public static Color DefaultBevelColor = new(181, 172, 154);
        public const string fName = "ManuallyInputBlockValueDialog";

        public ManuallyInputBlockValueDialog(int value, Action<int> handler) {
            m_value = value;
            m_handler = handler;
            LoadContents(this, ContentManager.Get<XElement>("Dialogs/ManuallyInputBlockValueDialog"));
            m_valueTextBoxBorder = Children.Find<BevelledRectangleWidget>("ValueTextBoxBorder");
            m_valueTextBox = Children.Find<TextBoxWidget>("ValueTextBox");
            m_contentTextBoxBorder = Children.Find<BevelledRectangleWidget>("ContentTextBoxBorder");
            m_contentTextBox = Children.Find<TextBoxWidget>("ContentTextBox");
            m_dataTextBoxBorder = Children.Find<BevelledRectangleWidget>("DataTextBoxBorder");
            m_dataTextBox = Children.Find<TextBoxWidget>("DataTextBox");
            m_previewIcon = Children.Find<BlockIconWidget>("PreviewIcon");
            m_displayNameLabel = Children.Find<LabelWidget>("DisplayNameLabel");
            m_okButton = Children.Find<ButtonWidget>("OkButton");
            m_cancelButton = Children.Find<ButtonWidget>("CancelButton");
            m_valueTextBox.Text = value.ToString();
            m_valueTextBox.TextChanged += textBoxWidget => {
                if (int.TryParse(textBoxWidget.Text, out int newValue)
                    && Terrain.ExtractContents(newValue) != 0) {
                    m_valueTextBoxBorder.BevelColor = DefaultBevelColor;
                    if (newValue != m_value) {
                        m_value = newValue;
                        m_contentTextBox.Text = Terrain.ExtractContents(newValue).ToString();
                        m_dataTextBox.Text = Terrain.ExtractData(newValue).ToString();
                        UpdatePreview(m_value);
                    }
                }
                else {
                    m_valueTextBoxBorder.BevelColor = Color.Red;
                    UpdatePreview(0);
                }
            };
            m_contentTextBox.Text = Terrain.ExtractContents(value).ToString();
            m_contentTextBox.TextChanged += textBoxWidget => {
                if (int.TryParse(textBoxWidget.Text, out int newContent)
                    && newContent != 0
                    && newContent < 1024) {
                    m_contentTextBoxBorder.BevelColor = DefaultBevelColor;
                    if (newContent != Terrain.ExtractContents(m_value)) {
                        m_value = Terrain.ReplaceContents(m_value, newContent);
                        m_valueTextBox.Text = m_value.ToString();
                        UpdatePreview(m_value);
                    }
                }
                else {
                    m_contentTextBoxBorder.BevelColor = Color.Red;
                    UpdatePreview(0);
                }
            };
            m_dataTextBox.Text = Terrain.ExtractData(value).ToString();
            m_dataTextBox.TextChanged += textBoxWidget => {
                if (int.TryParse(textBoxWidget.Text, out int newData)
                    && newData != Terrain.ExtractData(m_value)
                    && newData >= 0
                    && newData < 0x80000) {
                    m_dataTextBoxBorder.BevelColor = DefaultBevelColor;
                    m_value = Terrain.ReplaceData(m_value, newData);
                    m_valueTextBox.Text = m_value.ToString();
                    UpdatePreview(m_value);
                }
                else {
                    m_dataTextBoxBorder.BevelColor = Color.Red;
                    UpdatePreview(0);
                }
            };
            UpdatePreview(value);
        }

        public override void Update() {
            if (m_okButton.IsClicked) {
                if (int.TryParse(m_valueTextBox.Text, out int result)
                    && result != 0) {
                    Dismiss(Terrain.ReplaceLight(result, 0));
                }
                else {
                    DialogsManager.ShowDialog(
                        this,
                        new MessageDialog(LanguageControl.Error, LanguageControl.Get(fName, "1"), LanguageControl.Ok, null, null)
                    );
                }
            }
            if (Input.Cancel
                || m_cancelButton.IsClicked) {
                Dismiss(0);
            }
        }

        public void UpdatePreview(int value) {
            if (value == 0) {
                m_previewIcon.Value = 0;
                m_displayNameLabel.Text = LanguageControl.Get(fName, "1");
                return;
            }
            m_previewIcon.Value = value;
            m_previewIcon.Light = 15;
            m_displayNameLabel.Text = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetDisplayName(null, value);
        }

        public void Dismiss(int result) {
            DialogsManager.HideDialog(this);
            m_handler?.Invoke(result);
        }
    }
}