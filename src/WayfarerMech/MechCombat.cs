using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WayfarerMech.Core;

namespace WayfarerMech
{
    public struct MechHit
    {
        public bool hit;
        public bool wall;
        public Vector2 point;
        public Vector2 normal;
        public Collider2D collider;
        public BuildingEntity building;
        public Body body;
        public Limb limb;
        public string kind;
    }

    /// <summary>
    /// Target-game combat/mining adapter for the two mech arms. It uses the game's
    /// native block, building, animal and limb damage fields, but keeps an explicit
    /// self/friendly filter because TurretScript.Shoot(ignoreBody=false) would damage
    /// the pilot and every other body along the ray.
    /// </summary>
    public static class MechCombat
    {
        private static readonly int HitMask = LayerMask.GetMask("Ground", "Body", "Limb", "Descriptor");
        private static readonly int GroundMask = LayerMask.GetMask("Ground");
        private static readonly RaycastHit2D[] Buffer = new RaycastHit2D[96];

        public static bool IsFriendly(Body target, Body pilot)
        {
            if (target == null) return true;
            if (target == pilot) return true;
            if (target.CompareTag("Player")) return true;
            if (target.CompareTag("Scientist")) return true;
            if (target.GetComponent<TraderScript>() != null) return true;
            return false;
        }

        public static bool ResolveMuzzle(Vector2 pivot, Vector2 tip, out Vector2 origin, out bool blocked)
        {
            blocked = false;
            origin = tip;
            if (Vector2.Distance(pivot, tip) < 0.05f) return true;
            var hit = Physics2D.Linecast(pivot, tip, GroundMask);
            if (hit)
            {
                blocked = true;
                origin = hit.point - (tip - pivot).normalized * 0.08f;
                return false;
            }
            if (Physics2D.OverlapPoint(tip, GroundMask))
            {
                blocked = true;
                origin = tip;
                return false;
            }
            return true;
        }

