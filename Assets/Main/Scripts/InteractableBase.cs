using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionName = "Interact";

    public string InteractionName => interactionName;

    public virtual bool CanInteract(PlayerInteractor interactor)
    {
        return enabled && gameObject.activeInHierarchy;
    }

    public virtual void OnFocusEnter(PlayerInteractor interactor) { }

    public virtual void OnFocusExit(PlayerInteractor interactor) { }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        PerformInteract(interactor);
    }

    protected abstract void PerformInteract(PlayerInteractor interactor);
}
