using System;
using System.Collections.Generic;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// Renders the real pilot's head, eyes, and a small upper-torso slice inside the open
    /// cockpit. It never clones a Body, Rigidbody, Collider, Item, or behaviour script;
    /// it only mirrors SpriteRenderers from the already-authenticated player body.
    /// The real Body remains frozen under the controller's existing pilot isolation.
    /// </summary>
    public sealed class PilotVisualProxy : MonoBehaviour
    {
        private sealed class Mirror
        {
            public SpriteRenderer Source;
            public SpriteRenderer Proxy;
        }

        private Body body;
        private FacialExpression face;
        private SpriteRenderer sourceHead, sourceEyes, sourceNose, sourceTorso;
        private SpriteRenderer headProxy, eyesProxy, noseProxy, torsoProxy;
        private Transform headRoot, torsoRoot;
        private readonly List<Mirror> headMirrors = new List<Mirror>();
        private readonly List<Mirror> torsoMirrors = new List<Mirror>();
        private int baseSort;
        private bool built;
        private bool lastVisible;

        public bool HasFace => sourceHead != null && sourceHead.sprite != null;
        public string HeadSprite => sourceHead && sourceHead.sprite ? sourceHead.sprite.name : "none";
        public string EyesSprite => sourceEyes && sourceEyes.sprite ? sourceEyes.sprite.name : "none";
        public int HeadMirrorCount => headMirrors.Count;
        public int TorsoMirrorCount => torsoMirrors.Count;

        public static PilotVisualProxy Create(Body pilot, Transform parent, Vector3 localPosition, int sortingBase)
        {
            var go = new GameObject("pilot-visual-proxy");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            // Native game sprites are large next to the compact mech art. A small uniform
            // reduction keeps the real head readable without turning the body into a prop.
            go.transform.localScale = Vector3.one * 0.9f;
            var proxy = go.AddComponent<PilotVisualProxy>();
            proxy.Construct(pilot, sortingBase);
            return proxy;
        }

        private void Construct(Body pilot, int sortingBase)
        {
            body = pilot;
            baseSort = sortingBase;
            if (!body)
            {
                Plugin.LogError("PILOT_PROXY_NO_BODY");
                return;
            }

            face = body.GetComponentInChildren<FacialExpression>(true);
            if (face != null)
            {
                sourceHead = face.head;
                sourceEyes = face.eyes;
                sourceNose = face.nosebleedSprite;
            }
            if (sourceHead == null && body.limbs != null && body.limbs.Length > 0 && body.limbs[0] != null)
                sourceHead = body.limbs[0].GetComponent<SpriteRenderer>();
            if (body.limbs != null && body.limbs.Length > 1 && body.limbs[1] != null)
                sourceTorso = body.limbs[1].GetComponent<SpriteRenderer>();

            if (sourceHead == null)
            {
                Plugin.LogError("PILOT_PROXY_NO_HEAD_RENDERER");
                return;
            }

            // Build before the controller hides the player renderers so we know which
            // accessories were actually visible (hair, helmet, ear, etc.).
            headRoot = new GameObject("pilot-head").transform;
            headRoot.SetParent(transform, false);
            headRoot.localPosition = Vector3.zero;
            headRoot.localRotation = Quaternion.identity;

            headProxy = CreateSprite("head", headRoot, sourceHead, baseSort + 2);
            headProxy.transform.localPosition = Vector3.zero;
            headProxy.transform.localRotation = Quaternion.identity;
            headProxy.transform.localScale = RelativeScale(sourceHead.transform, body.transform);

            if (sourceEyes != null)
            {
                eyesProxy = CreateSprite("eyes", headProxy.transform, sourceEyes, baseSort + 3);
                PlaceRelative(eyesProxy.transform, sourceEyes.transform, sourceHead.transform);
            }
            if (sourceNose != null)
            {
                noseProxy = CreateSprite("nose", headProxy.transform, sourceNose, baseSort + 4);
                PlaceRelative(noseProxy.transform, sourceNose.transform, sourceHead.transform);
            }
            MirrorChildren(sourceHead.transform, headProxy.transform, headMirrors, baseSort + 2, sourceEyes, sourceNose);

            if (sourceTorso != null)
            {
                torsoRoot = new GameObject("pilot-torso").transform;
                torsoRoot.SetParent(transform, false);
                torsoProxy = CreateSprite("torso", torsoRoot, sourceTorso, baseSort);
                torsoProxy.transform.localPosition = Vector3.zero;
                torsoProxy.transform.localRotation = Quaternion.identity;
                torsoProxy.transform.localScale = RelativeScale(sourceTorso.transform, body.transform);
                torsoRoot.localPosition = ComputeTorsoOffset();
                MirrorChildren(sourceTorso.transform, torsoProxy.transform, torsoMirrors, baseSort + 1, null, null);
            }

            built = true;
            Plugin.LogInfo("PILOT_PROXY_BUILT face=" + (face != null) + " head=" + sourceHead.sprite.name +
                           " eyes=" + (sourceEyes ? (sourceEyes.sprite ? sourceEyes.sprite.name : "null") : "none") +
                           " torso=" + (sourceTorso ? (sourceTorso.sprite ? sourceTorso.sprite.name : "null") : "none") +
                           " headMirrors=" + headMirrors.Count + " torsoMirrors=" + torsoMirrors.Count);
        }

        private Vector3 ComputeTorsoOffset()
        {
            if (sourceHead == null || sourceHead.sprite == null || sourceTorso == null || sourceTorso.sprite == null)
                return new Vector3(0f, -0.6f, 0f);
            Vector3 hs = sourceHead.sprite.bounds.size;
            Vector3 ts = sourceTorso.sprite.bounds.size;
            float y = -hs.y * 0.34f - ts.y * 0.30f;
            return new Vector3(0f, y, 0f);
        }

        private static Vector3 RelativeScale(Transform source, Transform reference)
        {
            var a = source.lossyScale;
            var b = reference != null ? reference.lossyScale : Vector3.one;
            return new Vector3(
                Mathf.Approximately(b.x, 0f) ? 1f : Mathf.Abs(a.x / b.x),
                Mathf.Approximately(b.y, 0f) ? 1f : Mathf.Abs(a.y / b.y),
                1f);
        }

        private static void PlaceRelative(Transform target, Transform source, Transform reference)
        {
            target.localPosition = reference.InverseTransformPoint(source.position);
            target.localRotation = Quaternion.Inverse(reference.rotation) * source.rotation;
            target.localScale = RelativeScale(source, reference);
        }

        private SpriteRenderer CreateSprite(string name, Transform parent, SpriteRenderer source, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.color = source.color;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.sharedMaterial = source.sharedMaterial;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = order;
            return sr;
        }

        private void MirrorChildren(Transform rootSource, Transform proxyParent, List<Mirror> list,
            int sortBase, SpriteRenderer skipA, SpriteRenderer skipB)
        {
            if (rootSource == null) return;
            var rootRenderer = rootSource.GetComponent<SpriteRenderer>();
            int rootOrder = rootRenderer != null ? rootRenderer.sortingOrder : 0;
            foreach (var child in rootSource.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (child.transform == rootSource || child == skipA || child == skipB) continue;
                if (!child.enabled || child.sprite == null) continue;
                var sr = CreateSprite("mirror-" + child.name, proxyParent, child,
                    sortBase + Mathf.Clamp(child.sortingOrder - rootOrder, -4, 8));
                sr.transform.localPosition = rootSource.InverseTransformPoint(child.transform.position);
                sr.transform.localRotation = Quaternion.Inverse(rootSource.rotation) * child.transform.rotation;
                sr.transform.localScale = RelativeScale(child.transform, rootSource);
                list.Add(new Mirror { Source = child, Proxy = sr });
            }
        }

        private void LateUpdate()
        {
            if (!built || !body) return;
            Sync(sourceHead, headProxy, baseSort + 2);
            Sync(sourceEyes, eyesProxy, baseSort + 3);
            Sync(sourceNose, noseProxy, baseSort + 4);
            Sync(sourceTorso, torsoProxy, baseSort);
            for (int i = 0; i < headMirrors.Count; i++)
            {
                var m = headMirrors[i];
                Sync(m.Source, m.Proxy, m.Proxy ? m.Proxy.sortingOrder : 0);
            }
            for (int i = 0; i < torsoMirrors.Count; i++)
            {
                var m = torsoMirrors[i];
                Sync(m.Source, m.Proxy, m.Proxy ? m.Proxy.sortingOrder : 0);
            }

            bool visible = sourceHead != null && sourceHead.sprite != null &&
                           (body.limbs == null || body.limbs.Length == 0 || !body.limbs[0].dismembered);
            if (visible != lastVisible)
            {
                lastVisible = visible;
                foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = visible;
            }
        }

        private static void Sync(SpriteRenderer source, SpriteRenderer proxy, int order)
        {
            if (source == null || proxy == null) return;
            if (proxy.sprite != source.sprite) proxy.sprite = source.sprite;
            if (proxy.color != source.color) proxy.color = source.color;
            if (proxy.flipX != source.flipX) proxy.flipX = source.flipX;
            if (proxy.flipY != source.flipY) proxy.flipY = source.flipY;
            if (proxy.sharedMaterial != source.sharedMaterial) proxy.sharedMaterial = source.sharedMaterial;
            proxy.sortingOrder = order;
        }

        public void Teardown()
        {
            built = false;
            foreach (var m in headMirrors) if (m.Proxy) Destroy(m.Proxy.gameObject);
            foreach (var m in torsoMirrors) if (m.Proxy) Destroy(m.Proxy.gameObject);
            headMirrors.Clear();
            torsoMirrors.Clear();
            if (headRoot) Destroy(headRoot.gameObject);
            if (torsoRoot) Destroy(torsoRoot.gameObject);
            headProxy = null; eyesProxy = null; noseProxy = null; torsoProxy = null;
        }

        private void OnDestroy() { Teardown(); }
    }
}
