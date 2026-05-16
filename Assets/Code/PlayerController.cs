using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviourPun
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        // Only control our own player and not bots
        if (!photonView.IsMine || gameObject.name.Contains("Dummy")) return;

        // Block movement if a sub-menu is open
        if (Player_UI.Instance != null && Player_UI.Instance.IsSubMenuOpen) return;
        
        // Optional: Block if mouse is over UI (useful for clicking/scrolling)
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        if (Keyboard.current == null) return;

        float moveX = 0;
        float moveY = 0;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY = 1;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY = -1;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX = -1;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX = 1;

        Vector3 move = new Vector3(moveX, moveY, 0).normalized;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
