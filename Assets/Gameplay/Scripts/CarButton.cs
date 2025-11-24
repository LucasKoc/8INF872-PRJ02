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

        switch (buttonType)
        {
            case ButtonType.Left:
                car.pressingLeft = pressed;
                break;
            case ButtonType.Right:
                car.pressingRight = pressed;
                break;
            case ButtonType.Forward:
                car.pressingForward = pressed;
                break;
            case ButtonType.Reverse:
                car.pressingReverse = pressed;
                break;
        }
    }
}