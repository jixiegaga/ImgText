using UnityEngine;

/// <summary>
/// 超链接使用例子
/// </summary>
public class Example : MonoBehaviour
{
    private ImgText textEX;
    private void Start()
    {
        textEX = GetComponent<ImgText>();


        //  添加超链接的点击回调事件
        //  其中url的值是 填写标签时的param
        //  0表示baidu.com的超链接
        //  1表示google.com的超链接
        //  索引顺序是按照在Text的出现顺序定的
        textEX.AddClickLinkEvent(0, (url) => { Application.OpenURL(url); });
        textEX.AddClickLinkEvent(1, (url) => { Application.OpenURL(url); });
    }

    private void Update()
    {
        //按下A键 移除超链接的点击回调事件
        if(Input.GetKeyDown(KeyCode.A))
        {
            textEX.RemoveClickLinkAllEvent(0);
            textEX.RemoveClickLinkAllEvent(1);
        }
    }
}
