public interface IInteractable
{
    string InteractionName { get; }
    bool CanInteract(PlayerInteractor interactor);
    void OnFocusEnter(PlayerInteractor interactor);
    void OnFocusExit(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
