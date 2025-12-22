using System.Xml.Linq;
using Engine;

namespace Game {
    public class MineralPathfinderBlockSlotWidget : CanvasWidget {
        public RectangleWidget m_backgroundWidget;
        public RectangleWidget m_imageWidget;
        public BlockIconWidget m_blockIconWidget;
        public RectangleWidget m_starWidget;

        public Color BackgroundColor {
            get => field;
            set {
                field = value;
                if (value.A == 0) {
                    m_backgroundWidget.IsVisible = false;
                }
                else {
                    m_backgroundWidget.IsVisible = true;
                    m_backgroundWidget.FillColor = value;
                }
            }
        }

        public Subtexture Subtexture {
            get => m_imageWidget.Subtexture;
            set {
                m_imageWidget.IsVisible = value != null;
                m_imageWidget.Subtexture = value;
            }
        }

        public int BlockValue {
            get => m_blockIconWidget.Value;
            set {
                if (Terrain.ExtractContents(value) == 0) {
                    m_blockIconWidget.Value = 0;
                    m_blockIconWidget.IsVisible = false;
                }
                else {
                    m_blockIconWidget.Value = value;
                    m_blockIconWidget.Light = 15;
                    m_blockIconWidget.IsVisible = true;
                }
            }
        }

        public bool IsStarVisible {
            get => m_starWidget.IsVisible;
            set => m_starWidget.IsVisible = value;
        }

        public MineralPathfinderBlockSlotWidget() {
            XElement node = ContentManager.Get<XElement>("Widgets/MineralPathfinderBlockSlotWidget");
            LoadContents(this, node);
            m_backgroundWidget = Children.Find<RectangleWidget>("MineralPathfinderBlockSlotWidget.Background");
            m_imageWidget = Children.Find<RectangleWidget>("MineralPathfinderBlockSlotWidget.Image");
            m_blockIconWidget = Children.Find<BlockIconWidget>("MineralPathfinderBlockSlotWidget.Icon");
            m_starWidget = Children.Find<RectangleWidget>("MineralPathfinderBlockSlotWidget.Star");
        }
    }
}