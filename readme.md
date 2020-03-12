## 虚拟贴图的Unity实现和硬件加速方法
#### 简介
虚拟贴图方法能优化显存的贴图资源，本文介绍了如何在Unity中"软件化"地实现虚拟贴图，并同时介绍在DirectX中对虚拟贴图的硬件支持。
#### 引言
贴图资源是一种稀疏的图形绘制资源：理想情况下，屏幕空间像素只需要屏幕空间像素数量`pixls`的贴图纹素`texels`资源，换句话说，常驻内存或者显存的贴图资源不会太大。但是现有情况下，由于我们只是看到了大型贴图的一部分，或者我们只需要mipmap中的某一些特定mipmap chain，我们会加载大量的不需要的纹理资源到内存中，这样，大部分贴图资源都闲置在内存中没有应用，尤其是主机端游戏开发，由于内存的紧缺，虚拟贴图的使用会更加重要。虚拟贴图的概念很早就有，不需要额外的硬件支持，本文介绍了如何在Unity(截止2019.3还不支持硬件虚拟贴图)下支持虚拟贴图，并给出对应的资源管线处理工具。 然后介绍DirectX中使用[Tiled Resource](https://docs.microsoft.com/en-us/windows/win32/direct3d11/tiled-resources)进行加速。


#### 虚拟贴图
虚拟贴图的目的是只把当前帧需要用到的贴图资源部分提交到显存中。这样做的好处是可以允许使用者在显存或者主存硬件资源有限的情况下在离线端或者是IO上存放更大的资源。虚拟贴图其实更操作系统中的虚拟内存是一个概念，实质也是一样的。
虚拟贴图不需要在模型中重新计算UV，而是在实时的shader中在需要在UV的采样时额外通过地址转换算出在实际物理贴图中的位置后再进行采样。
由于通常情况下，几何体合batch限制在贴图的使用上，使用虚拟贴图的话Batch的效率会更好，因为所有的物件都可以去索引这张理论上无限大的纹理，极限情况下所有的场景和贴图可以在一个批次内完成绘制。
需要特别注意如何正确的加载虚拟贴图中的一个个贴图页块(这里使用操作系统的说法)。相机的移动会伴随大量的贴图页块的加载，这时候就通常需要用多线程或者异步IO来处理文件的加载，在没加载完毕的情况下，我们通常会保留一个上层mipmap来让过渡更加顺滑。另外，还需要额外处理的还有采样器的选择（比如各向异性采样需要多层mipmap），各种边界条件，以及如何绘制透明物体都需要在实际中去仔细打磨。

#### Unity 虚拟贴图流程
下图显示了Unity下的虚拟贴图效果。该虚拟贴图的大小是 $16384\times16384$。每个块大小是$512$,每个边有$32$个块。实际使用的物理显存大小小于 $1024 \times 1024$
![vt_unity](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/virutal_texture.gif?raw=true)
我们使用如下[流程图](https://computergraphics.stackexchange.com/questions/1768/how-can-virtual-texturing-actually-be-efficient)来进行Unity下的虚拟贴图
![virtual_texture_pipeline.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/virtual_texture_pipeline.png?raw=true)
* Asset Pipeline:代码库下的脚本[tile_generator.py](https://github.com/sienaiwun/Unity_TilesResource/blob/master/Assets/tiles_generator.py)将对应的虚拟贴图图片资源划分到块中，同时生成虚拟贴图的各个mipmap链中的块。
经过处理后将贴图资源颗粒化划分为众多小块,下图分别展示mip0,mip1,和mip5的第一个小块:

![Tiles_MIP0_Y0_X0.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/Tiles_MIP0_Y0_X0.png?raw=true),
![Tiles_MIP1_Y0_X0.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/Tiles_MIP1_Y0_X0.png?raw=true),
![Tiles_MIP5_Y0_X0.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/Tiles_MIP5_Y0_X0.png?raw=true)


* Feedback Pass:回读当前场景中所需要的虚拟贴图块，在实现中选用一个低分辨率的framebuffer。该framebuffer记录了需要加载的贴图id. 贴图id由xy块索引值和mipmap层级构成。 在GPU绘制后通过CPU回读进行处理
* Page Manager:Page Manager处理回读的贴图id信息，把已经读取好的块上传到GPU的物理位置中，然后更新Indirect Texture。Page Manager维护了一个虚拟贴图的四叉树，该四叉树记录了page的优先级，同时也维护这GPU物理缓存的更新。通过当前四叉树和page ids确定加载pages队列，进行异步读取。对已经读取的page提交到GPU的物理缓存上，并更新Indirect Texture对应区块的偏移和缩放。
* Indirect Texture:Indirect Texuture 和块的尺寸相符，在本示例中为$32 \times 32$，每个纹素记录了所在纹素对应的缩放和偏移。虚拟贴图的纹理采样首先找到在IndirectTexture的对应纹素，该纹素记录了在实际物理内存的偏移和缩放，通过计算找到物理内存地址，进行采样。

###### Usage
1. Run [image_generator.py](https://github.com/sienaiwun/Unity_TilesResource/blob/master/Assets/image_generator.py)
2. Run [tile_generator.py](https://github.com/sienaiwun/Unity_TilesResource/blob/master/Assets/tiles_generator.py)
3. Unity Run Asset/VirtualTexture/Asset Demo.unity
#### DirectX中的硬件加速
在对虚拟贴图用硬件支持的API中，虚拟贴图的使用是透明的，不需要去显示构建indirect texture,而纹理采样也不需要进行地质转换，和普通贴图的使用是一样的。而且由于硬件的支持，在物理显存的排列组织上，不必添加padding的pixel，也能支持更高级带mipmapleivel的比如说各向异性的采样方式（前提是需要的层级已经加载好）。另外高级的shader api使得更多的操作能集成在显卡运算中。
DirectX 在dx11.2中集成了tiled Resource 使用它可以硬件层面上提升virtual texture的效率。
在高级API中对上诉流程进行效率优化的地方有：
* Feedback Pass可以直接集成在贴图绘制中，通过写入structure buffer来统计出需要的纹理id.
如:
```cpp
 float minLod = texTiled.CalculateLevelOfDetailUnclamped(sampler0, vsOutput.uv);
uint pageCount = Size / pageSize;
   for (int i = 1; i <= int(minLod); i++)
   {
       pageOffset += pageCount * pageCount;
       pageCount = pageCount / 2;
   }

   pageOffset += uint(float(pageCount) *  vsOutput.uv.x) + uint(float(pageCount) *  vsOutput.uv.y) * pageCount;
   VisibilityBuffer[pageOffset] = 1;
```
* Indirect buffer 直接由[硬件API](https://docs.microsoft.com/en-us/windows/win32/api/d3d11_2/nf-d3d11_2-id3d11devicecontext2-updatetilemappings)进行构建，传入需要的tile信息组和已上传贴图资源即可。在采样时由硬件进行地质转码，不仅带来效率的提升，在shader的使用上也是透明发。
更Unity 一样的场景，换个颜色
![checkbox_vt.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/virtual_texture_dx12.gif?raw=true)
##### 可视化
我们用棋盘格进行可视化，在贴近棋盘格的如下的视角下：
![checkbox_vt.png](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/checkbox_vt.png?raw=true)
通过可视化工具即可看到只有很小的一部分贴图资源被加载实际物理显存中，极大的节省了物理显存的使用:
![mip_chain.gif](https://github.com/sienaiwun/publicImgs/blob/master/imgs/VirtualTexture/mip_chain.gif?raw=true)


#### 参考资料
[Software Virtual Texture](http://www.mrelusive.com/publications/papers/Software-Virtual-Textures.pdf)

[Real Virtual Texturing](https://developer.nvidia.com/sites/default/files/akamai/gameworks/events/gdc14/GDC_14_Real%20Virtual%20Texturing%20-%20Taking%20Advantage%20of%20DirectX%2011.2%20Tiled%20Resources.pdf)
