using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// 对UGUI中的Text功能进行了拓展                  <br></br>                  
/// 添加了图文混排,带有下划线的超链接功能         <br></br>
/// </summary>
[AddComponentMenu("UI/ImgText")]
public class ImgText : Text, IPointerClickHandler
{
    /// <summary>
    /// 表示超链接信息
    /// </summary>
    public class LinkInfo
    {
        //表示顶点数组中,超链接开始的位置
        public int startIndex;

        //表示顶点数组中,超链接结束的位置
        public int endIndex;

        public int oldStartIndex;

        public int oldEndIndex;

        public int trueStartIndex;

        public int trueEndIndex;

        //表示超链接点击的范围
        public readonly List<Rect> boxes = new List<Rect>();

        //表示点击超链接后触发的委托
        public Action<string> onClick;

        //传递给委托的参数
        public string param;
    }

    /// <summary>
    /// 表示表情图片信息
    /// </summary>
    public class ImageInfo
    {
        //表示顶点数组中,超链接开始的位置
        public int startIndex;

        //表示顶点数组中,超链接结束的位置
        public int endIndex;

        public int oldStartIndex;

        public int oldEndIndex;

        public int trueStartIndex;

        public int trueEndIndex;

        //表示图片加载的路径
        public string path;

        //表示图片Scale.x的值
        public float width;

        //表示图片Scale.y的值
        public float height;

        //表示图片显示的位置
        public Vector3 pos;
    }


    #region 序列化字段
    //下划线宽度
    [SerializeField] public float underLineWidth = 1;

    //下划线颜色
    [SerializeField] private Color underLineColor = Color.blue;

    //输入的text
    [SerializeField] [TextArea(3, 10)] private string originalText = "";
    #endregion

    #region 正则表达式
    private static readonly Regex linkRegex = new Regex(@"<url param=(.*?)>(.*?)(</url>)", RegexOptions.Singleline);     //超链接
    private static readonly Regex colorRegex = new Regex(@"(<color=.*?>)(.*?)(</color>)", RegexOptions.IgnoreCase);      //颜色
    private static readonly Regex bRegex = new Regex(@"(<b>)(.*?)(</b>)", RegexOptions.IgnoreCase);                      //粗体
    private static readonly Regex iRegex = new Regex(@"(<i>)(.*?)(</i>)", RegexOptions.IgnoreCase);                      //斜体
    private static readonly Regex sizeRegex = new Regex(@"(<size=.*?>)(.*?)(</size>)", RegexOptions.IgnoreCase);         //字体大小
    private static readonly Regex quadRegex = new Regex(@"<quad .*?>", RegexOptions.IgnoreCase);                         //quad
    private static readonly Regex spaceRegex = new Regex(@"\s");                                                         //空白字符
    private static readonly Regex quadImageRegex = new Regex(@"<quad img=(.*?) size=(\d+) width=(\d+(\.\d+)?) height=(\d+(\.\d+)?)/>", RegexOptions.IgnoreCase); //图片quad
    #endregion

    #region 私有字段
    //超链接文字颜色
    private static readonly string linkColor = "<color=blue>";

    private StringBuilder textBuilder = new StringBuilder();

    private List<LinkInfo> linkInfoList = new List<LinkInfo>();
    private List<ImageInfo> imgInfoList = new List<ImageInfo>();
    private List<Image> imgObjList = new List<Image>();
    private List<Image> underLineList = new List<Image>();

    private List<MatchCollection> richMatchCollertions = new List<MatchCollection>();

    private readonly static Dictionary<string, Sprite> imageResDict = new Dictionary<string, Sprite>();

    private bool isUIDirty = false;

    //19版本前的顶点数
    private int oldVertCount;
    #endregion


    //重写text属性
    public override string text
    {
        set
        {
            originalText = value;
            base.text = PrepareLink();
            oldVertCount = 6 * (m_Text.Length + 1);
            PrepareImage();
        }
    }


