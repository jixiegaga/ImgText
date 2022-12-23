# *功能介绍*
1. 通过对UGUI的Text组件进行了拓展，实现了图文混排和超链接下划线的功能。  
2. 只适用于unity2019以上版本，19以下的版本由于顶点数据的不同会导致位置错乱和出现乱码。  
3. 只支持静态图片。  
4. 图文混排的大致实现原理是通过\<quad>标签进行自动占位，然后通过顶点数据取得图片坐标生成Image实现的。
5. 关于性能方面没有做过优化。
6. Image的加载直接使用的是Resources.Load()。

# *效果预览*
![](https://github.com/535382/ImgText/blob/master/Preview/%E4%BE%8B%E5%AD%901.png?raw=true)
![](https://github.com/535382/ImgText/blob/master/Preview/%E4%BE%8B%E5%AD%902.png?raw=true)

# *使用说明*
1. 在场景中新建Canvas，然后在Canvas下新建一个游戏对象。
2. 在游戏对象上添加ImgText组件。
3. 将图片文件放到Resources文件夹下并调整成Sprite/UI模式。
4. 通过输入\<quad img=Emoji_1 size=80 width=1 height=1/>就可以生成图片了。
5. 超链接则通过\<url param=abc>abcdef.com\</url>生成。超链接的点击操作可以通过项目内的Example场景和Example.cs代码查看使用方式。