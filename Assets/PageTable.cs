using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PageTable : MonoBehaviour
{
    // Start is called before the first frame update

    public int m_TableSize = 64;

    public int TableSize { get { return m_TableSize; } }

    private Dictionary<Vector2Int, TableNode> m_ActivePages = new Dictionary<Vector2Int, TableNode>();

    public Texture2D m_LookupTexture;

    private TableNode m_RootPageNode;

    private int MaxMipLevel = 6;

    private FileLoader m_Loader;

    private TiledTexture m_tileTexture;

    void Start()
    {
        m_LookupTexture = new Texture2D(TableSize, TableSize, TextureFormat.RGBA32, false);
        m_LookupTexture.filterMode = FilterMode.Point;
        m_LookupTexture.wrapMode = TextureWrapMode.Clamp;

        m_RootPageNode = new TableNode(MaxMipLevel, 0, 0, TableSize, TableSize);


        m_Loader = (FileLoader)GetComponent(typeof(FileLoader));
        m_tileTexture = (TiledTexture)GetComponent(typeof(TiledTexture));

        m_Loader.OnLoadCompleteEvent += OnLoadTextureFinished;
        ((FeedBackCamera)GetComponent(typeof(FeedBackCamera))).readTextureAction += ProcessFeedback;
        Shader.SetGlobalVector(
               "_VTFeedbackParam",
               new Vector4(TableSize,
                           TableSize * m_tileTexture.TileSize, // virtualTexture's 1d dimension
                           MaxMipLevel,
                           0.0f));

    }

    private void ProcessFeedback(Texture2D texture)
    {
        foreach (Color32 readpixel in texture.GetRawTextureData<Color32>())
        {
            ActivatePage(readpixel);
        }
        byte currentFrame = (byte)Time.frameCount;
        var pixels = m_LookupTexture.GetRawTextureData<Color32>();
        foreach (var kv in m_ActivePages)
        {
            TableNode node = kv.Value;
            if (node.Payload.activeFrame != Time.frameCount)
                continue;
            Color32 c = new Color32((byte)node.Payload.tileIndex.x, (byte)node.Payload.tileIndex.y, (byte)node.MaxMipLevel, currentFrame);
            for (int y = node.Rect.y; y < node.Rect.yMax; y++)
            {
                for (int x = node.Rect.x; x < node.Rect.xMax; x++)
                {
                    var id = y * TableSize + x;
                    if (pixels[id].b > c.b ||  // 写入mipmap等级最小的页表
                        pixels[id].a != currentFrame) // 当前帧还没有写入过数据
                        pixels[id] = c;
                }
            }
        }
        m_LookupTexture.Apply(false);
    }

    private void  LoadPage(int x,int y,TableNode node)
    {
        if (node == null)
            return;

        // 正在加载中,不需要重复请求
        if (node.Payload.loadRequest != null)
            return;
        //    return;

        // 新建加载请求
        node.Payload.loadRequest = m_Loader.Request(x, y, node.MaxMipLevel);
    }

    private TableNode ActivatePage(Color32 pixel)
    {
        int pagex = pixel.r;
        int pagey = pixel.g;
       
        int mip = pixel.b;
        if (mip == 255)
            return null;
        mip = Mathf.Min(mip, m_RootPageNode.MaxMipLevel); // clear color
        TableNode node = m_RootPageNode.GetAvailable(pagex, pagey, mip);
        if (node == null)
        {
            // 
            LoadPage(pagex, pagey, m_RootPageNode);
            return null;
        }
        else if (node.MaxMipLevel > mip)
        {
            LoadPage(pagex, pagey, node.GetNextChild(pagex,pagey));
        }
        m_tileTexture.SetActive(node.Payload.tileIndex);
        node.Payload.activeFrame = Time.frameCount;
        return node;

    }

    private void OnLoadTextureFinished(LoadRequest request, Texture2D texture)
    {
        TableNode node = m_RootPageNode.GetExact(request.PageX, request.PageY, request.MipLevel);
        if (node == null || node.Payload.loadRequest != request) // loading is completed
            return;
        node.Payload.loadRequest = null;
        Vector2Int id = m_tileTexture.UpdatePos();
        m_tileTexture.UpdateTile(id, texture);
        node.Payload.tileIndex = id;
        m_ActivePages[id] = node;
    }
}
