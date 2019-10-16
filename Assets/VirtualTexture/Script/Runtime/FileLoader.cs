using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class FileLoader : MonoBehaviour
{
    public event Action<LoadRequest,Texture2D> OnLoadCompleteEvent;

    private List<LoadRequest> m_RuningRequests = new List<LoadRequest>();

    private List<LoadRequest> m_PendingRequests = new List<LoadRequest>();

    public Texture2D m_debugTexture ;

    
    private void Start()
    {
        m_debugTexture = Texture2D.whiteTexture;
    }

    private void Update()
    {
        if (m_PendingRequests.Count <= 0)
            return;
        if (m_RuningRequests.Count >= 1)
            return;
        m_PendingRequests.Sort((x,y)=> { return x.MipLevel.CompareTo(y.MipLevel); });
        LoadRequest req = m_PendingRequests[m_PendingRequests.Count - 1];
        m_PendingRequests.RemoveAt(m_PendingRequests.Count - 1);
        m_RuningRequests.Add(req);
        StartCoroutine(Load(req));
    }

    private IEnumerator Load(LoadRequest request)
    {
        Texture2D texture = null ;
        
        var file = string.Format("file:///" + Path.Combine(Application.streamingAssetsPath, "Tiles_MIP{2}_Y{1}_X{0}.png"), request.PageX >> request.MipLevel, request.PageY >> request.MipLevel, request.MipLevel);
        Debug.Log("load texture:"+ file);
        var www = UnityWebRequestTexture.GetTexture(file);
        yield return www.SendWebRequest();

        if (!www.isNetworkError && !www.isHttpError)
        {
            texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
        }
        else
        {
            Debug.LogWarningFormat("Load file({0}) failed: {1}", file, www.error);
        }
        m_RuningRequests.Remove(request);
        if (texture)
            m_debugTexture = texture;
        OnLoadCompleteEvent?.Invoke(request, texture);
    }

    public LoadRequest Request(int x, int y, int mip)
    {
        foreach (var r in m_RuningRequests)
        {
            if (r.PageX == x && r.PageY == y && r.MipLevel == mip)
                return null;
        }
        foreach (var r in m_PendingRequests)
        {
            if (r.PageX == x && r.PageY == y && r.MipLevel == mip)
                return null;
        }
        var request = new LoadRequest(x, y, mip);
        m_PendingRequests.Add(request);
        return request;
    }

    }
