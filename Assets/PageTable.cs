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

    void Start()
    {
        m_LookupTexture = new Texture2D(TableSize, TableSize, TextureFormat.RGBA32, false);
        m_LookupTexture.filterMode = FilterMode.Point;
        m_LookupTexture.wrapMode = TextureWrapMode.Clamp;

        m_RootPageNode = new TableNode(MaxMipLevel, 0, 0, TableSize, TableSize);
        Shader.SetGlobalVector(
               "_VTFeedbackParam",
               new Vector4(TableSize,
                           TableSize * 256,
                           TableSize,
                           0.0f));

        m_Loader = (FileLoader)GetComponent(typeof(FileLoader));
        ((FeedBackCamera)GetComponent(typeof(FeedBackCamera))).readTextureAction += ProcessFeedback;
    }

    private void ProcessFeedback(Texture2D texture)
    {
        foreach (Color32 readpixel in texture.GetRawTextureData<Color32>())
        {
            ActivatePage(readpixel);
        }
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
        if (mip > m_RootPageNode.MaxMipLevel) // clear color
                return null;
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
        return node;

    }
}
