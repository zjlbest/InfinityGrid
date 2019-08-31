using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MyUIComponent
{
    [Serializable]
    public enum AxisType
    {
        Horizontal,
        Vertical,
    }

    public class MyUIGrid : MonoBehaviour
    {
        #region 字段

        //默认加载路径
        private const string pbPrefix = "Assets/uiInfos/";

        /// <summary>
        /// 单项的大小
        /// </summary>
        public Vector2 cellSize = new Vector2(100, 100);

        /// <summary>
        /// 锚点位置
        /// </summary>
        public TextAnchor pivotType = TextAnchor.UpperLeft;

        /// <summary>
        /// 单项生成方向
        /// </summary>
        public AxisType axisType = AxisType.Horizontal;

        /// <summary>
        /// 固定几行
        /// </summary>
        public int rowLimit = 0;

        /// <summary>
        /// 固定几列
        /// </summary>
        public int columnLimit = 0;

        /// <summary>
        /// 单项对象池
        /// </summary>
        public List<MyUIGridItem> poolItems = new List<MyUIGridItem>();

        /// <summary>
        /// 目前显示的单项
        /// </summary>
        public Dictionary<int, MyUIGridItem> showItems = new Dictionary<int, MyUIGridItem>();

        /// <summary>
        /// 缓存所有索引的Rect(位置)，不用在做二次计算
        /// </summary>
        public Dictionary<int, Rect> cachedRects = new Dictionary<int, Rect>();

        /// <summary>
        /// 可见视图
        /// </summary>
        public RectTransform viewPortRect;

        /// <summary>
        /// 显示数据
        /// </summary>
        public List<object> dataList = null;

        /// <summary>
        /// 用来显示的预设名字
        /// </summary>
        private string pbName;

        /// <summary>
        /// 预制体资源
        /// </summary>
        private GameObject pbGo;

        /// <summary>
        /// 滚动视图
        /// </summary>
        private ScrollRect scrollRect;

        /// <summary>
        /// 原始的显示区域Rect
        /// </summary>
        private Rect originViewRect = Rect.zero;

        /// <summary>
        /// 起始位置
        /// </summary>
        private Vector3 originGridPos;

        /// <summary>
        /// 原先的RectTran
        /// </summary>
        private RectTransform originRectTran;

        private bool isInit = false;

        #endregion

        #region 生命周期函数

        private void Awake()
        {
            if (originRectTran == null)
            {
                originRectTran = GetComponent<RectTransform>();
            }

            if (viewPortRect == null)
            {
                Transform parent = transform.parent;
                while (parent != null && viewPortRect == null)
                {
                    if (parent.GetComponent<ScrollRect>() != null)
                    {
                        scrollRect = parent.GetComponent<ScrollRect>();
                        viewPortRect = scrollRect.viewport;
                    }
                    else
                    {
                        parent = parent.parent;
                    }
                }
            }
        }

        /// <summary>
        /// 记录这次距离上一次的位移
        /// </summary>
        private Vector2 offsetDelta = Vector2.zero;

        /// <summary>
        /// 记录现在位置距离初始位置的位移
        /// </summary>
        private Vector2 lastOffsetDelta = Vector2.zero;

        private void LateUpdate()
        {
            if (dataList == null || dataList.Count == 0)
            {
                return;
            }

            offsetDelta = transform.localPosition - originGridPos;
            if (offsetDelta != lastOffsetDelta)
            {
                Vector2 tmpVec2 = offsetDelta - lastOffsetDelta;
                int rows = 0, column = 0, offsetIndex = 0;

                if (axisType == AxisType.Horizontal)
                {
                    rows = Mathf.CeilToInt(Mathf.Abs(tmpVec2.y) / cellSize.y);
                    offsetIndex = rows * columnLimit;
                }
                else if (axisType == AxisType.Vertical)
                {
                    column = Mathf.CeilToInt(Mathf.Abs(tmpVec2.x) / cellSize.x);
                    offsetIndex = column * rowLimit;
                }

                int minIndex = 0, maxIndex = 0;
                if (showItems.Count > 0)
                {
                    List<int> list = new List<int>(showItems.Keys);
                    minIndex = list[0];
                    maxIndex = list[0];
                    for (int i = 1; i < list.Count; i++)
                    {
                        if (list[i] < minIndex)
                        {
                            minIndex = list[i];
                        }

                        if (list[i] > maxIndex)
                        {
                            maxIndex = list[i];
                        }
                    }
                }

                //优先处理正在显示的单项，
                //如果按照索引来处理的话，会导致缓存池中没有对象可用
                for (int index = minIndex; index < maxIndex; index++)
                {
                    ShowOrHideIndexData(1, index);
                }

                for (int index = minIndex - offsetIndex; index < minIndex; index++)
                {
                    if (index >= 0 && index < dataList.Count)
                    {
                        ShowOrHideIndexData(1, index);
                    }
                }

                for (int index = maxIndex + 1; index <= maxIndex + offsetIndex; index++)
                {
                    if (index >= 0 && index < dataList.Count)
                    {
                        ShowOrHideIndexData(1, index);
                    }
                }

                lastOffsetDelta = offsetDelta;
            }
        }

        #endregion

        public void SetPrefabName(string name)
        {
            pbName = name;
            pbGo = AssetDatabase.LoadAssetAtPath<GameObject>(pbPrefix + pbName + ".prefab");
        }

        /// <summary>
        /// 设置数据源
        /// </summary>
        /// <param name="datas"></param>
        public void SetData(List<object> datas)
        {
            /*
             * 1.根据数据来设置Grid的大小
             * 2.确定ScrollRect显示区域有多大
             * 3.确认对象池大小
             * 4.判断那些item要显示
             */
            if (datas == null || datas.Count == 0 || pbGo == null)
            {
                Debug.LogWarning("传入数据为空或者预设为空");
                ClearShowItems();
                return;
            }

            cachedRects.Clear();

            dataList = datas;

            UpdateGridSize(dataList.Count);
            
            if (!isInit)
            {
                isInit = true;
                originGridPos = transform.localPosition;
            }
            else
            {
                offsetDelta = Vector2.zero;
                lastOffsetDelta = Vector2.zero;
                transform.localPosition = originGridPos;
            }

            UpdateViewRect();

            SpawnPoolItems();

            UpdateShowItems();
        }

        /// <summary>
        /// 重新设置某些位置的数据
        /// </summary>
        /// <param name="newDatas">Key从0开始</param>
        public void RefreshSomeData(Dictionary<int, object> newDatas)
        {
            if (newDatas == null || newDatas.Count == 0)
            {
                Debug.LogWarning("MyUIGrid的ResetSomeData方法参数为空或者数量为0");
                return;
            }

            int dataSize = dataList.Count;
            foreach (int index in newDatas.Keys)
            {
                if (index >= 0 && index < dataSize)
                {
                    dataList[index] = newDatas[index];
                }

                if (showItems.ContainsKey(index))
                {
                    showItems[index].SetData(newDatas[index]);
                }
            }
        }

        /// <summary>
        /// 删除某些位置的数据
        /// </summary>
        /// <param name="indexs"></param>
        public void DeleteSomeData(List<int> indexs)
        {
            if (indexs == null || indexs.Count == 0)
            {
                Debug.LogWarning("MyUIGrid的DeleteSomeData方法参数为空或者数量为0");
                return;
            }

            List<object> myGridItems = new List<object>();

            int dataSize = dataList.Count;
            int index = 0;
            for (int i = 0; i < indexs.Count; i++)
            {
                index = indexs[i];
                if (index >= 0 && index < dataSize)
                {
                    myGridItems.Add(dataList[index]);
                }
            }

            for (int i = 0; i < myGridItems.Count; i++)
            {
                dataList.Remove(myGridItems[i]);
            }

            ClearShowItems();
            UpdateGridSize(dataList.Count);
            UpdateShowItems();
        }

        /// <summary>
        /// 添加一些数据
        /// </summary>
        /// <param name="datas"></param>
        public void AddSomeData(List<object> datas)
        {
            if (datas == null || datas.Count == 0)
            {
                Debug.LogWarning("MyUIGrid的AddSomeData方法参数为空或者数量为0");
                return;
            }

            dataList.AddRange(datas);
            UpdateGridSize(dataList.Count);
            SpawnPoolItems();
            UpdateShowItems();
        }

        /// <summary>
        /// 显示某一个索引的数据
        /// </summary>
        /// <param name="index"></param>
        public void ScrollTo(int index)
        {
            if (scrollRect == null)
            {
                return;
            }

            index++;
            if (index <= 0 || index >= dataList.Count)
            {
                Debug.LogWarning("MyUIGrid的ScrollTo方法参数小于0或者大于最大索引");
            }

            int maxRows = 0, maxColumns = 0, indexRow = 0, indexColumn = 0, dataSize = dataList.Count;

            if (axisType == AxisType.Horizontal)
            {
                maxColumns = Mathf.Min(columnLimit, dataSize);
                maxRows = dataSize / columnLimit;
                indexRow = index / columnLimit;
                indexColumn = index % maxColumns;
            }
            else if (axisType == AxisType.Vertical)
            {
                maxRows = Mathf.Min(rowLimit, dataSize);
                maxColumns = dataSize / rowLimit;
                indexRow = index % rowLimit;
                indexColumn = index / rowLimit;
            }

            Vector2 offset = Vector2.zero;

            if (scrollRect.vertical)
            {
                offset.y = 1 - (float) indexRow / maxRows;
            }

            if (scrollRect.horizontal)
            {
                offset.x = 1 - (float) indexColumn / maxColumns;
            }

            scrollRect.normalizedPosition = offset;
        }

        /// <summary>
        /// 刷新显示单项
        /// </summary>
        private void UpdateShowItems()
        {
            MyUIGridItem item = null;
            int count = dataList.Count;
            for (int index = 0; index < count; index++)
            {
                ShowOrHideIndexData(0, index);
                ShowOrHideIndexData(1, index);
            }
        }
        /// <summary>
        /// 显示还是隐藏index位置的单项
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="index"></param>
        private void ShowOrHideIndexData(int axis, int index)
        {
            RectTransform rectTran;
            if (showItems.ContainsKey(index))
            {
                rectTran = showItems[index].GetComponent<RectTransform>();
            }
            else
            {
                rectTran = poolItems[0].GetComponent<RectTransform>();
            }

            if (axis == 0)
            {
                rectTran.anchorMax = Vector2.up;
                rectTran.anchorMin = Vector2.up;
                rectTran.sizeDelta = cellSize;
            }
            else
            {
                Rect rect = Rect.zero;
                if (cachedRects.ContainsKey(index))
                {
                    rect = cachedRects[index];
                    rectTran.localPosition = rect.min + rectTran.pivot * rectTran.sizeDelta;
                }
                else
                {
                    int totalColumn = 0,
                        totalRow = 0,
                        tmpNum = 0,
                        realColumn = 0,
                        realRow = 0,
                        row = 0,
                        column = 0;
                    if (columnLimit > 0)
                    {
                        totalColumn = columnLimit;
                        totalRow = Mathf.CeilToInt(dataList.Count / (float) totalColumn);
                    }
                    else if (rowLimit > 0)
                    {
                        totalRow = rowLimit;
                        totalColumn = Mathf.CeilToInt(dataList.Count / (float) totalRow);
                    }

                    if (axisType == AxisType.Horizontal)
                    {
                        tmpNum = totalColumn;
                        realColumn = Mathf.Clamp(totalColumn, 1, dataList.Count);
                        realRow = Mathf.Clamp(totalRow, 1, Mathf.CeilToInt((float) dataList.Count / tmpNum));
                    }
                    else if (axisType == AxisType.Vertical)
                    {
                        tmpNum = totalRow;
                        realRow = Mathf.Clamp(totalRow, 1, dataList.Count);
                        realColumn = Mathf.Clamp(totalColumn, 1, Mathf.CeilToInt((float) dataList.Count / tmpNum));
                    }

                    Vector2 vector2_1 = new Vector2(realColumn * cellSize.x, realRow * cellSize.y);
                    Vector2 vector2_2 = new Vector2(GetStartOffset(0, vector2_1.x), GetStartOffset(1, vector2_1.y));

                    if (axisType == AxisType.Horizontal)
                    {
                        column = index % columnLimit;
                        row = index / columnLimit;
                    }
                    else if (axisType == AxisType.Vertical)
                    {
                        row = index % rowLimit;
                        column = index / rowLimit;
                    }

                    SetChildAlongAxis(rectTran, 0, vector2_2.x + cellSize[0] * column, cellSize[0]);
                    SetChildAlongAxis(rectTran, 1, vector2_2.y + cellSize[1] * row, cellSize[1]);
                    GetRectByRT(rectTran, ref rect);
                    //缓存位置，避免下次计算
                    cachedRects.Add(index, rect);
                }

                if (viewPortRect == null)
                {
                    SpawnItem(index);
                }
                else
                {
                    rect.position += offsetDelta;
                    bool isOverLaps = originViewRect.Overlaps(rect);
                    if (isOverLaps && !showItems.ContainsKey(index))
                    {
                        SpawnItem(index);
                    }
                    else if (!isOverLaps && showItems.ContainsKey(index))
                    {
                        UnSpawnItem(index);
                    }
                }
            }
        }

        /// <summary>
        /// 从显示列表回收单项
        /// </summary>
        /// <param name="index"></param>
        private void UnSpawnItem(int index)
        {
            MyUIGridItem gridItem;
            gridItem = showItems[index];
            showItems.Remove(index);
            poolItems.Add(gridItem);
            gridItem.OnUnSpawn();
        }

        /// <summary>
        /// 从缓存池取单项
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void SpawnItem(int index)
        {
            MyUIGridItem gridItem;
            gridItem = poolItems[0];
            poolItems.RemoveAt(0);
            showItems.Add(index, gridItem);
            gridItem.index = index;
            gridItem.SetData(dataList[index]);
            gridItem.OnSpawn();
        }


        /// <summary>
        /// 根据RectTransform来获得他在父节点坐标系下的Rect
        /// </summary>
        /// <param name="rectTran"></param>
        /// <param name="rect"></param>
        private void GetRectByRT(RectTransform rectTran, ref Rect rect)
        {
            rect.min = (Vector2) rectTran.localPosition - rectTran.pivot * rectTran.sizeDelta;
            rect.size = rectTran.sizeDelta;
        }

        /// <summary>
        /// 设置单项的位置
        /// 该函数是LayoutGroup里的SetChildAlongAxis函数照搬过来的
        /// 设置UGUI里面的RectTransform可以通过SetInsetAndSizeFromParentEdge来避免锚点的影响
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="axis"></param>
        /// <param name="pos"></param>
        /// <param name="size"></param>
        protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
        {
            if ((UnityEngine.Object) rect == (UnityEngine.Object) null)
                return;
            rect.SetInsetAndSizeFromParentEdge(axis != 0 ? RectTransform.Edge.Top : RectTransform.Edge.Left, pos, size);
        }

        /// <summary>
        /// 得到单项的起始偏移
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="requiredSpaceWithoutPadding"></param>
        /// <returns></returns>
        private float GetStartOffset(int axis, float requiredSpaceWithoutPadding)
        {
            float num1 = requiredSpaceWithoutPadding;
            float num2 = originRectTran.rect.size[axis] - num1;
            float alignmentOnAxis = GetAlignmentOnAxis(axis);
            return num2 * alignmentOnAxis;
        }

        /// <summary>
        /// 得到对齐方式
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        protected float GetAlignmentOnAxis(int axis)
        {
            if (axis == 0)
                return ((int) this.pivotType % 3) * 0.5f;
            return ((int) this.pivotType / 3) * 0.5f;
        }

        /// <summary>
        /// 创建单项缓存池
        /// </summary>
        private void SpawnPoolItems()
        {
            //初始化池大小
            int poolCount = 0;
            //显示区域大小
            Vector2 showSize = Vector2.zero;
            int maxShowColumn = 0;
            int maxShowRow = 0;

            if (originViewRect != Rect.zero)
            {
                showSize = originViewRect.size;
                maxShowColumn = Mathf.FloorToInt(showSize.x / cellSize.x);
                maxShowColumn += Mathf.Abs(showSize.x % cellSize.x) < float.Epsilon ? 0 : 1;
                maxShowRow = Mathf.FloorToInt(showSize.y / cellSize.y);
                maxShowRow += Mathf.Abs(showSize.y % cellSize.y) < float.Epsilon ? 0 : 1;
                poolCount = maxShowColumn * maxShowRow + 2 * (columnLimit + rowLimit + 1);
                poolCount = Mathf.Min(poolCount, dataList.Count);
            }
            else
            {
                poolCount = dataList.Count;
            }

            ClearShowItems();

            GameObject tmpGo = null;
            MyUIGridItem gridItem = null;
            int initPoolCount = poolItems.Count;
            //如果现在对象池不够那么再实例化
            for (int i = poolCount; i > initPoolCount; i--)
            {
                tmpGo = Instantiate(pbGo, transform);
                gridItem = tmpGo.GetComponent<MyUIGridItem>();
                if (gridItem == null)
                {
                    gridItem = tmpGo.AddComponent<MyUIGridItem>();
                }

                gridItem.OnUnSpawn();
                poolItems.Add(tmpGo.GetComponent<MyUIGridItem>());
            }
        }

        /// <summary>
        /// 更新显示区域Rect
        /// </summary>
        private void UpdateViewRect()
        {
            /*
             * WorldCorners
             *     1 -------- 2
             *     |          |
             *     |          |
             *     0 -------- 3
             */
            if (viewPortRect != null)
            {
                Vector3[] worldCorners = new Vector3[4];
                viewPortRect.GetWorldCorners(worldCorners);
                Vector3 lowerLeftCorner = transform.InverseTransformPoint(worldCorners[0]);
                Vector3 topRightCorner = transform.InverseTransformPoint(worldCorners[2]);
                originViewRect = new Rect((Vector2) lowerLeftCorner, topRightCorner - lowerLeftCorner);
            }
        }

        /// <summary>
        /// 更新Grid的宽和高
        /// </summary>
        /// <param name="dataSize"></param>
        private void UpdateGridSize(int dataSize)
        {
            float widthSize = 0;
            float heightSize = 0;
            Vector2 size = Vector2.zero;

            if (axisType == AxisType.Horizontal)
            {
                size.x = Mathf.Min(dataSize, columnLimit) * cellSize.x;
                //默认有一行
                size.y = (dataSize / columnLimit + (dataSize % columnLimit == 0 ? 0 : 1)) * cellSize.y;
            }
            else if (axisType == AxisType.Vertical)
            {
                //默认有一列
                size.x = (dataSize / rowLimit + (dataSize % rowLimit == 0 ? 0 : 1)) * cellSize.x;
                size.y = Mathf.Min(dataSize, rowLimit) * cellSize.y;
            }

            Vector2 anchor = new Vector2(GetAlignmentOnAxis(0), 1 - GetAlignmentOnAxis(1));
            originRectTran.pivot = anchor;
            originRectTran.anchorMin = anchor;
            originRectTran.anchorMax = anchor;
            originRectTran.sizeDelta = size;
        }

        /// <summary>
        /// 把现在所有显示的单项移到对象池
        /// </summary>
        private void ClearShowItems()
        {
            foreach (KeyValuePair<int, MyUIGridItem> keyValuePair in showItems)
            {
                keyValuePair.Value.OnUnSpawn();
                poolItems.Add(keyValuePair.Value);
            }

            showItems.Clear();
        }

        /////////////////////////////编辑器下使用的功能///////////////////////////////

        private List<RectTransform> rectChildren = new List<RectTransform>();
        /// <summary>
        /// 组件属性发生变化时回调
        /// </summary>
        private void OnValidate()
        {
            Reposition();
        }
        /// <summary>
        /// 重新设置位置
        /// </summary>
        [ContextMenu("Reposition")]
        private void Reposition()
        {
            if (Application.isPlaying || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (originRectTran == null)
            {
                originRectTran = GetComponent<RectTransform>();
            }

            UpdateChilds();
            UpdateGridSize(rectChildren.Count);
            SetCellsAlongAxis(0);
            SetCellsAlongAxis(1);
        }
        /// <summary>
        /// 获取所有子节点的RectTransform组件
        /// </summary>
        private void UpdateChilds()
        {
            rectChildren.Clear();
            for (int index = 0; index < originRectTran.childCount; index++)
            {
                RectTransform rectTran = originRectTran.GetChild(index) as RectTransform;
                if (rectTran != null && rectTran.gameObject.activeInHierarchy)
                {
                    rectChildren.Add(rectTran);
                }
            }
        }
        /// <summary>
        /// 该方法是从GridLayoutGroup类中照搬过来的
        /// 只做了小部分修改
        /// </summary>
        /// <param name="axis"></param>
        private void SetCellsAlongAxis(int axis)
        {
            int childCount = rectChildren.Count;
            if (axis == 0)
            {
                for (int index = 0; index < this.rectChildren.Count; ++index)
                {
                    RectTransform rectChild = this.rectChildren[index];
                    rectChild.anchorMin = Vector2.up;
                    rectChild.anchorMax = Vector2.up;
                    rectChild.sizeDelta = this.cellSize;
                }
            }
            else
            {
                int totalColumn = 0,
                    totalRow = 0,
                    tmpNum = 0,
                    realColumn = 0,
                    realRow = 0,
                    row = 0,
                    column = 0;
                if (columnLimit > 0)
                {
                    totalColumn = columnLimit;
                    totalRow = Mathf.CeilToInt(childCount / (float) totalColumn);
                }
                else if (rowLimit > 0)
                {
                    totalRow = rowLimit;
                    totalColumn = Mathf.CeilToInt(childCount / (float) totalRow);
                }

                if (axisType == AxisType.Horizontal)
                {
                    tmpNum = totalColumn;
                    realColumn = Mathf.Clamp(totalColumn, 1, childCount);
                    realRow = Mathf.Clamp(totalRow, 1, Mathf.CeilToInt((float) childCount / tmpNum));
                }
                else if (axisType == AxisType.Vertical)
                {
                    tmpNum = totalRow;
                    realRow = Mathf.Clamp(totalRow, 1, childCount);
                    realColumn = Mathf.Clamp(totalColumn, 1, Mathf.CeilToInt((float) childCount / tmpNum));
                }

                Vector2 vector2_1 = new Vector2(realColumn * cellSize.x, realRow * cellSize.y);
                Vector2 vector2_2 = new Vector2(GetStartOffset(0, vector2_1.x), GetStartOffset(1, vector2_1.y));


                for (int index = 0; index < this.rectChildren.Count; ++index)
                {
                    if (axisType == AxisType.Horizontal)
                    {
                        column = index % columnLimit;
                        row = index / columnLimit;
                    }
                    else if (axisType == AxisType.Vertical)
                    {
                        row = index % rowLimit;
                        column = index / rowLimit;
                    }

                    SetChildAlongAxis(rectChildren[index], 0, vector2_2.x + cellSize[0] * column, cellSize[0]);
                    SetChildAlongAxis(rectChildren[index], 1, vector2_2.y + cellSize[1] * row, cellSize[1]);
                }
            }
        }
    }
}