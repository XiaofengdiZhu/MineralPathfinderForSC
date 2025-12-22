using Engine;

namespace Game {
    public class MineralPathfinderBlock : CubeBlock, IElectricElementBlock {
        public override int GetFaceTextureSlot(int face, int value) => face switch {
            0 => 48,
            1 => 32,
            2 => 33,
            3 => 34,
            4 => 36,
            _ => 49
        };

        public ElectricElement CreateElectricElement(SubsystemElectricity subsystemElectricity, int value, int x, int y, int z) =>
            new MineralPathfinderElectricElement(subsystemElectricity, new Point3(x, y, z));

        public ElectricConnectorType? GetConnectorType(SubsystemTerrain terrain,
            int value,
            int face,
            int connectorFace,
            int x,
            int y,
            int z) => ElectricConnectorType.Input;

        public int GetConnectionMask(int value) => int.MaxValue;

        public override bool IsNonDuplicable_(int value) => Terrain.ExtractData(value) > 0;
    }
}