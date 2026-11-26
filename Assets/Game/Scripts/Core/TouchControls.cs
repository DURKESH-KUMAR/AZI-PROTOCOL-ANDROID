using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Anything that provides a stick value in [-1,1] (UI Toolkit stick or the old FixedJoystick).</summary>
public interface IStickInput
{
    Vector2 Value { get; }
}

/// <summary>
/// Virtual joystick for UI Toolkit. Each stick tracks its own touch (pointerId), so moving,
/// looking and firing work at the same time. A "floating" stick jumps to wherever the thumb
/// lands inside its zone and returns home on release (more forgiving on different phone sizes).
/// </summary>
public sealed class TouchStick : IStickInput
{
    readonly VisualElement zone, stick, knob;
    readonly bool floating;
    int pointerId = -1;
    Vector2 center;

    public Vector2 Value { get; private set; }
    public bool IsHeld => pointerId != -1;

    public TouchStick(VisualElement zone, VisualElement stick, VisualElement knob, bool floating)
    {
        this.zone = zone;
        this.stick = stick;
        this.knob = knob;
        this.floating = floating;
        zone.RegisterCallback<PointerDownEvent>(OnDown);
        zone.RegisterCallback<PointerMoveEvent>(OnMove);
        zone.RegisterCallback<PointerUpEvent>(OnUp);
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

        Vector2 local = e.localPosition;
        if (floating)
        {
            float half = stick.resolvedStyle.width * 0.5f;
            Rect bounds = zone.contentRect;
            local.x = Mathf.Clamp(local.x, half, bounds.width - half);
            local.y = Mathf.Clamp(local.y, half, bounds.height - half);
            stick.style.left = local.x - half;
            stick.style.top = local.y - half;
            stick.style.bottom = StyleKeyword.Auto;
            center = local;
        }
        else
        {
            center = stick.layout.center;
        }
        Track(e.localPosition);
        e.StopPropagation();
    }

    void OnMove(PointerMoveEvent e)
    {
        if (e.pointerId == pointerId)
            Track(e.localPosition);
    }

    void OnUp(PointerUpEvent e)
    {
        if (e.pointerId == pointerId)
            Release();
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
        if (floating)
        {
            // Back to the USS home position.
            stick.style.left = StyleKeyword.Null;
            stick.style.top = StyleKeyword.Null;
            stick.style.bottom = StyleKeyword.Null;
        }
    }
}

/// <summary>
/// Touch button for UI Toolkit with press / release callbacks (hold actions like fire and
/// sprint) and its own pointer capture, so it works alongside the sticks.
/// </summary>
public sealed class TouchButton
{
    readonly VisualElement element;
    readonly Action onPress, onRelease;
    int pointerId = -1;

    public bool IsHeld => pointerId != -1;

    public TouchButton(VisualElement element, Action onPress, Action onRelease = null)
    {
        this.element = element;
        this.onPress = onPress;
        this.onRelease = onRelease;
        element.RegisterCallback<PointerDownEvent>(e =>
        {
            if (pointerId != -1)
                return;
            pointerId = e.pointerId;
            element.CapturePointer(pointerId);
            element.AddToClassList("action--pressed");
            onPress?.Invoke();
            e.StopPropagation();
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
