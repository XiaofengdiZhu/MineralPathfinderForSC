using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Engine;

namespace Game {
    public class MineralPathfinderBlockData : IEditableItemData, IDisposable {
        public enum IndicatorType {
            None,
            Triangle, //只有三角形指示器
            Block //同时有三角形指示器和方块
        }

        public struct BlockValueAndCount {
            public int BlockValue;
            public int Count;

            public BlockValueAndCount(int blockValue, int count) {
                BlockValue = blockValue;
                Count = count;
            }
        }

        public static ImmutableHashSet<int> DefaultContentsTargets = ImmutableHashSet.Create(
            CoalOreBlock.Index,
            CopperOreBlock.Index,
            IronOreBlock.Index,
            SulphurChunkBlock.Index,
            DiamondOreBlock.Index,
            GermaniumOreBlock.Index,
            SaltpeterOreBlock.Index
        );

        public ISet<int> m_contentsTargets;

        //默认目标为矿物
        public ISet<int> ContentsTargets => m_contentsTargets ?? DefaultContentsTargets;

        public static EmptySet<int> EmptyIntSet = new();

        public ISet<int> m_valueTargets;
        public ISet<int> ValueTargets => m_valueTargets ?? EmptyIntSet;

        public bool m_sleepSelected;

        public bool SleepSelected {
            get => m_sleepSelected;
            set => m_sleepSelected = value;
        }

        public bool m_deathSelected;

        public bool DeathSelected {
            get => m_deathSelected;
            set => m_deathSelected = value;
        }

        public int m_maxResultGroupCount = 1;

        public int MaxResultGroupCount {
            get => m_maxResultGroupCount;
            set => m_maxResultGroupCount = value;
        }

        public float m_scanRange = float.PositiveInfinity;

        public float ScanRange {
            get => m_scanRange;
            set => m_scanRange = value;
        }

        public bool m_showIndicator = true;

        public bool ShowIndicator {
            get => m_showIndicator;
            set => m_showIndicator = value;
        }

        public readonly Dictionary<CellFace, BlockValueAndCount> m_resultVeins = [];

        public Dictionary<CellFace, BlockValueAndCount> ResultVeins => m_resultVeins;

        public List<CellFace> m_resultPath;

        public List<CellFace> ResultPath => m_resultPath;

        public Vector3[] m_resultPathStripe;

        public Vector3[] ResultPathStripe {
            get {
                if (m_resultPath == null) {
                    return null;
                }
                if (m_resultPathStripe == null) {
                    List<Vector3> stripe = new(m_resultPath.Count);
                    CellFace lastCellFace = m_resultPath[0];
                    stripe.Add(
                        new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                        + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                    );
                    CellFace lastAddedCellFace = lastCellFace;
                    int maxI = m_resultPath.Count - 1;
                    for (int i = 1; i <= maxI; i++) {
                        CellFace cellFace = m_resultPath[i];
                        if (cellFace.Face == lastAddedCellFace.Face) {
                            int temp = 0;
                            if (cellFace.X == lastAddedCellFace.X) {
                                temp = 1;
                            }
                            if (cellFace.Y == lastAddedCellFace.Y) {
                                temp++;
                            }
                            if (cellFace.Z == lastAddedCellFace.Z) {
                                temp++;
                            }
                            //实际不会大于等于3
                            if (temp >= 2) {
                                //原路返回
                                if (i >= 2
                                    && cellFace == m_resultPath[i - 2]
                                    && lastCellFace != lastAddedCellFace) {
                                    stripe.Add(
                                        new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                                        + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                                    );
                                    lastAddedCellFace = lastCellFace;
                                }
                                lastCellFace = cellFace;
                                continue;
                            }
                            stripe.Add(
                                new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                                + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                            );
                            lastAddedCellFace = lastCellFace;
                            lastCellFace = cellFace;
                            continue;
                        }
                        stripe.Add(
                            new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                            + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                        );
                        if (cellFace.X == lastCellFace.X
                            && cellFace.Y == lastCellFace.Y
                            && cellFace.Z == lastCellFace.Z) {
                            stripe.Add(
                                new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                                + (CellFace.FaceToVector3(lastCellFace.Face) + CellFace.FaceToVector3(cellFace.Face)) * 0.55f
                            );
                        }
                        else {
                            stripe.Add(
                                new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                                + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                                + CellFace.FaceToVector3(CellFace.OppositeFace(cellFace.Face)) * 0.45f
                            );
                        }
                        stripe.Add(
                            new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f) + CellFace.FaceToVector3(cellFace.Face) * 0.55f
                        );
                        lastAddedCellFace = cellFace;
                        lastCellFace = cellFace;
                    }
                    lastCellFace = m_resultPath[m_resultPath.Count - 1];
                    if (lastCellFace != lastAddedCellFace) {
                        stripe.Add(
                            new Vector3(lastCellFace.X + 0.5f, lastCellFace.Y + 0.5f, lastCellFace.Z + 0.5f)
                            + CellFace.FaceToVector3(lastCellFace.Face) * 0.55f
                        );
                    }
                    m_resultPathStripe = stripe.ToArray();
                }
                return m_resultPathStripe;
            }
        }

