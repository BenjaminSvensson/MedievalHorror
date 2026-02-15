using UnityEngine;

public class ToggleActiveInteractable : InteractableBase
{
    [SerializeField] private GameObject targetObject;

    protected override void PerformInteract(PlayerInteractor interactor)
    {
        GameObject target = targetObject != null ? targetObject : gameObject;
        target.SetActive(!target.activeSelf);
    }
}
