using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class NoDrawGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}