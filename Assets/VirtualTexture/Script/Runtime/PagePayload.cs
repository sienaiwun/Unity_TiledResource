using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PagePayload 
{
    public bool is_invalid = true;
    public Vector2Int tileIndex;

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
