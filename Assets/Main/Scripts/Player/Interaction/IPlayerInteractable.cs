using UnityEngine;

public interface IPlayerInteractable
{
    bool CanInteract(PlayerController player);
    Vector3 GetInteractionPoint();
    void Interact(PlayerController player);
}
