using UnityEngine;
using UnityEngine.UIElements;
using Canvas = Unity.AppUI.UI.Canvas;

namespace Unity.Behavior.GraphFramework
{
    internal static class GraphViewSpatialExtensions 
    {
        public static Vector2 WorldPosToLocal(this GraphView graphView, Vector2 position)
        {
            Vector2 local = graphView.Viewport.WorldToLocal(position);
            return local;
        }
        
        public static NodeUI NodeAt(this GraphView graphView, Vector2 pos)
        {
            foreach (NodeUI nodeUI in graphView.ViewState.Nodes)
            {
                if (nodeUI.ContainsPoint(nodeUI.WorldToLocal(pos)))
                {
                    return nodeUI;
                }
            }
            return null;
        }
        
        public static Edge EdgeAt(this GraphView graphView, Vector2 pos)
        {
            foreach (Edge edge in graphView.ViewState.Edges)
            {
                if (edge.ContainsPoint(edge.WorldToLocal(pos)))
                {
                    return edge;
                }
            }
            return null;
        }
    }
}