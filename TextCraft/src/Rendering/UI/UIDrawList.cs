using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using TextCraft.src.UI;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// 一个"绘制单元"：一段连续的顶点（通常是 1 个四边形，九宫格是 9 个）。
    /// 记录层级与提交序号，用于合批时的稳定排序。
    /// </summary>
    public struct UIPrimitive
    {
        public int Start;
        public int Count;
        public int Layer;
        public int Sequence;
        public UIBatchKey Key;
        public UIRect Bounds;
    }

    /// <summary>
    /// 合批结果：一次 <c>glDrawArrays</c> 对应的顶点区间与状态。
    /// </summary>
    public struct UIBatch
    {
        public UIBatchKey Key;
        public int Layer;
        public int Start;
        public int Count;
        public int PrimStart;
        public int PrimCount;
        public UIRect Bounds;

        public override string ToString()
            => $"layer={Layer} tex={Key.TextureId} mat={Key.Material} verts={Count} prims={PrimCount}";
    }

    /// <summary>
    /// UI 合批渲染统计，用于 F3 调试面板与自动化校验。
    /// </summary>
    public struct UIRenderStats
    {
        public int Components;
        public int Primitives;
        public int Vertices;
        public int Batches;
        public int DrawCalls;
        public int Layers;
        public bool LegacyMode;

        public override string ToString()
            => $"components={Components} prims={Primitives} verts={Vertices} batches={Batches} drawCalls={DrawCalls} layers={Layers}{(LegacyMode ? " (legacy)" : "")}";
    }

    /// <summary>
    /// UI 绘制列表：先把所有控件的几何写进同一个暂存顶点数组，再按
    /// (UI 层级 → 提交顺序) 排序并按 (纹理, 采样方式, 裁剪矩形) 合并成批次。
    /// 合并后的所有顶点会一次性上传到一个 VBO，每个批次只需一次 glDrawArrays。
    /// </summary>
    public sealed class UIDrawList
    {
        private UIVertex[] _stage = Array.Empty<UIVertex>();
        private int _stageCount;

        private UIPrimitive[] _prims = Array.Empty<UIPrimitive>();
        private int _primCount;

        private UIVertex[] _final = Array.Empty<UIVertex>();
        private int _finalCount;

        private UIBatch[] _batches = Array.Empty<UIBatch>();
        private int _batchCount;

        private UIPrimitive[] _moveBuffer = Array.Empty<UIPrimitive>();

        private int _sequence;

        /// <summary>是否允许把"中间没有重叠内容"的批次跨控件合并（默认开启）。</summary>
        public bool CoalesceBatches { get; set; } = true;

        /// <summary>跨批合并时最多向前回看的批次数。</summary>
        public int CoalesceWindow { get; set; } = 64;

        /// <summary>绘制顺序是否被拆成"每个绘制单元一个 DrawCall"（旧行为，便于对比）。</summary>
        public bool SplitAll { get; set; }

        public ReadOnlySpan<UIVertex> Vertices => _final.AsSpan(0, _finalCount);
        public int VertexCount => _finalCount;

        /// <summary>合并后的顶点数组（供 <see cref="UIMeshBuffer"/> 一次性上传）。</summary>
        public UIVertex[] FinalArray => _final;

        public ReadOnlySpan<UIBatch> Batches => _batches.AsSpan(0, _batchCount);
        public int BatchCount => _batchCount;

        /// <summary>本帧参与采集的控件数量（统计用）。</summary>
        public int ComponentsVisited { get; private set; }

        /// <summary>由 <see cref="UIComponent.CollectDraw"/> 调用，统计本帧参与采集的控件数。</summary>
        public void CountComponent() => ComponentsVisited++;

        public void Begin(int expectedVertices = 0)
        {
            _stageCount = 0;
            _finalCount = 0;
            _primCount = 0;
            _batchCount = 0;
            _sequence = 0;
            ComponentsVisited = 0;
            if (expectedVertices > 0)
            {
                EnsureStageCapacity(expectedVertices);
                EnsureFinalCapacity(expectedVertices);
            }
        }

        /// <summary>加入一个四边形。UV 与颜色都按"左上、右上、右下、左下"顺序填充。</summary>
        public void AddQuad(int layer, in UIBatchKey key, in UIRect rect, Vector2 uvMin, Vector2 uvMax, in Vector4 color)
        {
            if (rect.IsEmpty) return;

            var min = rect.Min;
            var max = rect.Max;
            float u0 = uvMin.X, v0 = uvMin.Y, u1 = uvMax.X, v1 = uvMax.Y;

            EnsureStageCapacity(_stageCount + 6);
            var v = _stage;
            int i = _stageCount;
            v[i + 0] = new UIVertex(new Vector2(min.X, min.Y), new Vector2(u0, v0), color);
            v[i + 1] = new UIVertex(new Vector2(max.X, min.Y), new Vector2(u1, v0), color);
            v[i + 2] = new UIVertex(new Vector2(max.X, max.Y), new Vector2(u1, v1), color);
            v[i + 3] = new UIVertex(new Vector2(min.X, min.Y), new Vector2(u0, v0), color);
            v[i + 4] = new UIVertex(new Vector2(max.X, max.Y), new Vector2(u1, v1), color);
            v[i + 5] = new UIVertex(new Vector2(min.X, max.Y), new Vector2(u0, v1), color);
            _stageCount = i + 6;

            AddPrimitive(layer, key, rect, i, 6);
        }

        /// <summary>
        /// 九宫格（9-slice）：四角不拉伸，四边单向拉伸，中心双向拉伸。
        /// 用于按钮/面板/进度条等需要任意尺寸的控件。
        /// </summary>
        public void AddNineSlice(int layer, in UIBatchKey key, in UIRect rect, in UIRect uv,
            int sourceWidth, int sourceHeight, int borderLeft, int borderTop, int borderRight, int borderBottom,
            in Vector4 color)
        {
            if (rect.IsEmpty) return;
            if (borderLeft + borderTop + borderRight + borderBottom <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
            {
                AddQuad(layer, key, rect, uv.Min, uv.Max, color);
                return;
            }

            float left = MathF.Max(0, borderLeft);
            float top = MathF.Max(0, borderTop);
            float right = MathF.Max(0, borderRight);
            float bottom = MathF.Max(0, borderBottom);

            // 目标区域比两边切片还窄时按比例压缩，避免出现负宽度
            float sx = 1f, sy = 1f;
            if (left + right > rect.Width) sx = rect.Width / (left + right);
            if (top + bottom > rect.Height) sy = rect.Height / (top + bottom);
            left *= sx; right *= sx;
            top *= sy; bottom *= sy;

            // 源图对应的 UV 切片比例
            float lu = uv.Width * (borderLeft / (float)sourceWidth);
            float ru = uv.Width * (borderRight / (float)sourceWidth);
            float tu = uv.Height * (borderTop / (float)sourceHeight);
            float bu = uv.Height * (borderBottom / (float)sourceHeight);

            Span<float> xs = stackalloc float[4] { rect.Left, rect.Left + left, rect.Right - right, rect.Right };
            Span<float> ys = stackalloc float[4] { rect.Top, rect.Top + top, rect.Bottom - bottom, rect.Bottom };
            Span<float> us = stackalloc float[4] { uv.Left, uv.Left + lu, uv.Right - ru, uv.Right };
            Span<float> vs = stackalloc float[4] { uv.Top, uv.Top + tu, uv.Bottom - bu, uv.Bottom };

            for (int row = 0; row < 3; row++)
            {
                if (ys[row + 1] - ys[row] <= 0f) continue;
                for (int col = 0; col < 3; col++)
                {
                    if (xs[col + 1] - xs[col] <= 0f) continue;
                    var piece = UIRect.FromMinMax(new Vector2(xs[col], ys[row]), new Vector2(xs[col + 1], ys[row + 1]));
                    var uvPiece = UIRect.FromMinMax(new Vector2(us[col], vs[row]), new Vector2(us[col + 1], vs[row + 1]));
                    AddQuad(layer, key, piece, uvPiece.Min, uvPiece.Max, color);
                }
            }
        }

        private void AddPrimitive(int layer, in UIBatchKey key, in UIRect rect, int start, int count)
        {
            EnsurePrimCapacity(_primCount + 1);
            var bounds = key.Clip.IsInfinite ? rect : UIRect.Intersect(rect, key.Clip);
            if (bounds.IsEmpty) bounds = rect;
            _prims[_primCount] = new UIPrimitive
            {
                Start = start,
                Count = count,
                Layer = layer,
                Sequence = _sequence++,
                Key = key,
                Bounds = bounds,
            };
            _primCount++;
        }

        /// <summary>
        /// 排序 + 合批。返回本次的统计信息（层级数、批次数等）。
        /// </summary>
        public UIRenderStats End()
        {
            if (_primCount == 0)
            {
                return new UIRenderStats { LegacyMode = SplitAll, Components = ComponentsVisited };
            }

            Array.Sort(_prims, 0, _primCount, PrimitiveComparer.Instance);

            BuildBatches();

            if (!SplitAll && CoalesceBatches && _batchCount > 1)
                CoalesceBatchesPass();

            Flatten();

            int layers = 0;
            int lastLayer = int.MinValue;
            for (int i = 0; i < _batchCount; i++)
            {
                if (_batches[i].Layer == lastLayer) continue;
                lastLayer = _batches[i].Layer;
                layers++;
            }

            return new UIRenderStats
            {
                Components = ComponentsVisited,
                Primitives = _primCount,
                Vertices = _finalCount,
                Batches = _batchCount,
                DrawCalls = _batchCount,
                Layers = layers,
                LegacyMode = SplitAll,
            };
        }

        private void BuildBatches()
        {
            _batchCount = 0;
            for (int i = 0; i < _primCount; i++)
            {
                ref var p = ref _prims[i];
                if (!SplitAll && _batchCount > 0)
                {
                    ref var last = ref _batches[_batchCount - 1];
                    if (last.Key == p.Key && last.Layer == p.Layer)
                    {
                        last.PrimCount++;
                        last.Bounds = last.Bounds.ExpandToInclude(p.Bounds);
                        continue;
                    }
                }

                EnsureBatchCapacity(_batchCount + 1);
                _batches[_batchCount] = new UIBatch
                {
                    Key = p.Key,
                    Layer = p.Layer,
                    PrimStart = i,
                    PrimCount = 1,
                    Bounds = p.Bounds,
                };
                _batchCount++;
            }
            RecomputePrimRanges();
        }

        /// <summary>
        /// 合并"状态相同、中间没有重叠绘制内容"的批次。
        /// 判定条件：对批次 i 与更早的同键批次 j，区间 (j, i) 内所有批次的屏幕包围盒
        /// 都必须与批次 i 的包围盒不相交——此时把 i 的几何前移到 j 之后不会改变任何像素结果。
        /// 合并方式是就地移动顶点区间，因此合并后批次仍然是一段连续顶点，不需要额外的索引列表。
        /// </summary>
        private void CoalesceBatchesPass()
        {
            int window = Math.Max(1, CoalesceWindow);
            int i = 1;
            int guard = 0;
            while (i < _batchCount && guard++ < 4096)
            {
                bool merged = false;
                int low = Math.Max(0, i - window);
                for (int j = i - 1; j >= low; j--)
                {
                    if (_batches[j].Key != _batches[i].Key) continue;
                    if (_batches[j].Layer != _batches[i].Layer) continue;

                    bool disjoint = true;
                    for (int k = j + 1; k < i; k++)
                    {
                        if (_batches[k].Bounds.Intersects(_batches[i].Bounds))
                        {
                            disjoint = false;
                            break;
                        }
                    }
                    if (!disjoint) continue;

                    MergeBatch(j, i);
                    merged = true;
                    break;
                }
                if (!merged) i++;
            }
        }

        private void MergeBatch(int dst, int src)
        {
            int dstEnd = _batches[dst].PrimStart + _batches[dst].PrimCount;
            int srcStart = _batches[src].PrimStart;
            int srcCount = _batches[src].PrimCount;

            if (srcStart != dstEnd)
            {
                if (_moveBuffer.Length < srcCount)
                    _moveBuffer = new UIPrimitive[Math.Max(srcCount, 32)];
                Array.Copy(_prims, srcStart, _moveBuffer, 0, srcCount);
                Array.Copy(_prims, dstEnd, _prims, dstEnd + srcCount, srcStart - dstEnd);
                Array.Copy(_moveBuffer, 0, _prims, dstEnd, srcCount);
            }

            _batches[dst].PrimCount += srcCount;
            _batches[dst].Bounds = _batches[dst].Bounds.ExpandToInclude(_batches[src].Bounds);

            if (src + 1 < _batchCount)
                Array.Copy(_batches, src + 1, _batches, src, _batchCount - src - 1);
            _batchCount--;

            RecomputePrimRanges();
        }

        private void RecomputePrimRanges()
        {
            int cursor = 0;
            for (int i = 0; i < _batchCount; i++)
            {
                _batches[i].PrimStart = cursor;
                cursor += _batches[i].PrimCount;
            }
        }

        private void Flatten()
        {
            EnsureFinalCapacity(_stageCount);
            int v = 0;
            for (int b = 0; b < _batchCount; b++)
            {
                ref var batch = ref _batches[b];
                batch.Start = v;
                int primEnd = batch.PrimStart + batch.PrimCount;
                for (int p = batch.PrimStart; p < primEnd; p++)
                {
                    ref var prim = ref _prims[p];
                    Array.Copy(_stage, prim.Start, _final, v, prim.Count);
                    v += prim.Count;
                }
                batch.Count = v - batch.Start;
            }
            _finalCount = v;
        }

        private void EnsureStageCapacity(int needed)
        {
            if (_stage.Length >= needed) return;
            int size = Math.Max(needed, Math.Max(256, _stage.Length * 2));
            Array.Resize(ref _stage, size);
        }

        private void EnsureFinalCapacity(int needed)
        {
            if (_final.Length >= needed) return;
            int size = Math.Max(needed, Math.Max(256, _final.Length * 2));
            Array.Resize(ref _final, size);
        }

        private void EnsurePrimCapacity(int needed)
        {
            if (_prims.Length >= needed) return;
            int size = Math.Max(needed, Math.Max(64, _prims.Length * 2));
            Array.Resize(ref _prims, size);
        }

        private void EnsureBatchCapacity(int needed)
        {
            if (_batches.Length >= needed) return;
            int size = Math.Max(needed, Math.Max(16, _batches.Length * 2));
            Array.Resize(ref _batches, size);
        }

        private sealed class PrimitiveComparer : IComparer<UIPrimitive>
        {
            public static readonly PrimitiveComparer Instance = new();

            public int Compare(UIPrimitive x, UIPrimitive y)
            {
                int byLayer = x.Layer.CompareTo(y.Layer);
                return byLayer != 0 ? byLayer : x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
