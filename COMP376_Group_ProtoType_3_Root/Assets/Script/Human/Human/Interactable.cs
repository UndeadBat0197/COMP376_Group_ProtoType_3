using UnityEngine;

/// <summary>
/// Base class for anything the human can interact with (press E).
/// Put a subclass on the object (or on a parent of its collider).
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Name shown in the on-screen prompt. Leave empty to use the GameObject name.")]
    [SerializeField] protected string displayName;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

    /// <summary>Return false to make the interactor ignore this object right now.</summary>
    public virtual bool CanInteract(HumanInteractor who) => isActiveAndEnabled;

    /// <summary>Text after "[E]" in the prompt, e.g. "Pick up Rock".</summary>
    public abstract string GetPrompt(HumanInteractor who);

    public abstract void Interact(HumanInteractor who);
}