        public float m_resultPathIndicatorTime;
        public int m_lastResultPathIndicatorTimeInt = -1;
        public Dictionary<int, int> m_resultPathIndicatorTimeToBlockValue = [];

        public float ResultPathIndicatorTime {
            get => m_resultPathIndicatorTime;
            set {
                m_resultPathIndicatorTime = value;
                if (m_resultPathIndicatorTime >= (m_resultPath?.Count ?? 0f)) {
                    m_resultPathIndicatorTime = 0f;
                }
                if (ResultPathIndicatorType == IndicatorType.Block
                    && m_resultPath != null) {
                    int timeInt = (int)m_resultPathIndicatorTime;
                    if (timeInt != m_lastResultPathIndicatorTimeInt) {
                        m_lastResultPathIndicatorTimeInt = timeInt;
                        if (m_resultPathIndicatorTimeToBlockValue.Count == 0) {
                            int i = 0;
                            int j = 1;
                            while (j < m_resultPath.Count) {
                                if (m_resultVeins.TryGetValue(m_resultPath[j], out BlockValueAndCount pair)) {
                                    m_resultPathIndicatorTimeToBlockValue.Add(i, pair.BlockValue);
                                    i = j;
                                }
                                j++;
                            }
                        }
                        if (m_resultPathIndicatorTimeToBlockValue.TryGetValue(timeInt, out int blockValue)) {
                            m_resultPathIndicatorBlockValue = blockValue;
                        }
                    }
                }
            }
        }

        public IndicatorType m_resultPathIndicatorType = IndicatorType.Block;

        public IndicatorType ResultPathIndicatorType => m_resultPathIndicatorType;

        public int m_resultPathIndicatorBlockValue;

        public int ResultPathIndicatorBlockValue => m_resultPathIndicatorBlockValue;

