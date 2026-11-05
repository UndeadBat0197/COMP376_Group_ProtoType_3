using UnityEngine;

/// <summary>
/// Lets the mouse eat cheese: walk up to a cheese and press E.
/// Added to the mouse automatically by CheeseGameManager, so the mouse prefab doesn't need changing.
/// Cheese you can't actually reach (under the boulder, behind furniture) is reported as Blocked,
/// so the HUD can hint that the human is needed.
/// </summary>
public class MouseCheeseEater : MonoBehaviour
{
    public KeyCode eatKey = KeyCode.E;
    [Tooltip("How close the mouse's body has to be to the cheese to eat it.")]
    public float eatRange = 0.5f;
    [Tooltip("Within this distance a cheese you can't reach shows the 'something's in the way' hint.")]
    public float hintRange = 1.4f;

    /// <summary>Cheese you can eat right now (null if none).</summary>
    public Cheese Target { get; private set; }
    /// <summary>Closest cheese nearby that's blocked by something (null if none).</summary>
    public Cheese Blocked { get; private set; }

    Collider[] own;

    void Awake() { own = GetComponentsInChildren<Collider>(true); }

    void OnDisable() { Target = null; Blocked = null; }

    void Update()
    {
        if (GameState.InputBlocked) return;
        Scan();
        if (Target != null && Input.GetKeyDown(eatKey)) Target.Eat();
    }

    /// <summary>The middle of the mouse's body (its root pivot isn't on the body).</summary>
    public Vector3 Body
    {
        get
        {
            foreach (Collider c in own)
                if (c != null && c.enabled && !c.isTrigger) return c.bounds.center;
            return transform.position;
        }
    }

    void Scan()
    {
        Target = null;
        Blocked = null;
        Vector3 body = Body;
        float bestEat = float.MaxValue, bestBlocked = float.MaxValue;

        foreach (Cheese cheese in Cheese.All)
        {
            if (cheese == null || cheese.IsEaten) continue;
            Liftable lift = cheese.GetComponent<Liftable>();
            if (lift != null && lift.IsHeld) continue;

            Bounds b = cheese.Bounds;
            float dist = Vector3.Distance(body, b.ClosestPoint(body));
            if (dist > hintRange) continue;

            bool reachable = dist <= eatRange && ClearPath(body, b.center, cheese);
            if (reachable)
            {
                if (dist < bestEat) { bestEat = dist; Target = cheese; }
            }
            else if (dist < bestBlocked && !ClearPath(body, b.center, cheese))
            {
                bestBlocked = dist;
                Blocked = cheese;
            }
        }
        if (Target != null) Blocked = null;
    }

    bool ClearPath(Vector3 from, Vector3 to, Cheese cheese)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        foreach (RaycastHit h in Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore))
        {
            if (cheese.Owns(h.collider)) continue;
            if (IsOwn(h.collider)) continue;
            return false;
        }
        return true;
    }

    bool IsOwn(Collider c)
    {
        foreach (Collider o in own) if (o == c) return true;
        return c.transform.IsChildOf(transform);
    }
}
