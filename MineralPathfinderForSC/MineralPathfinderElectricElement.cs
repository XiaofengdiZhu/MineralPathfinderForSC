using Engine;

namespace Game {
    public class MineralPathfinderElectricElement : ElectricElement {
        readonly SubsystemMineralPathfinderBlockBehavior m_blockBehavior;

        public bool m_isHighVoltage;

        public MineralPathfinderElectricElement(SubsystemElectricity subsystemElectricity, Point3 point) : base(
            subsystemElectricity,
            [
                new CellFace(point, 0),
                new CellFace(point, 1),
                new CellFace(point, 2),
                new CellFace(point, 3),
                new CellFace(point, 4),
                new CellFace(point, 5)
            ]
        ) => m_blockBehavior = subsystemElectricity.Project.FindSubsystem<SubsystemMineralPathfinderBlockBehavior>();

        public override bool Simulate() {
            float voltage = 0f;
            foreach (ElectricConnection connection in Connections) {
                if (connection.ConnectorType != ElectricConnectorType.Output
                    && connection.NeighborConnectorType != 0) {
                    voltage = MathUtils.Max(voltage, connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
            }
            bool isHighVoltage = IsSignalHigh(voltage);
            if (isHighVoltage != m_isHighVoltage) {
                m_isHighVoltage = isHighVoltage;
                if (m_isHighVoltage) {
                    Point3 start = CellFaces[0].Point;
                    MineralPathfinderBlockData data = m_blockBehavior.GetBlockData(start);
                    m_blockBehavior.Scan(start, data);
                }
            }
            return false;
        }
    }
}