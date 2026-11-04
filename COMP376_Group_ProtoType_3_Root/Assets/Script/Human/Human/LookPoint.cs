using System.Collections;
using UnityEngine;

/// <summary>
/// "Look into the top shelf." Put this on a trigger collider covering the shelf area.
/// Interacting switches the view to a fixed camera aimed at the shelf, frees the cursor so the
/// player can click an item there, and freezes movement. Q / Esc / right-click steps back.
///
/// Camera options (use one):
///  - virtualCamera: a DISABLED GameObject with a CinemachineCamera on it, posed at the view.
///    Enabling it makes Cinemachine blend to it; disabling blends back to the FreeLook.
///  - viewPoint: just a Transform. Used when there's no Cinemachine camera; temporarily
///    disables the CinemachineBrain and moves Camera.main there.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LookPoint : Interactable
{
    public GameObject virtualCamera;
    public Transform viewPoint;

    [Tooltip("Optional: only usable while this door/drawer is open.")]
    public Openable requiresOpen;

    public string verb = "Look at";
    public float fallbackMoveSpeed = 4f;

    public bool IsActive => viewer != null;

    HumanInteractor viewer;
    Behaviour disabledBrain;
    Vector3 savedCamPos;
    Quaternion savedCamRot;
    Coroutine fallbackRoutine;

    void Awake()
    {
        if (virtualCamera != null) virtualCamera.SetActive(false);
    }

    public override bool CanInteract(HumanInteractor who)
    {
        if (!base.CanInteract(who) || IsActive) return false;
        if (requiresOpen != null && !requiresOpen.IsOpen) return false;
        return virtualCamera != null || viewPoint != null;
    }

    public override string GetPrompt(HumanInteractor who) => $"{verb} {DisplayName}";

    public override void Interact(HumanInteractor who) => Enter(who);

    public void Enter(HumanInteractor who)
    {
        if (IsActive) return;
        viewer = who;
        who.BeginLook(this);

        if (virtualCamera != null)
        {
            virtualCamera.SetActive(true);
        }
        else if (viewPoint != null && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            savedCamPos = cam.position;
            savedCamRot = cam.rotation;
            disabledBrain = FindBrain(Camera.main.gameObject);
            if (disabledBrain != null) disabledBrain.enabled = false;
            if (fallbackRoutine != null) StopCoroutine(fallbackRoutine);
            fallbackRoutine = StartCoroutine(MoveCamera(cam, viewPoint.position, viewPoint.rotation, null));
        }
    }

    public void Exit(HumanInteractor who)
    {
        if (viewer != who || viewer == null) return;
        viewer = null;

        if (virtualCamera != null)
        {
            virtualCamera.SetActive(false);
        }
        else if (Camera.main != null)
        {
            if (fallbackRoutine != null) StopCoroutine(fallbackRoutine);
            Behaviour brain = disabledBrain;
            disabledBrain = null;
            if (isActiveAndEnabled)
                fallbackRoutine = StartCoroutine(MoveCamera(Camera.main.transform, savedCamPos, savedCamRot, brain));
            else if (brain != null)
                brain.enabled = true;
        }

        who.EndLook(this);
    }

    void Update()
    {
        // Door got closed (or the player transformed) while looking -> step back.
        if (viewer != null && ((requiresOpen != null && !requiresOpen.IsOpen) || !viewer.isActiveAndEnabled))
            Exit(viewer);
    }

    IEnumerator MoveCamera(Transform cam, Vector3 pos, Quaternion rot, Behaviour enableWhenDone)
    {
        while ((cam.position - pos).sqrMagnitude > 0.0001f || Quaternion.Angle(cam.rotation, rot) > 0.1f)
        {
            float k = 1f - Mathf.Exp(-fallbackMoveSpeed * Time.deltaTime);
            cam.position = Vector3.Lerp(cam.position, pos, k);
            cam.rotation = Quaternion.Slerp(cam.rotation, rot, k);
            yield return null;
        }
        cam.SetPositionAndRotation(pos, rot);
        if (enableWhenDone != null) enableWhenDone.enabled = true;
        fallbackRoutine = null;
    }

    static Behaviour FindBrain(GameObject cam)
    {
        foreach (Behaviour b in cam.GetComponents<Behaviour>())
            if (b != null && b.GetType().Name == "CinemachineBrain") return b;
        return null;
    }

    void OnDisable()
    {
        if (viewer != null) Exit(viewer);
    }
}
