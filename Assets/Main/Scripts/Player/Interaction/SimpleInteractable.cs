using UnityEngine;
using UnityEngine.Events;

public class SimpleInteractable : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private bool disableAfterInteraction;
    [SerializeField] private UnityEvent onInteracted;

    public bool CanInteract(PlayerController player)
    {
        return enabled && gameObject.activeInHierarchy;
    }

    public Vector3 GetInteractionPoint()
    {
        if (interactionPoint != null)
        {
            return interactionPoint.position;
        }

        if (TryGetComponent(out Collider ownCollider))
        {
            return ownCollider.bounds.center;
        }

        return transform.position;
    }

    public void Interact(PlayerController player)
    {
        onInteracted?.Invoke();

        if (disableAfterInteraction)
        {
            enabled = false;
        }
    }
}
