using UnityEngine;
using UnityEngine.EventSystems;

// ATTACH THIS TO: a UI Image (the joystick's outer "Background" circle).
// Also drag a child UI Image (the inner "Handle" circle) into the Handle slot.
// You'll make two of these total: one for movement, one for aiming.
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;

    // How far the stick is pushed, from -1 to 1 on each axis.
    public Vector2 InputVector { get; private set; }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out position);

        position.x = (position.x / background.sizeDelta.x) * 2f;
        position.y = (position.y / background.sizeDelta.y) * 2f;

        Vector2 clamped = position.magnitude > 1f ? position.normalized : position;

        handle.anchoredPosition = new Vector2(
            clamped.x * (background.sizeDelta.x / 2f),
            clamped.y * (background.sizeDelta.y / 2f));

        InputVector = clamped;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}
