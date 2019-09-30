using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PagePayload 
{
    public bool is_invalid = true;
    private Vector2Int m_tile_index;
    public Vector2Int tileIndex { get { return m_tile_index; }
        set {
            m_tile_index = value; is_invalid = false;
        } }

    public int activeFrame;

    public LoadRequest loadRequest;

    public bool IsReady()
    {
        return !is_invalid;
    }

    public void Reset()
    {
        is_invalid = true;
    }
}
