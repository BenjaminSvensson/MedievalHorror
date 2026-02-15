using UnityEngine;

public class InteractableTarget : MonoBehaviour
{
    [SerializeField] private MonoBehaviour interactableSource;

    private IInteractable cachedInteractable;

    private void Awake()
    {
        CacheInteractable();
    }

    private void OnValidate()
    {
        CacheInteractable();
    }

    public void SetInteractable(IInteractable interactable)
    {
        cachedInteractable = interactable;
        interactableSource = interactable as MonoBehaviour;
    }

    public bool TryGetInteractable(out IInteractable interactable)
    {
        if (cachedInteractable == null)
        {
            CacheInteractable();
        }

        interactable = cachedInteractable;
        return interactable != null;
    }

    private void CacheInteractable()
    {
        if (interactableSource is IInteractable interactable)
        {
            cachedInteractable = interactable;
        }
        else
        {
            cachedInteractable = GetComponentInParent<IInteractable>();
        }
    }
}
