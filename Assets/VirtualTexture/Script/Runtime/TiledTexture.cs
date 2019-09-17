using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TiledTexture : MonoBehaviour
{
    // 二维 虚拟内存
    public Vector2Int RegionSize = new Vector2Int(6,6);

    [SerializeField]
    private int m_tiled_size = 128;

    [SerializeField]
    private int m_padding_size = 4;

    private LruCache pools = new LruCache();

    public int TileSize { get { return m_tiled_size; } }

    public int TileSizeWithPadding { get { return m_tiled_size + 2 * m_padding_size; } }

    public RenderTexture tileTexture;

    [SerializeField]
    private Shader m_DrawTextureShader = default;
    private Material m_DrawTextureMateral;

    private void Start()
    {
        for (int i = 0; i < RegionSize.x * RegionSize.y; i++)
            pools.Add(i);
        tileTexture = new RenderTexture(RegionSize.x * TileSizeWithPadding, RegionSize.y * TileSizeWithPadding, 0);
        tileTexture.useMipMap = false;
        tileTexture.wrapMode = TextureWrapMode.Clamp;
        m_DrawTextureMateral = new Material(m_DrawTextureShader);
    }

    private int PosToId(Vector2Int tileIndex)
    {
        return (tileIndex.x) * RegionSize.x + tileIndex.y;
    }

    private Vector2Int IdToPos(int id)
    {
        return new Vector2Int(id / RegionSize.x, id % RegionSize.x);
    }

    public Vector2Int UpdatePos()
    {
        return IdToPos(pools.First);
    }

    public bool SetActive(Vector2Int tileIndes)
    {
        return pools.SetActive(PosToId(tileIndes));
    }

    public void UpdateTile(Vector2Int tileIndex,Texture newLoadTexture)
    {
        bool textureIsLoaded = pools.SetActive(PosToId(tileIndex));
        if (!textureIsLoaded)
            return;

        RectInt renderPos = new RectInt(tileIndex.x * TileSizeWithPadding, tileIndex.y * TileSizeWithPadding, TileSizeWithPadding, TileSizeWithPadding);
        DrawTexture(newLoadTexture, renderPos);
    }

    private void DrawTexture(Texture input, RectInt position)
    {
        if (input == null  || m_DrawTextureMateral == null)
            return;
        float l = position.x * 2.0f / tileTexture.width - 1;
        float r = (position.x + position.width) * 2.0f / tileTexture.width - 1;
        float b = position.y * 2.0f / tileTexture.height - 1;
        float t = (position.y + position.height) * 2.0f / tileTexture.height - 1;

        Matrix4x4 mat = new Matrix4x4();
        mat.m00 = r - l;
        mat.m03 = l;
        mat.m11 = t - b;
        mat.m13 = b;
        mat.m23 = -1;
        mat.m33 = 1;

        // 绘制贴图
        m_DrawTextureMateral.SetMatrix(Shader.PropertyToID("_ImageMVP"), GL.GetGPUProjectionMatrix(mat, true));
        
        Graphics.Blit(input, tileTexture, m_DrawTextureMateral);
    }
}