        public static MechHit Cast(Vector2 origin, Vector2 dir, float range, Transform ignoreRoot, Body pilot)
        {
            var result = new MechHit { point = origin + dir * range, kind = "none" };
            var list = Physics2D.RaycastAll(origin, dir, range, HitMask).ToList();
            list.Sort((a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in list)
            {
                if (!h.collider) continue;
                if (ignoreRoot && (h.transform == ignoreRoot || h.transform.IsChildOf(ignoreRoot))) continue;
                if (h.collider.isTrigger) continue;
                int layer = h.collider.gameObject.layer;
                // Some native creatures (for example shadecrawler) use a Ground-layer
                // collider. Check the entity owner before classifying the hit as terrain,
                // otherwise animal damage silently becomes block damage.
                var building = h.collider.GetComponentInParent<BuildingEntity>();
                if (building)
                {
                    result.hit = true; result.point = h.point; result.normal = h.normal; result.collider = h.collider;
                    result.building = building; result.kind = building.animal ? "animal" : "building";
                    result.wall = layer == LayerMask.NameToLayer("Ground") && !building.animal;
                    return result;
                }
                if (layer == LayerMask.NameToLayer("Ground"))
                {
                    result.hit = true; result.wall = true; result.point = h.point; result.normal = h.normal;
                    result.collider = h.collider; result.kind = "terrain";
                    return result;
                }
                var limb = h.collider.GetComponentInParent<Limb>();
                Body body = limb != null ? limb.body : h.collider.GetComponentInParent<Body>();
                if (body)
                {
                    result.hit = true; result.point = h.point; result.normal = h.normal; result.collider = h.collider;
                    result.body = body; result.limb = limb;
                    result.kind = IsFriendly(body, pilot) ? "friendly" : "body";
                    return result;
                }
                // Descriptor or unknown collider: still stop the ray, but do not damage it.
                result.hit = true; result.point = h.point; result.normal = h.normal; result.collider = h.collider;
                result.kind = "descriptor";
                return result;
            }
            return result;
        }

        public static MechHit Cannon(Vector2 pivot, Vector2 tip, Vector2 aim, float range, Body pilot,
            Transform ignoreRoot, float structureDamage, float animalDamage, float bodyDamage)
        {
            Vector2 dir = aim - tip;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
            dir.Normalize();
            ResolveMuzzle(pivot, tip, out var origin, out bool blocked);
            var hit = Cast(origin, dir, blocked ? 1.2f : range, ignoreRoot, pilot);
            if (hit.hit)
            {
                if (hit.building != null) DamageBuilding(hit, structureDamage, animalDamage);
                else if (hit.body != null && !IsFriendly(hit.body, pilot)) DamageBody(hit, bodyDamage);
                else if (hit.wall) DamageTerrain(hit, structureDamage);
            }
            Tracer(origin, hit.hit ? hit.point : origin + dir * Mathf.Min(range, 30f));
            if (Plugin.Mech) Plugin.Mech.NoteShot(hit);
            return hit;
        }

        public static MechHit Beam(Vector2 pivot, Vector2 tip, Vector2 aim, float range, Body pilot,
            Transform ignoreRoot, float terrainDamage, float entityDamage, out float length)
        {
            Vector2 dir = aim - tip;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
            dir.Normalize();
            ResolveMuzzle(pivot, tip, out var origin, out bool blocked);
            var hit = Cast(origin, dir, blocked ? 1.2f : range, ignoreRoot, pilot);
            length = Vector2.Distance(origin, hit.hit ? hit.point : origin + dir * range);
            if (hit.hit)
            {
                if (hit.building != null) DamageBuilding(hit, entityDamage, entityDamage);
                else if (hit.body != null && !IsFriendly(hit.body, pilot)) DamageBody(hit, entityDamage);
                else if (hit.wall) DamageTerrain(hit, terrainDamage);
            }
            if (Plugin.Mech) Plugin.Mech.NoteShot(hit);
            return hit;
        }

        public static void DamageBuilding(MechHit hit, float structureDamage, float animalDamage)
        {
            var b = hit.building;
            if (!b || b.cantHit) return;
            float dmg = b.animal ? animalDamage : structureDamage;
            b.health -= dmg;
            if (b.animal)
            {
                b.SendMessage("AnimalHit", dmg, SendMessageOptions.DontRequireReceiver);
                Sound.Play("gore" + UnityEngine.Random.Range(1, 6), hit.point);
            }
            WorldGeneration.CreateDamageNumber(hit.point, (int)dmg);
            if (Plugin.Mech) Plugin.Mech.NoteTargetHit(hit.kind, hit.point, dmg);
        }

        public static void DamageBody(MechHit hit, float damage)
        {
            var target = hit.body;
            if (!target) return;
            var limb = hit.limb ?? target.GetClosestLimb(hit.point);
            if (limb == null) return;
            float armor = Mathf.Max(0.1f, limb.GetArmorReduction());
            limb.pain += damage * 1.1f / armor;
            limb.skinHealth -= damage * 0.85f / armor;
            limb.muscleHealth -= damage * 0.65f / armor;
            limb.bleedAmount += damage * 0.12f / armor;
            target.adrenaline = Mathf.Max(target.adrenaline, 60f);
            target.shock += damage * 0.08f / armor;
            if (target.rb)
            {
                target.rb.velocity += (Vector2)(hit.point - (Vector2)target.transform.position).normalized * 3f;
            }
            WorldGeneration.CreateDamageNumber(hit.point, (int)damage);
            target.DoGoreSound();
            if (damage >= 100f) target.Ragdoll();
            if (Plugin.Mech) Plugin.Mech.NoteTargetHit("body", hit.point, damage);
            Plugin.LogInfo("MECH_HIT_BODY target=" + target.GetInstanceID() + " limb=" + limb.shortName +
                           " damage=" + damage + " skin=" + limb.skinHealth.ToString("0.0") +
                           " muscle=" + limb.muscleHealth.ToString("0.0"));
        }

        public static void DamageTerrain(MechHit hit, float damage)
        {
            var w = WorldGeneration.world;
            if (!w || !w.worldExists) return;
            var cell = w.WorldToBlockPos(hit.point + (Vector2)hit.normal * 0.15f);
            int id = w.GetBlock(cell);
            if (id == 0) return;
            var info = w.GetBlockInfo((ushort)id);
            if (info == null || info.health <= 0f || info.health >= DrillShape.IndestructibleHealth) return;
            w.DamageBlock(cell, damage, hitSound: true, bonusMetal: false, ignoreLoot: false);
            WorldGeneration.CreateDamageNumber(hit.point, (int)damage);
            if (Plugin.Mech) Plugin.Mech.NoteBreak(cell, id, info);
        }

        public static void Tracer(Vector2 from, Vector2 to)
        {
            try
            {
                var prefab = Resources.Load<GameObject>("Special/TurretLine");
                if (prefab != null)
                {
                    var obj = UnityEngine.Object.Instantiate(prefab, Vector2.zero, Quaternion.identity);
                    var line = obj.GetComponent<LineRenderer>();
                    if (line)
                    {
                        line.SetPosition(0, from);
                        line.SetPosition(1, to);
                    }
                    UnityEngine.Object.Destroy(obj, 0.05f);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("MECH_TRACER_EXCEPTION " + ex.Message);
            }
        }
    }
}
