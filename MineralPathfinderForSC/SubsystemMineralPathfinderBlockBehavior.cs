using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Silk.NET.OpenGLES;
using TemplatesDatabase;
using BlockValueAndCount = Game.MineralPathfinderBlockData.BlockValueAndCount;
using IndicatorType = Game.MineralPathfinderBlockData.IndicatorType;

namespace Game {
    public class SubsystemMineralPathfinderBlockBehavior : SubsystemEditableItemBehavior<MineralPathfinderBlockData>, IDrawable {
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemPlayers m_subsystemPlayers;
        public SubsystemPlayerStats m_subsystemPlayerStats;
        public Terrain m_terrain;
        public bool m_isScanning;
        public DateTime m_lastScanTime = DateTime.MinValue;
        public double m_lastDrawTime;
        public readonly PrimitivesRenderer3D m_primitivesRenderer3D = new();
        public FontBatch3D m_fontBatch3D;
        public FlatBatch3D m_flatBatch3D;
        public TexturedBatch3D m_sleepBatch3D;
        public TexturedBatch3D m_deathBatch3D;
        public readonly DrawBlockEnvironmentData m_drawBlockEnvironmentData = new();
        public readonly HashSet<int> PlaceableBlockContents = [];
        public readonly HashSet<int> FavoriteTargets = [];
        public static Color PathStripeColor = new(byte.MaxValue, byte.MaxValue, (byte)0, (byte)128);
        public static Color PathIndicatorColor = new(byte.MaxValue, byte.MaxValue, (byte)0, (byte)192);
        public const float PathIndicatorSpeed = 3f;
        public const string fName = "SubsystemMineralPathfinderBlockBehavior";

        public int[] DrawOrders => [299];
        public override int[] HandledBlocks => [BlocksManager.GetBlockIndex<MineralPathfinderBlock>()];

        public SubsystemMineralPathfinderBlockBehavior() : base(BlocksManager.GetBlockIndex<MineralPathfinderBlock>()) { }

        public override void Load(ValuesDictionary valuesDictionary) {
            base.Load(valuesDictionary);
            string favoriteTargetsString = valuesDictionary.GetValue<string>("FavoriteTargets", null);
            if (!string.IsNullOrEmpty(favoriteTargetsString)) {
                string[] array = favoriteTargetsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (array.Length > 0) {
                    foreach (string str in array) {
                        if (int.TryParse(str, out int favoriteTarget)) {
                            FavoriteTargets.Add(favoriteTarget);
                        }
                    }
                }
            }
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>();
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>();
            m_subsystemPlayerStats = Project.FindSubsystem<SubsystemPlayerStats>();
            m_terrain = m_subsystemTerrain.Terrain;
            m_fontBatch3D = m_primitivesRenderer3D.FontBatch(
                LabelWidget.BitmapFont,
                1,
                DepthStencilState.DepthRead,
                RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp
            );
            m_flatBatch3D = m_primitivesRenderer3D.FlatBatch(
                0,
                DepthStencilState.DepthRead,
                RasterizerState.CullNoneScissor,
                BlendState.NonPremultiplied
            );
            InitializePlaceableBlocks();
        }

        public override void Save(ValuesDictionary valuesDictionary) {
            base.Save(valuesDictionary);
            valuesDictionary.SetValue("FavoriteTargets", string.Join(',', FavoriteTargets));
        }

