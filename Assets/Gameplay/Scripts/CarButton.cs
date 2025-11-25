using UnityEngine;
using UnityEngine.EventSystems;

public class CarButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum ButtonType
    {
        Left,
        Right,
        Forward,
        Reverse
    }

    public ButtonType buttonType;
    public SimpleCarController car;

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetPressed(false);
    }

    private void SetPressed(bool pressed)
    {
        if (car == null) return;
        car.SendInput(buttonType, pressed);
    }
}