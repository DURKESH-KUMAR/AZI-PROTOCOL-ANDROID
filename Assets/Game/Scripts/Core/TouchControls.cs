using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Anything that provides a stick value in [-1,1] (UI Toolkit stick or the old FixedJoystick).</summary>
public interface IStickInput
{
    Vector2 Value { get; }
}

/// <summary>Anything that provides camera-look drag since the last frame (panel units, +y = up).</summary>
public interface ILookInput
{
    Vector2 ConsumeDelta();
}

/// <summary>
/// Fixed virtual joystick for UI Toolkit (left thumb, movement). The stick never moves; a touch
/// anywhere inside its (larger) activation zone steers relative to the stick's centre, so it is
/// easy to grab without looking. Each control tracks its own pointerId, so moving, looking and
/// firing work at the same time.
/// </summary>
public sealed class TouchStick : IStickInput
{
    readonly VisualElement zone, stick, knob;
    int pointerId = -1;
    Vector2 center;

    public Vector2 Value { get; private set; }
    public bool IsHeld => pointerId != -1;

    public TouchStick(VisualElement zone, VisualElement stick, VisualElement knob)
    {
        this.zone = zone;
        this.stick = stick;
        this.knob = knob;
        zone.RegisterCallback<PointerDownEvent>(OnDown);
        zone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == pointerId) Track(e.localPosition); });
        zone.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointerId) Release(); });
        zone.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointerId) Release(); });
        zone.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pointerId) Release(); });
    }

    void OnDown(PointerDownEvent e)
    {
        if (pointerId != -1)
            return;
        pointerId = e.pointerId;
        zone.CapturePointer(pointerId);
        stick.AddToClassList("stick--active");
        center = stick.layout.center; // stick is a child of the zone: same local space
        Track(e.localPosition);
        e.StopPropagation();
    }

    void Track(Vector2 local)
    {
        float radius = Mathf.Max(1f, stick.resolvedStyle.width * 0.5f);
        Vector2 delta = Vector2.ClampMagnitude(local - center, radius);
        knob.style.translate = new Translate(delta.x, delta.y);
        Value = new Vector2(delta.x / radius, -delta.y / radius); // UI y grows downwards
    }

    /// <summary>Let go (touch ended, death, pause).</summary>
    public void Release()
    {
        int id = pointerId;
        pointerId = -1; // first, so the capture-out event raised below is ignored
        if (id != -1 && zone.HasPointerCapture(id))
            zone.ReleasePointer(id);
        Value = Vector2.zero;
        knob.style.translate = new Translate(0, 0);
        stick.RemoveFromClassList("stick--active");
    }
}

/// <summary>
/// Touch-and-drag free look (right thumb). Drag anywhere in the zone to turn the camera; the
/// finger's movement is accumulated and consumed once per frame by InputManager, so the
/// result is exact and frame-rate independent (unlike a joystick, which sets a turn RATE).
/// Other controls (e.g. the fire button) can feed drags in too via <see cref="AddDelta"/>.
/// </summary>
public sealed class TouchLook : ILookInput
{
    readonly VisualElement zone;
    int pointerId = -1;
    Vector2 last;
    Vector2 accumulated;

    public bool IsHeld => pointerId != -1;

    /// <summary>Raised the first time the player drags (to hide the "drag to look" hint).</summary>
    public event Action FirstDrag;

    public TouchLook(VisualElement zone)
    {
        this.zone = zone;
        zone.RegisterCallback<PointerDownEvent>(e =>
        {
            if (pointerId != -1)
                return;
            pointerId = e.pointerId;
            last = e.position;
            zone.CapturePointer(pointerId);
            zone.AddToClassList("look-zone--active");
            e.StopPropagation();
        });
        zone.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (e.pointerId != pointerId)
                return;
            Vector2 p = e.position;
            AddDelta(p - last);
            last = p;
        });
        zone.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointerId) Release(); });
        zone.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointerId) Release(); });
        zone.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pointerId) Release(); });
    }

    /// <summary>Adds a drag in panel units (UI y down).</summary>
    public void AddDelta(Vector2 uiDelta)
    {
        if (uiDelta == Vector2.zero)
            return;
        accumulated += uiDelta;
        if (FirstDrag != null && uiDelta.sqrMagnitude > 4f)
        {
            FirstDrag.Invoke();
            FirstDrag = null;
        }
    }

    /// <summary>Drag since the last call, +y = up. Called once per frame by InputManager.</summary>
    public Vector2 ConsumeDelta()
    {
        Vector2 d = accumulated;
        accumulated = Vector2.zero;
        return new Vector2(d.x, -d.y);
    }

    public void Release()
    {
        int id = pointerId;
        pointerId = -1;
        if (id != -1 && zone.HasPointerCapture(id))
            zone.ReleasePointer(id);
        accumulated = Vector2.zero;
        zone.RemoveFromClassList("look-zone--active");
    }
}

/// <summary>
/// Touch button for UI Toolkit with press / release callbacks (hold actions like fire) and its
/// own pointer capture, so it works alongside the stick and the look zone. With
/// <c>onDrag</c>, sliding the finger while holding reports the movement (fire + aim).
/// </summary>
public sealed class TouchButton
{
    readonly VisualElement element;
    readonly Action onPress, onRelease;
    readonly Action<Vector2> onDrag;
    int pointerId = -1;
    Vector2 last;

    public bool IsHeld => pointerId != -1;

    public TouchButton(VisualElement element, Action onPress, Action onRelease = null, Action<Vector2> onDrag = null)
    {
        this.element = element;
        this.onPress = onPress;
        this.onRelease = onRelease;
        this.onDrag = onDrag;
        element.RegisterCallback<PointerDownEvent>(e =>
        {
            if (pointerId != -1)
                return;
            pointerId = e.pointerId;
            last = e.position;
            element.CapturePointer(pointerId);
            element.AddToClassList("action--pressed");
            onPress?.Invoke();
            e.StopPropagation();
        });
        element.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (e.pointerId != pointerId || onDrag == null)
                return;
            Vector2 p = e.position;
            onDrag(p - last);
            last = p;
        });
        element.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointerId) Release(); });
        element.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointerId) Release(); });
        element.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pointerId) Release(); });
    }

    public void Release()
    {
        if (pointerId == -1)
            return;
        int id = pointerId;
        pointerId = -1; // first, so the capture-out event raised below is ignored
        if (element.HasPointerCapture(id))
            element.ReleasePointer(id);
        element.RemoveFromClassList("action--pressed");
        onRelease?.Invoke();
    }
}
