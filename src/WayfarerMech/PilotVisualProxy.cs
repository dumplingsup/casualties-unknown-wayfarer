using System;
using System.Collections.Generic;
using UnityEngine;
using WayfarerMech.Core;

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

        private sealed class SeatedArm
        {
            public Limb[] Limbs = new Limb[3];
            public SpriteRenderer[] Sources = new SpriteRenderer[3], Proxies = new SpriteRenderer[3];
            public Vector2 Shoulder, Wrist;
            public readonly List<Mirror> Clothes = new List<Mirror>();
        }
        private readonly List<SeatedArm> seatedArms = new List<SeatedArm>();
        private Transform handsRoot;
        public int HandCount { get; private set; }
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

        public void AttachSeatedHands()
        {
            if (!built || handsRoot || body.limbs == null || body.limbs.Length < 9) return;
            handsRoot = new GameObject("pilot-seated-arms").transform;
            handsRoot.SetParent(transform, false);
            // Verified native chain: 3/4/5 and 6/7/8 = upper/forearm/hand.
            // Render-only copies; held items, colliders and Body scripts are excluded.
            for (int side = 0; side < 2; side++)
            {
                var arm = new SeatedArm { Shoulder = side == 0 ? new Vector2(-3, 5) : new Vector2(0, 5),
                    Wrist = side == 0 ? new Vector2(4, 7) : new Vector2(11, 7) };
                for (int j = 0; j < 3; j++)
                {
                    var limb = body.limbs[3 + side * 3 + j];
                    arm.Limbs[j] = limb;
                    var source = limb ? limb.GetComponent<SpriteRenderer>() : null;
                    arm.Sources[j] = source;
                    if (!source) continue;
                    arm.Proxies[j] = CreateSprite("seated-arm-" + side + "-" + j, handsRoot,
                        source, side == 0 ? baseSort - 1 : baseSort + 5);
                    MirrorChildren(source.transform, arm.Proxies[j].transform, arm.Clothes,
                        side == 0 ? baseSort - 1 : baseSort + 5, null, null);
                }
                seatedArms.Add(arm);
            }
            UpdateSeatedHands();
            Plugin.LogInfo("PILOT_HANDS_BUILT chains=" + seatedArms.Count);
        }

        private static Vector2 SeatPoint(Vector2 pixels) => new Vector2(pixels.x, -pixels.y) / (MechArt.Ppu * 0.9f);

        private static void FitSegment(SpriteRenderer sr, Vector2 from, Vector2 to)
        {
            if (!sr || !sr.sprite) return;
            var bounds = sr.sprite.bounds;
            bool horizontal = bounds.size.x > bounds.size.y * 1.25f;
            float extent = horizontal ? bounds.size.x : bounds.size.y;
            float scale = Vector2.Distance(from, to) / Mathf.Max(0.01f, extent);
            var rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(
                horizontal ? Vector2.right : Vector2.down, to - from));
            sr.transform.localRotation = rotation;
            sr.transform.localScale = Vector3.one * scale;
            sr.transform.localPosition = (Vector3)((from + to) * 0.5f) - rotation * (bounds.center * scale);
        }

        private void UpdateSeatedHands()
        {
            HandCount = 0;
            foreach (var arm in seatedArms)
            {
                var delta = arm.Wrist - arm.Shoulder;
                var pose = TwoBonePose.Solve(delta.x, delta.y, 6.5, 7, -1);
                Vector2 shoulder = SeatPoint(arm.Shoulder);
                Vector2 elbow = SeatPoint(arm.Shoulder + new Vector2((float)pose.KneeX, (float)pose.KneeY));
                Vector2 wrist = SeatPoint(arm.Wrist);
                bool intact = lastVisible;
                for (int j = 0; j < 3; j++)
                {
                    intact = intact && arm.Limbs[j] && !arm.Limbs[j].dismembered;
                    var target = arm.Proxies[j];
                    Sync(arm.Sources[j], target, target ? target.sortingOrder : 0);
                    if (!target) continue;
                    target.gameObject.SetActive(intact && target.sprite);
                    if (j == 0) FitSegment(target, shoulder, elbow);
                    if (j == 1) FitSegment(target, elbow, wrist);
                    if (j == 2)
                    {
                        FitSegment(target, wrist - Vector2.right * 0.12f, wrist + Vector2.right * 0.12f);
                        if (intact && target.sprite) HandCount++;
                    }
                }
                foreach (var cloth in arm.Clothes)
                    Sync(cloth.Source, cloth.Proxy, cloth.Proxy ? cloth.Proxy.sortingOrder : 0);
            }
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
            UpdateSeatedHands();
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
            seatedArms.Clear();
            if (handsRoot) Destroy(handsRoot.gameObject);
            handsRoot = null;
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
