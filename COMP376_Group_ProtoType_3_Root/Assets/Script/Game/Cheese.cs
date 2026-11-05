using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A piece of cheese the MOUSE can eat (the human can only carry it).
/// Put it on the cheese's root object. Works with or without colliders: reach is measured
/// against the cheese's visible bounds.
///
/// Smell: any SmellZone close to this cheese is "owned" by it. Owned zones follow the cheese
/// if the human carries it somewhere, and switch off once it's eaten. If no zone is nearby,
/// one is created so every cheese can be smelled.
/// </summary>
public class Cheese : MonoBehaviour
{
    public static readonly List<Cheese> All = new List<Cheese>();

    [Tooltip("Points for eating this cheese.")]
    public int points = 100;

    [Tooltip("Give this cheese a smell zone if it doesn't already have one nearby.")]
    public bool createSmellZoneIfMissing = true;
    public float smellRadius = 2.5f;

    public bool IsEaten { get; private set; }

    /// <summary>Raised once when the mouse eats this cheese.</summary>
    public event System.Action<Cheese> Eaten;

    readonly List<Transform> zones = new List<Transform>();
    readonly List<Vector3> zoneOffsets = new List<Vector3>();
    Renderer[] renderers;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        // Cheese is mouse-only: the human can't pick it up, carry it or interact with it at all.
        foreach (Interactable it in GetComponentsInChildren<Interactable>()) it.enabled = false;
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Centre of what you see (the root pivot of prefab cheese is often off to the side).</summary>
    public Vector3 Center => Bounds.center;

    public Bounds Bounds
    {
        get
        {
            bool has = false;
            Bounds b = new Bounds(transform.position, Vector3.one * 0.1f);
            if (renderers != null)
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
            return b;
        }
    }

    public bool Owns(Collider c) => c != null && c.transform.IsChildOf(transform);

    // ------------------------------------------------------------ smell

    public void AdoptSmellZone(Transform zone, bool keepOffset = true)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
        zoneOffsets.Add(keepOffset ? zone.position - Center : Vector3.zero);
    }

    public bool HasSmellZone => zones.Count > 0;
    public IReadOnlyList<Transform> SmellZones => zones;

    /// <summary>Hand this cheese's smell to another one (used when this one gets eaten).</summary>
    public void TransferSmellZonesTo(Cheese other)
    {
        if (other == null || other == this) return;
        foreach (Transform z in zones) other.AdoptSmellZone(z, keepOffset: false);
        zones.Clear();
        zoneOffsets.Clear();
    }

    public void CreateSmellZone()
    {
        if (!createSmellZoneIfMissing || HasSmellZone) return;
        GameObject go = new GameObject(name + " SmellZone (auto)");
        try { go.tag = "SmellZone"; }
        catch (UnityException) { Debug.LogWarning("[Cheese] Tag 'SmellZone' is missing, so this cheese can't be smelled."); }
        go.transform.position = Center;
        SphereCollider sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = smellRadius;
        AdoptSmellZone(go.transform);
    }

    void LateUpdate()
    {
        if (IsEaten) return;
        // Keep the smell with the cheese if the human moved it.
        Vector3 c = Center;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] == null) continue;
            Vector3 want = c + zoneOffsets[i];
            if ((zones[i].position - want).sqrMagnitude > 0.0004f) zones[i].position = want;
        }
    }

    // ------------------------------------------------------------ eating

    public void Eat()
    {
        if (IsEaten) return;
        IsEaten = true;

        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
        foreach (Interactable it in GetComponentsInChildren<Interactable>()) it.enabled = false;

        // Listeners may hand our smell to a nearby cheese first; whatever's left switches off.
        Eaten?.Invoke(this);
        foreach (Transform z in zones) if (z != null) z.gameObject.SetActive(false);
        StartCoroutine(EatAnimation());
    }

    IEnumerator EatAnimation()
    {
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;
        const float duration = 0.35f;
        // Unscaled so the last cheese still finishes animating behind the win screen.
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            // Quick nibble "pop" then shrink away.
            float s = k < 0.25f ? 1f + 0.25f * (k / 0.25f) : 1.25f * (1f - (k - 0.25f) / 0.75f);
            transform.localScale = startScale * Mathf.Max(0f, s);
            transform.position = startPos + Vector3.up * (0.06f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        Destroy(gameObject);
    }
}