    /// <summary>
    /// 添加点击超链接后的事件
    /// </summary>
    /// <param name="linkIndex">超链接的索引</param>
    /// <param name="onClick">点击触发的事件</param>
    /// <returns>添加成功放回true</returns>
    public bool AddClickLinkEvent(int linkIndex, Action<string> onClick)
    {
        if (linkIndex >= linkInfoList.Count)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(AddClickLinkEvent)}(), linkIndex out of bounds!");
#endif
            return false;
        }

        linkInfoList[linkIndex].onClick += onClick;

        return true;
    }

    /// <summary>
    /// 移除点击超链接后的事件
    /// </summary>
    /// <param name="linkIndex">超链接的索引</param>
    /// <param name="onClick">点击触发的事件</param>
    public void RemoveClickLinkEvent(int linkIndex, Action<string> onClick)
    {
        if (linkIndex >= linkInfoList.Count)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(RemoveClickLinkEvent)}(), linkIndex out of bounds!");
#endif
            return;
        }

        linkInfoList[linkIndex].onClick -= onClick;
    }

    /// <summary>
    /// 移除该超链接点击后的事件
    /// </summary>
    /// <param name="linkIndex">超链接的索引</param>
    public void RemoveClickLinkAllEvent(int linkIndex)
    {
        linkInfoList[linkIndex].onClick = null;
    }


    /// <summary>
    /// 解析超链接标签
    /// </summary>
    private string PrepareLink()
    {
        textBuilder.Clear();
        linkInfoList.Clear();
        richMatchCollertions.Clear();
        richMatchCollertions.Add(colorRegex.Matches(originalText));
        richMatchCollertions.Add(bRegex.Matches(originalText));
        richMatchCollertions.Add(iRegex.Matches(originalText));
        richMatchCollertions.Add(sizeRegex.Matches(originalText));
        MatchCollection quadMatchCollection = quadRegex.Matches(originalText);
        MatchCollection spaceMatchCollection = spaceRegex.Matches(originalText);
        MatchCollection linkMatchCollection = linkRegex.Matches(originalText);

        //  遍历超链接标签的Match
        //  维护LinkInfo中的startIndex和textBuilder
        int index = 0;
        foreach (Match match in linkMatchCollection)
        {
            textBuilder.Append(originalText.Substring(index, match.Index - index));
            textBuilder.Append(linkColor);
            textBuilder.Append(match.Groups[2].Value);
            textBuilder.Append("</color>");

            int startIndex = match.Index;
            int oldStartIndex = match.Index + linkColor.Length;

            //遍历富文本标签维护startIndex
            foreach (MatchCollection mc in richMatchCollertions)
            {
                foreach (Match m in mc)
                {
                    if (m.Groups[1].Index < match.Index)
                    {
                        startIndex -= m.Groups[1].Length;
                    }
                    if (m.Groups[3].Index < match.Index)
                    {
                        startIndex -= m.Groups[3].Length;
                    }
                }
            }

            //遍历quad标签维护startIndex
            foreach (Match m in quadMatchCollection)
            {
                if (m.Index < match.Index)
                {
                    startIndex -= m.Length;
                    startIndex++;

                    //quad标签里会出现空格
                    startIndex += 4;
                }
            }

            //遍历超链接标签维护startIndex
            foreach (Match m in linkMatchCollection)
            {
                if (m.Index < match.Index)
                {
                    startIndex -= m.Length;
                    startIndex += m.Groups[2].Length;

                    //空格
                    startIndex++;

                    oldStartIndex -= m.Length;
                    oldStartIndex += m.Groups[2].Length;

                    oldStartIndex += linkColor.Length;
                    oldStartIndex += "</color>".Length;
                }
            }

            //遍历空白字符维护startIndex
            foreach (Match m in spaceMatchCollection)
            {
                if (m.Index < match.Index)
                {
                    startIndex--;
                }
            }

            //构造超链接信息
            LinkInfo info = new LinkInfo();
            info.startIndex = startIndex * 6;
            info.oldStartIndex = oldStartIndex * 6;
            info.oldEndIndex = (oldStartIndex + match.Groups[2].Value.Length) * 6 - 1;
            info.endIndex = (startIndex + match.Groups[2].Value.Length) * 6 - 1;
            info.param = match.Groups[1].Value;

            linkInfoList.Add(info);

            //移动index用于构造text
            index = match.Index + match.Length;
        }

        //补全text
        textBuilder.Append(originalText.Substring(index, originalText.Length - index));

        return textBuilder.ToString();
    }

    /// <summary>
    /// 解析图片标签
    /// </summary>
    private void PrepareImage()
    {
        imgInfoList.Clear();
        richMatchCollertions.Clear();
        richMatchCollertions.Add(colorRegex.Matches(text));
        richMatchCollertions.Add(bRegex.Matches(text));
        richMatchCollertions.Add(iRegex.Matches(text));
        richMatchCollertions.Add(sizeRegex.Matches(text));
        MatchCollection quadMatchCollection = quadRegex.Matches(text);
        MatchCollection spaceMatchCollection = spaceRegex.Matches(text);
        MatchCollection quadImgMatchCollection = quadImageRegex.Matches(text);

        //遍历quad img标签,维护ImageInfo中的startIndex
        foreach (Match match in quadImgMatchCollection)
        {
            int startIndex = match.Index;
            int oldStartIndex = match.Index;

            //遍历富文本标签维护startIndex
            foreach (MatchCollection mc in richMatchCollertions)
            {
                foreach (Match m in mc)
                {
                    if (m.Groups[1].Index < match.Index)
                    {
                        startIndex -= m.Groups[1].Length;
                    }
                    if (m.Groups[3].Index < match.Index)
                    {
                        startIndex -= m.Groups[3].Length;
                    }
                }
            }


            //遍历quad标签维护startIndex
            foreach (Match m in quadMatchCollection)
            {
                if (m.Index < match.Index)
                {
                    startIndex -= m.Length;
                    startIndex++;

                    //注意空格
                    startIndex += 4;
                }
            }

            //遍历空白字符维护startIndex
            foreach (Match m in spaceMatchCollection)
            {
                if (m.Index < match.Index)
                {
                    startIndex--;
                }
            }

            //构建图片信息
            ImageInfo info = new ImageInfo();
            info.startIndex = startIndex * 6;
            info.oldStartIndex = oldStartIndex * 6;
            info.oldEndIndex = (oldStartIndex + 1) * 6 - 1;
            info.endIndex = (startIndex + 1) * 6 - 1;
            info.path = match.Groups[1].Value;
            info.width = int.Parse(match.Groups[2].Value) * float.Parse(match.Groups[3].Value);
            info.height = int.Parse(match.Groups[2].Value) * float.Parse(match.Groups[5].Value);

            imgInfoList.Add(info);
        }
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        base.OnPopulateMesh(toFill);

        List<UIVertex> verts = new List<UIVertex>();
        toFill.GetUIVertexStream(verts);

        //遍历超链接信息的List,设置其点击范围
        foreach (LinkInfo info in linkInfoList)
        {
            if (verts.Count == oldVertCount)
            {
                info.trueStartIndex = info.oldStartIndex;
                info.trueEndIndex = info.oldEndIndex;
            }
            else
            {
                info.trueStartIndex = info.startIndex;
                info.trueEndIndex = info.endIndex;
            }

            info.boxes.Clear();

#if UNITY_EDITOR
            if (info.trueStartIndex >= verts.Count)
            {
                Debug.LogError($"\"trueStartIndex\" value is error, trueStartIndex:{info.trueStartIndex}");
                continue;
            }
#endif
            var pos = verts[info.trueStartIndex].position;
            var bounds = new Bounds(pos, Vector3.zero);

            //根据顶点不断扩展点击范围
            float lastPosHeight;
            float lastPosToCurPosHeight;
            Vector3 curPos = verts[info.trueStartIndex].position;
            for (int i = info.trueStartIndex + 1; i <= info.trueEndIndex && i < verts.Count; ++i)
            {
                curPos = verts[i].position;
                if (0 == i % 6 && i - 6 >= 0)
                {
                    lastPosHeight = verts[i - 6].position.y - verts[i - 6 + 4].position.y;
                    lastPosToCurPosHeight = verts[i - 6].position.y - curPos.y;

                    if (lastPosToCurPosHeight > 0 && lastPosToCurPosHeight > lastPosHeight)
                    {
                        info.boxes.Add(new Rect(bounds.min, bounds.size));
                        bounds = new Bounds(curPos, Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(curPos);
                    }
                }
                else
                {
                    bounds.Encapsulate(curPos);
                }
            }

            info.boxes.Add(new Rect(bounds.min, bounds.size));
        }


        //遍历图片信息List,设置其图片坐标
        foreach (ImageInfo info in imgInfoList)
        {
            if (verts.Count == oldVertCount)
            {
                info.trueStartIndex = info.oldStartIndex;
                info.trueEndIndex = info.oldEndIndex;
            }
            else
            {
                info.trueStartIndex = info.startIndex;
                info.trueEndIndex = info.endIndex;
            }

#if UNITY_EDITOR
            if (info.trueStartIndex >= verts.Count)
            {
                Debug.LogError($"\"trueStartIndex\" value is error, trueStartIndex:{info.trueStartIndex}");
                continue;
            }
#endif

            var pos = verts[info.trueStartIndex].position;
            var bounds = new Bounds(pos, Vector3.zero);

            //蜷缩顶点
            for (int i = info.trueStartIndex + 1; i <= info.trueEndIndex && i < verts.Count; i++)
            {
                pos = verts[i].position;
                bounds.Encapsulate(pos);
                UIVertex uv = verts[i];
                uv.position = verts[info.trueStartIndex].position;
                verts[i] = uv;
            }
            info.pos = bounds.center;
        }

        //重置顶点数据
        if (imgInfoList.Count > 0)
        {
            toFill.Clear();
            toFill.AddUIVertexTriangleStream(verts);
        }

        //命令在Update处更新UI
        isUIDirty = true;
    }

    private void Update()
    {
        if (isUIDirty)
        {
            //需要将UI元素放入主线程处理
            RebuildImage();
            RebuildLine();
            isUIDirty = !isUIDirty;
        }
    }

    /// <summary>
    ///根据超链接生成下划线
    /// </summary>
    private void RebuildLine()
    {
        List<Rect> boxes = new List<Rect>();
        int lineCount = 0;
        foreach (LinkInfo info in linkInfoList)
        {
            foreach (Rect box in info.boxes)
            {
                boxes.Add(box);
            }
            lineCount += info.boxes.Count;
        }

        int lineObjCount = underLineList.Count;

        if (lineObjCount >= lineCount)
        {
            //当List中的游戏对象足够时
            for (int i = 0; i < lineCount; i++)
            {
                Image img = underLineList[i];
                Transform tr = img.transform;

                img.color = underLineColor;
                tr.localPosition = new Vector3(boxes[i].center.x, boxes[i].yMin, 0);
                tr.GetComponent<RectTransform>().sizeDelta = new Vector2(boxes[i].width, underLineWidth);
            }
            for (int i = lineCount; i < lineObjCount; i++)
            {
                underLineList[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }
        else
        {
            //当List中的游戏对象不足时
            for (int i = 0; i < lineObjCount; i++)
            {
                Image img = underLineList[i];
                Transform tr = img.transform;

                tr.localPosition = new Vector3(boxes[i].center.x, boxes[i].yMin, 0);
                tr.GetComponent<RectTransform>().sizeDelta = new Vector2(boxes[i].width, underLineWidth);
                img.color = underLineColor;

            }
            for (int i = lineObjCount; i < lineCount; i++)
            {
                GameObject obj = new GameObject("UnderLine");
                Image img = obj.AddComponent<Image>();
                Transform tr = obj.transform;

                tr.SetParent(transform);
                tr.localScale = Vector3.one;
                tr.localPosition = new Vector3(boxes[i].center.x, boxes[i].yMin, 0);
                obj.GetComponent<RectTransform>().sizeDelta = new Vector2(boxes[i].width, underLineWidth);
                img.color = underLineColor;

                img.raycastTarget = false;
                img.maskable = false;

                underLineList.Add(img);
            }
        }
    }

    /// <summary>
    /// 根据信息生成图片
    /// </summary>
    private void RebuildImage()
    {
        int imgObjCount = imgObjList.Count;
        if (imgObjCount >= imgInfoList.Count)
        {
            //当List中游戏对象足够时

            for (int i = 0; i < imgInfoList.Count; i++)
            {
                Image img = imgObjList[i];
                ImageInfo info = imgInfoList[i];
                Sprite sprite;
                if (imageResDict.TryGetValue(info.path, out sprite))
                {
                    img.sprite = sprite;
                }
                else
                {
                    sprite = Resources.Load<Sprite>(info.path);
                    imageResDict.Add(info.path, sprite);

                    img.sprite = sprite;
                }
                img.color = new Color(1f, 1f, 1f, 1f);
                img.transform.localPosition = info.pos;
                img.GetComponent<RectTransform>().sizeDelta = new Vector2(info.width, info.height);
            }
            for (int i = imgInfoList.Count; i < imgObjCount; i++)
            {
                imgObjList[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }
        else
        {
            //当List中的游戏对象不够时

            for (int i = 0; i < imgObjCount; i++)
            {
                Image img = imgObjList[i];
                ImageInfo info = imgInfoList[i];
                Sprite sprite;
                if (imageResDict.TryGetValue(info.path, out sprite))
                {
                    img.sprite = sprite;
                }
                else
                {
                    sprite = Resources.Load<Sprite>(info.path);
                    imageResDict.Add(info.path, sprite);

                    img.sprite = sprite;
                }
                img.color = new Color(1f, 1f, 1f, 1f);
                img.transform.localPosition = info.pos;
                img.GetComponent<RectTransform>().sizeDelta = new Vector2(info.width, info.height);
            }
            for (int i = imgObjCount; i < imgInfoList.Count; i++)
            {

                GameObject obj = new GameObject("ImageObj");
                Image img = obj.AddComponent<Image>();
                ImageInfo info = imgInfoList[i];

                Sprite sprite;
                if (imageResDict.TryGetValue(info.path, out sprite))
                {
                    img.sprite = sprite;
                }
                else
                {
                    sprite = Resources.Load<Sprite>(info.path);
                    imageResDict.Add(info.path, sprite);

                    img.sprite = sprite;
                }
                obj.transform.SetParent(transform);
                obj.transform.localScale = Vector3.one;
                obj.transform.localPosition = info.pos;
                obj.GetComponent<RectTransform>().sizeDelta = new Vector2(info.width, info.height);

                img.raycastTarget = false;
                img.maskable = false;


                imgObjList.Add(img);
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        text = originalText;
    }
#endif
    
    protected override void OnEnable()
    {
        base.OnEnable();

        //  设定初始值
        //  其他换行模式可能会出现BUG
        horizontalOverflow = HorizontalWrapMode.Wrap;
        verticalOverflow = VerticalWrapMode.Overflow;

#if UNITY_EDITOR
        imgObjList.Clear();
        underLineList.Clear();
        //索引从后往前清空子对象
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }          
        }
#endif
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 localCursorPos; //局部空间的光标位置
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localCursorPos);

        foreach (var info in linkInfoList)
        {
            foreach (var box in info.boxes)
            {
                if (box.Contains(localCursorPos))
                {
                    info.onClick?.Invoke(info.param);
                }
            }
        }
    }
}
