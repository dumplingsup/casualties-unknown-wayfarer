using System.Linq;
using UnityEngine;

namespace WayfarerMech
{
    // Draw the live renderer poses unlit inside the panel. No cloned Body, camera,
    // render layer changes, or render textures; the player's face remains live.
    public static class AssemblyPreview
    {
        public static void Draw(Rect area, MechController mech)
        {
            GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill, false,
                0f, new Color(0.15f, 0.18f, 0.20f), 0f, 0f);
            if (Event.current.type != EventType.Repaint || !mech) return;
            var renderers = mech.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled && r.sprite && r.color.a > 0f)
                .OrderBy(r => SortingLayer.GetLayerValueFromID(r.sortingLayerID))
                .ThenBy(r => r.sortingOrder).ToArray();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float zoom = Mathf.Min((area.width - 24) / Mathf.Max(1, bounds.size.x),
                (area.height - 24) / Mathf.Max(1, bounds.size.y));
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            try
            {
                var clipMatrix = GUI.matrix;
                foreach (var r in renderers)
                {
                    var sprite = r.sprite;
                    var size = sprite.rect.size / sprite.pixelsPerUnit;
                    var pivot = sprite.pivot / sprite.pixelsPerUnit;
                    var pos = r.transform.position - bounds.center;
                    var scale = r.transform.lossyScale;
                    GUI.matrix = clipMatrix * Matrix4x4.TRS(
                        new Vector3(area.center.x + pos.x * zoom, area.center.y - pos.y * zoom),
                        Quaternion.Euler(0, 0, -r.transform.eulerAngles.z),
                        new Vector3(scale.x * zoom * (r.flipX ? -1 : 1),
                            scale.y * zoom * (r.flipY ? -1 : 1), 1));
                    var tex = sprite.texture;
                    var uv = sprite.textureRect;
                    uv = new Rect(uv.x / tex.width, uv.y / tex.height, uv.width / tex.width, uv.height / tex.height);
                    GUI.color = r.color;
                    GUI.DrawTextureWithTexCoords(new Rect(-pivot.x, pivot.y - size.y, size.x, size.y), tex, uv);
                }
            }
            finally
            {
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
        }
    }
}