        //同时完成ResultPathIndicatorBlockPosition、ResultPathIndicatorQuadVertices的计算
        public Vector3[] ResultPathIndicatorTriangleVertices {
            get {
                if (ResultPath == null) {
                    return null;
                }
                int timeInt = (int)ResultPathIndicatorTime;
                float timeFraction = ResultPathIndicatorTime - timeInt;
                CellFace indicatorCellFace = ResultPath[timeInt];
                Vector3 up = CellFace.FaceToVector3(indicatorCellFace.Face);
                Vector3 vector = new Vector3(indicatorCellFace.X + 0.5f, indicatorCellFace.Y + 0.5f, indicatorCellFace.Z + 0.5f) + up * 0.55f;
                Vector3 from;
                Vector3 to;
                if (timeFraction < 0.5f
                    && timeInt > 0) {
                    to = vector;
                    CellFace previousCellFace = ResultPath[timeInt - 1];
                    if (previousCellFace.Face == indicatorCellFace.Face) {
                        from = vector
                            + new Vector3(
                                previousCellFace.X - indicatorCellFace.X,
                                previousCellFace.Y - indicatorCellFace.Y,
                                previousCellFace.Z - indicatorCellFace.Z
                            )
                            * 0.5f;
                    }
                    else if (previousCellFace.X == indicatorCellFace.X
                        && previousCellFace.Y == indicatorCellFace.Y
                        && previousCellFace.Z == indicatorCellFace.Z) {
                        from = vector + CellFace.FaceToVector3(previousCellFace.Face) * 0.55f;
                    }
                    else {
                        from = vector + CellFace.FaceToVector3(CellFace.OppositeFace(previousCellFace.Face)) * 0.55f;
                    }
                }
                else if (timeFraction >= 0.5f
                    && timeInt < ResultPath.Count - 1) {
                    from = vector;
                    CellFace nextCellFace = ResultPath[timeInt + 1];
                    if (nextCellFace.Face == indicatorCellFace.Face) {
                        to = vector
                            + new Vector3(
                                nextCellFace.X - indicatorCellFace.X,
                                nextCellFace.Y - indicatorCellFace.Y,
                                nextCellFace.Z - indicatorCellFace.Z
                            )
                            * 0.5f;
                    }
                    else if (nextCellFace.X == indicatorCellFace.X
                        && nextCellFace.Y == indicatorCellFace.Y
                        && nextCellFace.Z == indicatorCellFace.Z) {
                        to = vector + CellFace.FaceToVector3(nextCellFace.Face) * 0.55f;
                    }
                    else {
                        to = vector + CellFace.FaceToVector3(CellFace.OppositeFace(nextCellFace.Face)) * 0.55f;
                    }
                }
                else {
                    return null;
                }
                Vector3 forward = Vector3.Normalize(to - from);
                Vector3 right = Vector3.Cross(up, forward);
                Vector3 center = Vector3.Lerp(from, to, (timeFraction >= 0.5f ? timeFraction - 0.5f : timeFraction) * 2f);
                Vector3[] result = new Vector3[3];
                result[0] = center + forward * 0.24f;
                Vector3 baseCenter = center - forward * 0.12f;
                const float halfBaseSize = 0.2078460969082652752232935609807f;
                result[1] = baseCenter - right * halfBaseSize;
                result[2] = baseCenter + right * halfBaseSize;
                if (ResultPathIndicatorType == IndicatorType.Block) {
                    m_resultPathIndicatorBlockPosition = center + up * 0.125f;
                    if (ResultPathIndicatorBlockValue is -1 or -2) {
                        m_resultPathIndicatorQuadVertices[0] = m_resultPathIndicatorBlockPosition + (forward + right) * -0.15f;
                        m_resultPathIndicatorQuadVertices[1] = m_resultPathIndicatorBlockPosition + (forward - right) * 0.15f;
                        m_resultPathIndicatorQuadVertices[2] = m_resultPathIndicatorBlockPosition + (forward + right) * 0.15f;
                        m_resultPathIndicatorQuadVertices[3] = m_resultPathIndicatorBlockPosition + (forward - right) * -0.15f;
                    }
                }
                return result;
            }
        }

        public Vector3 m_resultPathIndicatorBlockPosition;

        public Vector3 ResultPathIndicatorBlockPosition => m_resultPathIndicatorBlockPosition;

        public Vector3[] m_resultPathIndicatorQuadVertices = new Vector3[4];

        public Vector3[] ResultPathIndicatorQuadVertices => m_resultPathIndicatorQuadVertices;