        public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer) {
            if (componentPlayer.DragHostWidget.IsDragInProgress) {
                return false;
            }
            int value = inventory.GetSlotValue(slotIndex);
            int count = inventory.GetSlotCount(slotIndex);
            int id = Terrain.ExtractData(value);
            MineralPathfinderBlockData data = (MineralPathfinderBlockData)GetItemData(id)?.Copy() ?? new MineralPathfinderBlockData();
            DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditMineralPathfinderDialog(this, componentPlayer, data));
            return true;
        }

        public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer) {
            MineralPathfinderBlockData data = GetBlockData(new Point3(x, y, z));
            if (data != null) {
                DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditMineralPathfinderDialog(this, componentPlayer, data));
            }
            return true;
        }

        public override void OnItemPlaced(int x, int y, int z, ref BlockPlacementData placementData, int itemValue) {
            int id = Terrain.ExtractData(itemValue);
            if (id == 0) {
                m_blocksData[new Point3(x, y, z)] = new MineralPathfinderBlockData();
                return;
            }
            MineralPathfinderBlockData itemData = GetItemData(id);
            if (itemData != null) {
                m_blocksData[new Point3(x, y, z)] = (MineralPathfinderBlockData)itemData.Copy();
                itemData.Dispose();
            }
        }

        public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue) {
            MineralPathfinderBlockData blockData = GetBlockData(new Point3(x, y, z));
            if (blockData != null
                && !MineralPathfinderBlockData.IsDefault(blockData)) {
                int num = FindFreeItemId();
                m_itemsData.Add(num, (MineralPathfinderBlockData)blockData.Copy());
                dropValue.Value = Terrain.ReplaceData(dropValue.Value, num);
                blockData.Dispose();
            }
        }

        public void Draw(Camera camera, int drawOrder) {
            double time = Time.RealTime;
            Vector3 viewDirection = camera.ViewDirection;
            foreach ((_, MineralPathfinderBlockData data) in m_blocksData) {
                if (data.ResultVeins.Count == 0) {
                    continue;
                }
                m_flatBatch3D.QueueLineStrip(data.ResultPathStripe, PathStripeColor);
                data.ResultPathIndicatorTime += (float)(time - m_lastDrawTime) * PathIndicatorSpeed;
                if (data.ShowIndicator) {
                    Vector3[] indicatorVertices = data.ResultPathIndicatorTriangleVertices;
                    if (indicatorVertices != null
                        && camera.ViewFrustum.Intersection(data.ResultPathIndicatorBlockPosition)) {
                        m_flatBatch3D.QueueTriangle(indicatorVertices[0], indicatorVertices[1], indicatorVertices[2], PathIndicatorColor);
                        if (data.m_resultPathIndicatorType == IndicatorType.Block) {
                            m_drawBlockEnvironmentData.BillboardDirection = viewDirection;
                            m_drawBlockEnvironmentData.InWorldMatrix.Translation = data.ResultPathIndicatorBlockPosition;
                            Matrix matrix = Matrix.CreateTranslation(data.ResultPathIndicatorBlockPosition);
                            switch (data.ResultPathIndicatorBlockValue) {
                                case -1:
                                    m_sleepBatch3D ??= m_primitivesRenderer3D.TexturedBatch(
                                        ContentManager.Get<Texture2D>("Textures/Gui/MineralPathfinder/Sleep"),
                                        false,
                                        0,
                                        DepthStencilState.DepthRead,
                                        RasterizerState.CullNone,
                                        BlendState.NonPremultiplied,
                                        SamplerState.AnisotropicClamp
                                    );
                                    Vector3[] vertices = data.ResultPathIndicatorQuadVertices;
                                    m_sleepBatch3D.QueueQuad(
                                        vertices[0],
                                        vertices[1],
                                        vertices[2],
                                        vertices[3],
                                        Vector2.UnitX,
                                        Vector2.Zero,
                                        Vector2.UnitY,
                                        Vector2.One,
                                        Color.White
                                    );
                                    break;
                                case -2:
                                    m_deathBatch3D ??= m_primitivesRenderer3D.TexturedBatch(
                                        ContentManager.Get<Texture2D>("Textures/Gui/MineralPathfinder/SkullAndBones"),
                                        false,
                                        0,
                                        DepthStencilState.DepthRead,
                                        RasterizerState.CullNone,
                                        BlendState.NonPremultiplied,
                                        SamplerState.AnisotropicClamp
                                    );
                                    Vector3[] vertices1 = data.ResultPathIndicatorQuadVertices;
                                    m_deathBatch3D.QueueQuad(
                                        vertices1[0],
                                        vertices1[1],
                                        vertices1[2],
                                        vertices1[3],
                                        Vector2.UnitX,
                                        Vector2.Zero,
                                        Vector2.UnitY,
                                        Vector2.One,
                                        Color.White
                                    );
                                    break;
                                case 0: break;
                                case > 0:
                                    BlocksManager.Blocks[Terrain.ExtractContents(data.ResultPathIndicatorBlockValue)]
                                        .DrawBlock(
                                            m_primitivesRenderer3D,
                                            data.ResultPathIndicatorBlockValue,
                                            Color.White,
                                            0.2f,
                                            ref matrix,
                                            m_drawBlockEnvironmentData
                                        ); break;
                            }
                        }
                    }
                }
                foreach ((CellFace cellFace, BlockValueAndCount pair) in data.ResultVeins) {
                    Vector3 position = cellFace.GetFaceCenter(0.1f);
                    if (!camera.ViewFrustum.Intersection(position)) {
                        continue;
                    }
                    Vector3 forward = CellFace.FaceToVector3(cellFace.Face);
                    Vector3 up = cellFace.Face < 4 ? Vector3.UnitY :
                        Math.Abs(viewDirection.X) > Math.Abs(viewDirection.Z) ?
                            new Vector3((viewDirection.X > 0 ? 1 : -1) * (viewDirection.Y < 0 ? 1 : -1), 0, 0) :
                            new Vector3(0, 0, (viewDirection.Z > 0 ? 1 : -1) * (viewDirection.Y < 0 ? 1 : -1));
                    Vector3 right = Vector3.Cross(forward, up);
                    m_fontBatch3D.QueueText(pair.Count.ToString("D"), position, right * -0.02f, up * -0.02f, Color.White, TextAnchor.Center);
                }
            }
            GLWrapper.GL.GetFloat(GetPName.LineWidth, out float lineWidth);
            GLWrapper.GL.LineWidth(4f);
            m_primitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
            GLWrapper.GL.LineWidth(lineWidth);
            m_lastDrawTime = time;
        }

        /// <summary>
        ///     扫描获取所有结果，并获取路径
        /// </summary>
        public void Scan(Point3 start, MineralPathfinderBlockData data) {
            if (data.MaxResultGroupCount < 1
                || data.ScanRange <= 0
                || (data.ContentsTargets.Count == 0 && data.ValueTargets.Count == 0 && !data.SleepSelected && !data.DeathSelected)) {
                return;
            }
            if (m_isScanning) {
                foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers) {
                    componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, "1"), Color.White, false, true);
                }
                return;
            }
            if (DateTime.Now - m_lastScanTime < TimeSpan.FromSeconds(1)) {
                foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers) {
                    componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, "2"), Color.White, false, true);
                }
                return;
            }
            m_isScanning = true;
            m_lastScanTime = DateTime.Now;
            data.ResetResults();
            ScanBlocks(
                start,
                data.ContentsTargets,
                data.ValueTargets,
                (cellFace, blockValue, count, onlyContents) => {
                    data.ResultVeins.Add(cellFace, new BlockValueAndCount(onlyContents ? Terrain.ExtractContents(blockValue) : blockValue, count));
                },
                data.MaxResultGroupCount,
                data.ScanRange
            );
            if (data.SleepSelected) {
                foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers) {
                    Point3 point3 = Terrain.ToCell(componentPlayer.PlayerData.SpawnPosition);
                    if (m_terrain.GetCellContents(point3) == 0
                        && m_terrain.GetCellContents(point3.X, point3.Y - 1, point3.Z) != 0) {
                        data.ResultVeins.Add(new CellFace(point3.X, point3.Y - 1, point3.Z, 4), new BlockValueAndCount(-1, 1));
                    }
                }
            }
            if (data.DeathSelected) {
                foreach (PlayerStats playerStats in m_subsystemPlayerStats.m_playerStats.Values) {
                    if (playerStats.DeathRecords.Count > 0) {
                        Point3 point3 = Terrain.ToCell(playerStats.DeathRecords.Last().Location);
                        if (m_terrain.GetCellContents(point3) == 0
                            && m_terrain.GetCellContents(point3.X, point3.Y - 1, point3.Z) != 0) {
                            data.ResultVeins.Add(new CellFace(point3.X, point3.Y - 1, point3.Z, 4), new BlockValueAndCount(-1, 1));
                        }
                    }
                }
            }
            if (data.ResultVeins.Count > 0) {
                if (data.ResultVeins.Count > data.MaxResultGroupCount) {
                    KeepNearest(data.ResultVeins, start, data.MaxResultGroupCount);
                }
                data.m_resultPath = FindPath(start, data.ResultVeins.Keys.ToArray());
                foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers) {
                    componentPlayer.ComponentGui.DisplaySmallMessage(
                        string.Format(LanguageControl.Get(fName, "3"), data.ResultVeins.Count),
                        Color.White,
                        false,
                        false
                    );
                }
            }
            else {
                foreach (ComponentPlayer componentPlayer in m_subsystemPlayers.ComponentPlayers) {
                    componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, "4"), Color.White, false, false);
                }
            }
            m_isScanning = false;
        }

        /// <summary>
        ///     在实体方块表面爬行，寻找并统计连续的目标方块数量并触发回调，但不破坏地形
        /// </summary>
        public void ScanBlocks(Point3 start,
            ISet<int> contentsTargets,
            ISet<int> valueTargets,
            Action<CellFace, int, int, bool> callback,
            int maxResultGroupCount = 1,
            float range = float.PositiveInfinity) {
            if (maxResultGroupCount < 1
                || range <= 0
                || (contentsTargets.Count == 0 && valueTargets.Count == 0)) {
                return;
            }
            HashSet<CellFace> visitedFaces = new(); // 记录访问过的表面（用于爬行逻辑）
            HashSet<Point3> scannedBlocks = new(); // 记录已经统计过的目标方块坐标（防止重复计数）
            Queue<CellFace> toVisiteQueue = new();
            int resultCount = 0;
            float rangeSquared = range * range;
            // 1. 初始化：找到 start 初始暴露的所有面
            for (int i = 0; i < 6; i++) {
                Point3 neighbor = start + CellFace.FaceToPoint3(i);
                if (IsValidAir(neighbor)) {
                    toVisiteQueue.Enqueue(new CellFace(start, i));
                }
            }
            while (toVisiteQueue.Count > 0) {
                CellFace current = toVisiteQueue.Dequeue();
                Point3 currentPoint3 = current.Point;
                // 检查方块是否有效
                int currentBlockValue = Terrain.ReplaceLight(m_terrain.GetCellValueFast(currentPoint3), 0);
                if (currentBlockValue == 0) {
                    continue; // 如果变成了空气（可能被其他逻辑修改），跳过
                }
                // 检查面是否爬过
                if (visitedFaces.Contains(current)) {
                    continue;
                }
                // 如果是目标方块，且从未被任何一次扫描统计过
                if (valueTargets.Contains(currentBlockValue)) {
                    if (!scannedBlocks.Contains(currentPoint3)) {
                        // 启动泛洪搜索，统计这一“脉”矿的数量，并标记所有相关方块为已扫描
                        int count = CountContinuousBlocks(currentPoint3, currentBlockValue, false, scannedBlocks);
                        // 回调：返回 值, 起始位置, 数量
                        callback(current, currentBlockValue, count, false);
                        if (++resultCount >= maxResultGroupCount) {
                            return;
                        }
                    }
                }
                else {
                    int currentBlockContents = Terrain.ExtractContents(currentBlockValue);
                    if (contentsTargets.Contains(currentBlockContents)
                        && !scannedBlocks.Contains(currentPoint3)) {
                        int count = CountContinuousBlocks(currentPoint3, currentBlockContents, true, scannedBlocks);
                        callback(current, currentBlockValue, count, true);
                        if (++resultCount >= maxResultGroupCount) {
                            return;
                        }
                    }
                }
                // 无论是目标方块还是普通方块，在 Scan 模式下它依然是实体，
                // 我们需要标记这个面为已访问，并继续在其表面爬行以寻找更多目标。
                visitedFaces.Add(current);
                foreach (CellFace neighbor in GetCrawlingNeighbors(current, rangeSquared)) {
                    toVisiteQueue.Enqueue(neighbor);
                }
            }
        }

        /// <summary>
        ///     计算连续目标方块数
        /// </summary>
        public int CountContinuousBlocks(Point3 start, int target, bool onlyContents, HashSet<Point3> scannedBlocks) {
            Queue<Point3> veinQueue = new();
            veinQueue.Enqueue(start);
            // 立即标记起始点
            scannedBlocks.Add(start);
            int count = 0;
            while (veinQueue.Count > 0) {
                Point3 current = veinQueue.Dequeue();
                count++;
                // 检查 6 个邻居
                for (int i = 0; i < 6; i++) {
                    Point3 neighbor = current + CellFace.FaceToPoint3(i);
                    // 基础检查
                    if (!m_terrain.IsCellValid(neighbor)) {
                        continue;
                    }
                    // 已经在全局扫描列表中，跳过（避免重复计数和死循环）
                    if (scannedBlocks.Contains(neighbor)) {
                        continue;
                    }
                    TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                    if (neighborChunk == null
                        || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                        continue;
                    }
                    int neighborBlockValue = Terrain.ReplaceLight(neighborChunk.GetCellValueFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15), 0);
                    if (onlyContents) {
                        neighborBlockValue = Terrain.ExtractContents(neighborBlockValue);
                    }
                    if (neighborBlockValue == target) {
                        scannedBlocks.Add(neighbor); // 入队前立即标记，防止重复入队
                        veinQueue.Enqueue(neighbor);
                    }
                    // 注意：这里不需要像 DigBlocks 那样处理 "非目标实体"，
                    // 因为我们不进行挖掘，不会暴露内部的新面，所以不需要关心内部的非目标方块。
                }
            }
            return count;
        }

        /// <summary>
        ///     获取周围可爬行到的邻面
        /// </summary>
        public List<CellFace> GetCrawlingNeighbors(CellFace current, float rangeSquared = float.PositiveInfinity) {
            List<CellFace> result = new(4);
            Point3 currentPoint3 = current.Point;
            Point3 direction = CellFace.FaceToPoint3(current.Face);
            foreach (int tangent in CellFace.FaceToTangents(current.Face)) {
                Point3 tangentPoint3 = CellFace.FaceToPoint3(tangent);
                Point3 neighbor = currentPoint3 + tangentPoint3;
                if (rangeSquared != float.PositiveInfinity
                    && Vector3.DistanceSquared(neighbor, currentPoint3) > rangeSquared) {
                    continue;
                }
                // 1. 凹角 (Concave) - 墙角拐上去
                Point3 diagonal = neighbor + direction;
                if (!m_terrain.IsCellValid(diagonal)) {
                    continue;
                }
                TerrainChunk diagonalChunk = m_terrain.GetChunkAtCell(diagonal.X, diagonal.Z);
                if (diagonalChunk == null
                    || diagonalChunk.ThreadState < TerrainChunkState.InvalidLight) {
                    continue;
                }
                int diagonalContent = diagonalChunk.GetCellContentsFast(diagonal.X & 15, diagonal.Y, diagonal.Z & 15);
                if (diagonalContent != 0) {
                    // 检查对角块是否是个有效的落脚点 (其实 IsValidNotAir 已经检查了一部分)
                    // 这里的面是 OpposideFace(tangent)
                    result.Add(new CellFace(diagonal, CellFace.OppositeFace(tangent)));
                    continue;
                }

                //实际不需要，因为对角无效时邻居肯定也无效
                // 检查邻居本身是否越界或未加载
                /*if (!m_terrain.IsCellValid(neighbor)) {
                    continue;
                }*/
                TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                if (neighborChunk == null
                    || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                    continue;
                }
                int neighborContent = neighborChunk.GetCellContentsFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15);
                result.Add(
                    neighborContent == 0
                        // 2. 凸角 (Convex) - 绕过棱角
                        // 邻居是空气，说明路断了，需要翻过棱。面变成当前块的 tangent 面
                        ? new CellFace(currentPoint3, tangent)
                        // 3. 平面 (Flat) - 平地走
                        // 邻居是方块，可以直接走过去。面保持 current.Face
                        : new CellFace(neighbor, current.Face)
                );
            }
            return result;
        }

        /// <summary>
        ///     寻找从起点出发，逐个抵达所有目的地的表面爬行路径。
        /// </summary>
        public List<CellFace> FindPath(Point3 start, CellFace[] destinations, int maxSteps = int.MaxValue) {
            if (destinations == null
                || destinations.Length == 0) {
                return new List<CellFace>();
            }

            // 1. 确定起点的有效初始面 (Start Nodes)
            List<CellFace> initialStartFaces = new();
            for (int i = 0; i < 6; i++) {
                Point3 neighbor = start + CellFace.FaceToPoint3(i);
                if (IsValidAir(neighbor)) {
                    initialStartFaces.Add(new CellFace(start, i));
                }
            }
            if (initialStartFaces.Count == 0) {
                return null;
            }

            // 2. 构建节点和曼哈顿距离缓存
            int count = destinations.Length;
            int nodeCount = count + 1; // 0=StartPoint3, 1..N=Destinations

            // 缓存曼哈顿距离
            int[,] distCache = new int[nodeCount, nodeCount];

            // 2.a 计算 起点 -> 所有目标 (StartPoint3 到目标 Point3)
            // 这里的距离是 StartPoint3 的最佳面到目标 CellFace 的 Point3 的曼哈顿距离
            for (int i = 0; i < count; i++) {
                int minManhattanDist = int.MaxValue;
                foreach (CellFace sFace in initialStartFaces) {
                    minManhattanDist = Math.Min(minManhattanDist, ManhattanDistance(sFace.Point, destinations[i].Point));
                }
                distCache[0, i + 1] = minManhattanDist;
            }

            // 2.b 计算 目标 -> 目标 (目标 CellFace 的 Point3 之间)
            for (int i = 0; i < count; i++) {
                for (int j = 0; j < count; j++) {
                    if (i == j) {
                        continue;
                    }
                    distCache[i + 1, j + 1] = ManhattanDistance(destinations[i].Point, destinations[j].Point);
                }
            }

            // 3. TSP 求解：寻找基于曼哈顿距离的最短访问顺序 (全排列)
            int[] indices = Enumerable.Range(0, count).ToArray(); // 目标索引 0..N-1
            int[] bestOrder = null;
            int minTotalDistance = int.MaxValue;
            CellFace bestStartFace = default; // 记录最佳路径对应的 StartFace
            foreach (int[] order in Permutations(indices)) {
                int currentManhattanDist = 0;

                // 3.a 确定起点到第一个目标的最佳起始面
                int firstDestIndex = order.First();
                int minInitialDist = int.MaxValue;
                CellFace tempBestStartFace = default;
                foreach (CellFace sFace in initialStartFaces) {
                    int dist = ManhattanDistance(sFace.Point, destinations[firstDestIndex].Point);
                    if (dist < minInitialDist) {
                        minInitialDist = dist;
                        tempBestStartFace = sFace;
                    }
                }
                currentManhattanDist += minInitialDist;

                // 3.b 计算后续目标之间的距离
                int currentNodeIndex = firstDestIndex + 1; // 对应 distCache 的索引 1..N
                foreach (int nextDestIndex in order.Skip(1)) {
                    int nextNodeIndex = nextDestIndex + 1;
                    currentManhattanDist += distCache[currentNodeIndex, nextNodeIndex];
                    currentNodeIndex = nextNodeIndex;
                }
                if (currentManhattanDist < minTotalDistance) {
                    minTotalDistance = currentManhattanDist;
                    bestOrder = order.ToArray();
                    bestStartFace = tempBestStartFace; // 存储与当前最佳路径对应的起点
                }
            }
            if (bestOrder == null) {
                return null; // 无法形成路径 (理论上曼哈顿距离总是非无穷大)
            }

            // 4. 逐段计算 A* 路径并拼接
            List<CellFace> finalPath = new();

            // 4.a 第一段：StartPoint3 的最佳面 -> 第一个目的地
            CellFace segmentStart = bestStartFace;
            CellFace segmentEnd = destinations[bestOrder[0]];
            List<CellFace> segmentPath = CalcAStarPath(segmentStart, segmentEnd, maxSteps);
            if (segmentPath == null) {
                return null;
            }
            finalPath.AddRange(segmentPath);

            // 4.b 后续段：目的地 i -> 目的地 i+1
            for (int i = 0; i < bestOrder.Length - 1; i++) {
                segmentStart = destinations[bestOrder[i]];
                segmentEnd = destinations[bestOrder[i + 1]];

                // 直接使用 CellFace 作为 A* 路径的起点
                segmentPath = CalcAStarPath(segmentStart, segmentEnd, maxSteps);
                if (segmentPath == null) {
                    return null;
                }

                // 拼接路径：跳过段的起点 (因为它已经在上一段的终点中)
                finalPath.AddRange(segmentPath.Skip(1));
            }
            return finalPath;
        }

        /// <summary>
        ///     核心 A* 算法
        /// </summary>
        List<CellFace> CalcAStarPath(CellFace start, CellFace end, int maxSearchSteps) {
            // 注意：这里将起点从 List<CellFace> 改为单个 CellFace，因为在确定顺序后，起点是确定的。
            // 唯一例外是最初的 start Point3，需要特殊处理。
            PriorityQueue<CellFace, int> openSet = new();
            Dictionary<CellFace, CellFace> cameFrom = new();
            Dictionary<CellFace, int> gScore = new() { // 初始化起点
                [start] = 0
            };
            int h = ManhattanDistance(start.Point, end.Point);
            openSet.Enqueue(start, h);
            cameFrom[start] = start; // 标记回溯终止点
            int steps = 0;
            while (openSet.Count > 0) {
                if (steps++ > maxSearchSteps) {
                    return null;
                }
                CellFace current = openSet.Dequeue();

                // 到达目标
                if (current.Equals(end)) {
                    return ReconstructPath(cameFrom, current, start);
                }
                int currentG = gScore[current];
                foreach (CellFace neighbor in GetCrawlingNeighbors(current)) {
                    // 假设每步权重为 1
                    int tentativeG = currentG + 1;
                    if (!gScore.TryGetValue(neighbor, out int existingG)
                        || tentativeG < existingG) {
                        gScore[neighbor] = tentativeG;
                        // 启发函数：曼哈顿距离
                        int f = tentativeG + ManhattanDistance(neighbor.Point, end.Point);
                        cameFrom[neighbor] = current;
                        openSet.Enqueue(neighbor, f);
                    }
                }
            }
            return null; // 未找到路径
        }

        /// <summary>
        ///     从终点回溯路径
        /// </summary>
        List<CellFace> ReconstructPath(Dictionary<CellFace, CellFace> cameFrom, CellFace current, CellFace start) {
            List<CellFace> totalPath = [current];
            while (cameFrom.TryGetValue(current, out CellFace previous)
                && !previous.Equals(start)) {
                // 如果 previous 是起始点，或者回溯到了自身标记（start），则终止
                current = previous;
                totalPath.Add(current);
            }
            // 补上起始点
            totalPath.Add(start);
            totalPath.Reverse();
            return totalPath;
        }

        /// <summary>
        ///     曼哈顿距离启发函数
        /// </summary>
        public static int ManhattanDistance(Point3 a, Point3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

        public static int ManhattanDistance(CellFace a, Point3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

        /// <summary>
        ///     生成全排列的辅助方法
        /// </summary>
        public static IEnumerable<int[]> Permutations(int[] source) {
            bool[] used = new bool[source.Length];
            int[] current = new int[source.Length];

            IEnumerable<int[]> Backtrack(int depth) {
                if (depth == source.Length) {
                    // 必须拷贝，否则后续修改会影响已返回的结果
                    yield return (int[])current.Clone();
                    yield break;
                }
                for (int i = 0; i < source.Length; i++) {
                    if (used[i]) {
                        continue;
                    }
                    used[i] = true;
                    current[depth] = source[i];
                    foreach (int[] perm in Backtrack(depth + 1)) {
                        yield return perm;
                    }
                    used[i] = false;
                }
            }

            return Backtrack(0);
        }

        /// <summary>
        ///     是否是有效的实体方块
        /// </summary>
        bool IsValidNotAir(Point3 p) {
            if (!m_terrain.IsCellValid(p)) {
                return false;
            }
            TerrainChunk chunk = m_terrain.GetChunkAtCell(p.X, p.Z);
            if (chunk == null
                || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                return false;
            }
            return chunk.GetCellContentsFast(p.X & 15, p.Y, p.Z & 15) != 0;
        }

        /// <summary>
        ///     是否是有效的空气方块
        /// </summary>
        bool IsValidAir(Point3 p) {
            if (!m_terrain.IsCellValid(p)) {
                return false;
            }
            TerrainChunk chunk = m_terrain.GetChunkAtCell(p.X, p.Z);
            if (chunk == null
                || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                return false;
            }
            return chunk.GetCellContentsFast(p.X & 15, p.Y, p.Z & 15) == 0;
        }

        public void InitializePlaceableBlocks() {
            for (int blockContents = 0; blockContents < 1024; blockContents++) {
                Block block = BlocksManager.Blocks[blockContents];
                if (block is not AirBlock
                    && block.IsPlaceable_(blockContents)) {
                    PlaceableBlockContents.Add(blockContents);
                }
            }
            PlaceableBlockContents.Remove(FurnitureBlock.Index);
            PlaceableBlockContents.Remove(PistonHeadBlock.Index);
            PlaceableBlockContents.Add(BedrockBlock.Index);
            PlaceableBlockContents.Add(WaterBlock.Index);
            PlaceableBlockContents.Add(MagmaBlock.Index);
        }

        public HashSet<int> BlockContentsToValues(HashSet<int> blockContents) {
            HashSet<int> result = [];
            foreach (int contents in blockContents) {
                Block block = BlocksManager.Blocks[contents];
                switch (block) {
                    case FluidBlock fluidBlock:
                        for (int i = 0; i < fluidBlock.MaxLevel; i++) {
                            result.Add(Terrain.MakeBlockValue(contents, 0, FluidBlock.SetLevel(0, i)));
                            result.Add(Terrain.MakeBlockValue(contents, 0, FluidBlock.SetLevel(FluidBlock.SetIsTop(0, true), i)));
                        }
                        break;
                    case StairsBlock:
                        for (int color = -1; color < 16; color++) {
                            for (int variant = 0; variant < 0x1F; variant++) {
                                result.Add(Terrain.MakeBlockValue(contents, color, StairsBlock.SetColor(variant, color == -1 ? null : color)));
                            }
                        }
                        break;
                    case SlabBlock:
                        for (int color = -1; color < 16; color++) {
                            for (int variant = 0; variant < 1; variant++) {
                                result.Add(Terrain.MakeBlockValue(contents, color, SlabBlock.SetColor(variant, color == -1 ? null : color)));
                            }
                        }
                        break;
                    case RotateableMountedElectricElementBlock:
                        for (int face = 0; face < 6; face++) {
                            for (int rotation = 0; rotation < 4; rotation++) {
                                result.Add(
                                    Terrain.MakeBlockValue(contents, face, RotateableMountedElectricElementBlock.SetRotation(face << 2, rotation))
                                );
                            }
                        }
                        break;
                    default:
                        foreach (int value in block.GetCreativeValues()) {
                            if (block.IsPlaceable_(contents)) {
                                result.Add(contents);
                            }
                        }
                        break;
                }
            }
            return result;
        }

        public override void Dispose() {
            base.Dispose();
            PlaceableBlockContents.Clear();
            foreach (MineralPathfinderBlockData data in m_itemsData.Values) {
                data.Dispose();
            }
            m_itemsData.Clear();
            foreach (MineralPathfinderBlockData data in m_blocksData.Values) {
                data.Dispose();
            }
            m_blocksData.Clear();
            foreach (MineralPathfinderBlockData data in m_movingBlocksData.Values) {
                data.Dispose();
            }
            m_movingBlocksData.Clear();
        }

        public static void KeepNearest<T>(Dictionary<CellFace, T> dic, Point3 center, int n) {
            if (n <= 0) {
                dic.Clear();
                return;
            }
            int m = dic.Count;
            if (n >= m) {
                return;
            }
            int k = m - n;
            PriorityQueue<CellFace, int> pq = new();
            foreach (CellFace key in dic.Keys) {
                int dist = ManhattanDistance(key, center);
                if (pq.Count < k) {
                    pq.Enqueue(key, dist);
                }
                else {
                    pq.TryPeek(out _, out int bestDist);
                    if (dist > bestDist) {
                        pq.Dequeue();
                        pq.Enqueue(key, dist);
                    }
                }
            }
            while (pq.Count > 0) {
                dic.Remove(pq.Dequeue());
            }
        }
    }
}