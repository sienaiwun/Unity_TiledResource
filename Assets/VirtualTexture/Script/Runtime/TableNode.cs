using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class TableNode 
{
    private TableNode[] m_children ;
    public int MaxMipLevel;

    public RectInt Rect;

    public PagePayload Payload;

    public TableNode(int mip,int x, int y, int width, int height)
    {
        Rect = new RectInt(x, y, width, height);
        Payload = new PagePayload();
        MaxMipLevel = mip;
    }

    public TableNode GetNextChild(int x,int y)
    {
        if (!Contain(x, y))
            return null;
        if (MaxMipLevel == 0)
            return null;
        if(m_children== null)
        {
            m_children = new TableNode[4];
            int halfwidth = Rect.width / 2;
            int halfheight = Rect.height / 2;
            int midx = Rect.x + halfwidth;
            int midy = Rect.y + halfheight;

            m_children[0] = new TableNode(MaxMipLevel - 1, x, y, halfwidth, halfheight);
            m_children[1] = new TableNode(MaxMipLevel - 1, midx, y, halfwidth, halfheight);
            m_children[2] = new TableNode(MaxMipLevel - 1, x, midy, halfwidth, halfheight);
            m_children[3] = new TableNode(MaxMipLevel - 1, midx, midy, halfwidth, halfheight);
        }
        foreach (TableNode child in m_children)
        {
            if (child.Contain(x, y))
                return child;
        }
        Assert.IsTrue(true, "never get here");
        return null;
    }

    public TableNode GetExact(int x, int y, int mip)
    {
        if (!Contain(x, y))
            return null;
        if (MaxMipLevel == mip)
            return this;
        if(m_children!= null)
        {
            foreach (TableNode node in m_children)
            {
                TableNode child = node.GetExact(x, y, mip);
                if (child != null)
                    return child;
            }
        }
        return null;
    }

    public TableNode GetAvailable(int x, int y, int mip)
    {
        // 不需要mipmap 相等
        if (!Contain(x, y))
            return null;
        if(mip< MaxMipLevel&& m_children!= null)
        {
            foreach (TableNode node in m_children)
            {
                TableNode child = node.GetAvailable(x, y, mip);
                if (child != null)
                    return child;
            }
        }
        return (Payload.IsReady() ? this : null);
    }


    public bool Contain(int x, int y)
    {
        if (x < Rect.xMin || x > Rect.xMax)
            return false;
        if (y < Rect.yMin || y > Rect.yMax)
            return false;
        return true;
    }
}