        public MineralPathfinderBlockData(int maxResultGroupCount = 1,
            float scanRange = 100,
            bool showIndicator = true,
            bool sleepSelected = false,
            bool deathSelected = false,
            ISet<int> contentsTargets = null,
            ISet<int> valueTargets = null) {
            MaxResultGroupCount = maxResultGroupCount;
            ScanRange = scanRange;
            ShowIndicator = showIndicator;
            SleepSelected = sleepSelected;
            DeathSelected = deathSelected;
            m_contentsTargets = contentsTargets;
            m_valueTargets = valueTargets;
        }

        public MineralPathfinderBlockData() { }

        public IEditableItemData Copy() {
            HashSet<int> contentsTargets = null;
            if (m_contentsTargets != null) {
                contentsTargets = [];
                foreach (int target in m_contentsTargets) {
                    contentsTargets.Add(target);
                }
            }
            HashSet<int> valueTargets = null;
            if (m_valueTargets != null) {
                valueTargets = [];
                foreach (int target in m_valueTargets) {
                    valueTargets.Add(target);
                }
            }
            ResetResults();
            return new MineralPathfinderBlockData(
                m_maxResultGroupCount,
                m_scanRange,
                m_showIndicator,
                m_sleepSelected,
                m_deathSelected,
                contentsTargets,
                valueTargets
            );
        }

        public void LoadString(string data) {
            string[] array = data.Split(';');
            if (array.Length < 5) {
                return;
            }
            if (int.TryParse(array[0], out int maxResultGroupCount)) {
                MaxResultGroupCount = maxResultGroupCount;
            }
            if (float.TryParse(array[1], out float scanRange)) {
                ScanRange = scanRange;
            }
            if (int.TryParse(array[2], out int showIndicator)) {
                ShowIndicator = showIndicator != 0;
            }
            if (int.TryParse(array[3], out int sleepSelected)) {
                SleepSelected = sleepSelected != 0;
            }
            if (int.TryParse(array[4], out int deathSelected)) {
                DeathSelected = deathSelected != 0;
            }
            if (array.Length >= 6) {
                string[] array2 = array[5].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (array2.Length > 0) {
                    m_contentsTargets = new HashSet<int>(array2.Length);
                    foreach (string str in array2) {
                        if (int.TryParse(str, out int target)) {
                            m_contentsTargets.Add(target);
                        }
                    }
                }
            }
            if (array.Length >= 7) {
                string[] array3 = array[6].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (array3.Length > 0) {
                    m_valueTargets = new HashSet<int>(array3.Length);
                    foreach (string str in array3) {
                        if (int.TryParse(str, out int target)) {
                            m_valueTargets.Add(target);
                        }
                    }
                }
            }
        }

        public string SaveString() {
            if (IsDefault(this)) {
                return string.Empty;
            }
            StringBuilder sb = new(
                $"{MaxResultGroupCount};{ScanRange};{(ShowIndicator ? 1 : 0)};{(SleepSelected ? 1 : 0)};{(DeathSelected ? 1 : 0)}"
            );
            sb.Append(";");
            if (m_contentsTargets != null) {
                sb.Append(string.Join(",", m_contentsTargets));
            }
            sb.Append(";");
            if (m_valueTargets != null) {
                sb.Append(string.Join(",", m_valueTargets));
            }
            return sb.ToString();
        }

        public void ResetResults() {
            m_resultVeins.Clear();
            m_resultPath = null;
            m_resultPathStripe = null;
            m_resultPathIndicatorTimeToBlockValue.Clear();
        }

        public static bool IsDefault(MineralPathfinderBlockData data) => data.m_contentsTargets == null
            && data.m_valueTargets == null
            && data.m_maxResultGroupCount == 1
            && data.m_scanRange == float.PositiveInfinity
            && data.m_showIndicator
            && !data.m_sleepSelected
            && !data.m_deathSelected;

        public void Dispose() {
            m_contentsTargets?.Clear();
            m_valueTargets?.Clear();
            ResetResults();
        }
    }
}