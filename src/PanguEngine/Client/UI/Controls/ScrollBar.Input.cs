using PanguEngine.Client.UI.Input;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

public sealed partial class ScrollBar
{
    private const int InitialRepeatDelayMilliseconds = 400;
    private const int RepeatIntervalMilliseconds = 50;

    private readonly UiTicker _repeatTicker;
    private InteractionKind _interaction;
    private double _thumbDragStartPointer;
    private double _thumbDragStartValue;
    private bool _isThumbDragRebasePending;
    private int _trackDirection;
    private Point _lastPointerPosition;
    private bool _hasPointerPosition;
    private bool _hasRepeatBaseline;
    private TimeSpan _lastRepeatTime;
    private TimeSpan _repeatAccumulated;
    private TimeSpan _nextRepeatDelay;

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button != MouseButton.Left ||
            !IsEnabled ||
            eventArgs.Handled ||
            eventArgs.Source is not ScrollBarPart part)
        {
            return;
        }

        var position = eventArgs.GetPosition(this);
        _lastPointerPosition = position;
        _hasPointerPosition = true;

        switch (part.Kind)
        {
            case ScrollBarPartKind.Thumb:
                if (!TryStartThumbDrag(position))
                    return;
                break;
            case ScrollBarPartKind.Track:
                StartTrackRepeat(position);
                break;
            case ScrollBarPartKind.Decrease:
                Step(-SmallChange);
                if (part.IsEnabled)
                    StartRepeat(InteractionKind.RepeatDecrease, 0);
                break;
            case ScrollBarPartKind.Increase:
                Step(SmallChange);
                if (part.IsEnabled)
                    StartRepeat(InteractionKind.RepeatIncrease, 0);
                break;
        }

        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(UiPointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (_interaction == InteractionKind.None)
            return;

        if (!IsInteractionAvailable())
        {
            CancelInteraction();
            return;
        }

        var position = eventArgs.GetPosition(this);
        _lastPointerPosition = position;
        _hasPointerPosition = true;
        if (_interaction == InteractionKind.DragThumb)
            UpdateThumbDrag(position);
        else if (!CanRepeat())
            ResetRepeatDelay();
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        if (_interaction != InteractionKind.None)
            CancelInteraction();
        if (eventArgs.Source is ScrollBarPart)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCanceled(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerCanceled(eventArgs);
        if (eventArgs.Button != MouseButton.Left || _interaction == InteractionKind.None)
            return;

        CancelInteraction();
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(UiPointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheel(eventArgs);
        if (!IsWheelScrollingEnabled || !IsEnabled || eventArgs.Handled)
            return;

        var delta = GetWheelDelta(eventArgs);
        if (delta == 0)
            return;

        var previous = Value;
        Step(-delta * 3 * SmallChange);
        eventArgs.Handled = Value != previous;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (!IsFocused ||
            eventArgs.Handled ||
            eventArgs.Modifiers != KeyModifiers.None)
        {
            return;
        }

        switch (eventArgs.Key)
        {
            case Key.Up when Orientation == Orientation.Vertical:
                Step(-SmallChange);
                eventArgs.Handled = true;
                return;
            case Key.Down when Orientation == Orientation.Vertical:
                Step(SmallChange);
                eventArgs.Handled = true;
                return;
            case Key.Left when Orientation == Orientation.Horizontal:
                Step(-SmallChange);
                eventArgs.Handled = true;
                return;
            case Key.Right when Orientation == Orientation.Horizontal:
                Step(SmallChange);
                eventArgs.Handled = true;
                return;
            case Key.PageUp:
                Step(-LargeChange);
                eventArgs.Handled = true;
                return;
            case Key.PageDown:
                Step(LargeChange);
                eventArgs.Handled = true;
                return;
            case Key.Home:
                Value = Minimum;
                eventArgs.Handled = true;
                return;
            case Key.End:
                Value = Maximum;
                eventArgs.Handled = true;
                return;
        }
    }

    /// <summary>
    /// Cancels any in-flight drag or repeat and stops the repeat ticker.
    /// </summary>
    internal void CancelInteraction()
    {
        if (_interaction == InteractionKind.None)
            return;

        _interaction = InteractionKind.None;
        _thumbDragStartPointer = 0;
        _thumbDragStartValue = 0;
        _isThumbDragRebasePending = false;
        _trackDirection = 0;
        _hasPointerPosition = false;
        ResetRepeatDelay();
        if (_repeatTicker.IsActive)
            _repeatTicker.Stop();
    }

    private bool TryStartThumbDrag(Point position)
    {
        var track = _track.LayoutBounds;
        var thumb = _thumb.LayoutBounds;
        var travel = GetMain(track, Orientation) - GetMain(thumb, Orientation);
        if (travel <= 0)
            return false;

        _interaction = InteractionKind.DragThumb;
        _thumbDragStartPointer = GetMainPoint(position, Orientation);
        _thumbDragStartValue = Value;
        return true;
    }

    private void StartTrackRepeat(Point position)
    {
        var thumb = _thumb.LayoutBounds;
        var main = GetMainPoint(position, Orientation);
        _trackDirection = main < GetMainOrigin(thumb, Orientation) ? -1 : 1;
        Step(LargeChange * _trackDirection);
        StartRepeat(InteractionKind.RepeatTrack, _trackDirection);
    }

    private void StartRepeat(InteractionKind interaction, int trackDirection)
    {
        _interaction = interaction;
        _trackDirection = trackDirection;
        ResetRepeatDelay();
        if (!_repeatTicker.IsActive)
            _repeatTicker.Start();
    }

    private void UpdateThumbDrag(Point position)
    {
        var track = _track.LayoutBounds;
        var thumb = _thumb.LayoutBounds;
        var travel = GetMain(track, Orientation) - GetMain(thumb, Orientation);
        if (travel <= 0)
            return;

        var delta = GetMainPoint(position, Orientation) - _thumbDragStartPointer;
        Value = _thumbDragStartValue + delta / travel * Range;
    }

    private void OnRepeatTick(TimeSpan frameTime)
    {
        if (_interaction == InteractionKind.None)
            return;

        if (!IsInteractionAvailable())
        {
            CancelInteraction();
            return;
        }

        if (!CanRepeat())
        {
            ResetRepeatDelay();
            return;
        }

        if (!_hasRepeatBaseline)
        {
            _hasRepeatBaseline = true;
            _lastRepeatTime = frameTime;
            return;
        }

        var delta = frameTime - _lastRepeatTime;
        _lastRepeatTime = frameTime;
        if (delta <= TimeSpan.Zero)
            return;

        _repeatAccumulated += delta;
        if (_repeatAccumulated < _nextRepeatDelay)
            return;

        _repeatAccumulated = TimeSpan.Zero;
        _nextRepeatDelay = TimeSpan.FromMilliseconds(RepeatIntervalMilliseconds);
        RepeatInteraction();
    }

    private void RepeatInteraction()
    {
        switch (_interaction)
        {
            case InteractionKind.None:
            case InteractionKind.DragThumb:
                return;
            case InteractionKind.RepeatDecrease:
                Step(-SmallChange);
                break;
            case InteractionKind.RepeatIncrease:
                Step(SmallChange);
                break;
            case InteractionKind.RepeatTrack:
                Step(LargeChange * _trackDirection);
                break;
        }
    }

    private bool IsInteractionAvailable()
    {
        if (Screen is null)
            return false;

        return _interaction switch
        {
            InteractionKind.DragThumb => _thumb.IsPressed,
            InteractionKind.RepeatDecrease => _decrease.IsPressed,
            InteractionKind.RepeatIncrease => _increase.IsPressed,
            InteractionKind.RepeatTrack => _track.IsPressed,
            _ => false
        };
    }

    private bool CanRepeat()
    {
        if (!_hasPointerPosition)
            return false;

        return _interaction switch
        {
            InteractionKind.RepeatDecrease => IsPointerInside(_decrease),
            InteractionKind.RepeatIncrease => IsPointerInside(_increase),
            InteractionKind.RepeatTrack => CanRepeatTrack(),
            _ => false
        };
    }

    private void ResetRepeatDelay()
    {
        _hasRepeatBaseline = false;
        _repeatAccumulated = TimeSpan.Zero;
        _nextRepeatDelay = TimeSpan.FromMilliseconds(InitialRepeatDelayMilliseconds);
    }

    private bool IsPointerInside(ScrollBarPart part)
    {
        if (!part.IsEnabled)
            return false;

        var bounds = part.LayoutBounds;
        return _lastPointerPosition.X >= bounds.X &&
            _lastPointerPosition.Y >= bounds.Y &&
            _lastPointerPosition.X < bounds.X + bounds.Width &&
            _lastPointerPosition.Y < bounds.Y + bounds.Height;
    }

    private bool CanRepeatTrack()
    {
        if (!_track.IsEnabled || !IsPointerInside(_track))
            return false;

        var thumb = _thumb.LayoutBounds;
        var main = GetMainPoint(_lastPointerPosition, Orientation);
        var thumbStart = GetMainOrigin(thumb, Orientation);
        var thumbEnd = thumbStart + GetMain(thumb, Orientation);
        return _trackDirection < 0 ? main < thumbStart : main > thumbEnd;
    }

    private void Step(double delta)
    {
        if (delta == 0)
            return;

        Value += delta;
    }

    private double GetWheelDelta(UiPointerWheelEventArgs eventArgs)
    {
        if (Orientation != Orientation.Horizontal)
            return eventArgs.DeltaY;

        var delta = eventArgs.DeltaX;
        if ((eventArgs.Modifiers & KeyModifiers.Shift) != 0)
            delta += eventArgs.DeltaY;
        return delta;
    }

    private static double GetMainPoint(Point point, Orientation orientation) =>
        orientation == Orientation.Vertical ? point.Y : point.X;

    private enum InteractionKind
    {
        None,
        DragThumb,
        RepeatDecrease,
        RepeatIncrease,
        RepeatTrack
    }
}
