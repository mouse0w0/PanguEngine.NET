using System.ComponentModel;
using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class CanvasTests
{
    [Fact]
    public void PositionPropertiesExposeAttachedMetadata()
    {
        Assert.Equal("Left", Canvas.LeftProperty.Name);
        Assert.Equal(typeof(Canvas), Canvas.LeftProperty.OwnerType);
        Assert.Equal(typeof(UiNode), Canvas.LeftProperty.TargetType);
        Assert.Equal(typeof(double), Canvas.LeftProperty.ValueType);
        Assert.True(double.IsNaN(Canvas.LeftProperty.DefaultValue));
        Assert.Equal(UiPropertyInvalidation.Arrange, Canvas.LeftProperty.Invalidation);

        Assert.Equal("Top", Canvas.TopProperty.Name);
        Assert.Equal(typeof(Canvas), Canvas.TopProperty.OwnerType);
        Assert.Equal(typeof(UiNode), Canvas.TopProperty.TargetType);
        Assert.Equal(typeof(double), Canvas.TopProperty.ValueType);
        Assert.True(double.IsNaN(Canvas.TopProperty.DefaultValue));
        Assert.Equal(UiPropertyInvalidation.Arrange, Canvas.TopProperty.Invalidation);

        Assert.Equal("Right", Canvas.RightProperty.Name);
        Assert.Equal(typeof(Canvas), Canvas.RightProperty.OwnerType);
        Assert.Equal(typeof(UiNode), Canvas.RightProperty.TargetType);
        Assert.Equal(typeof(double), Canvas.RightProperty.ValueType);
        Assert.True(double.IsNaN(Canvas.RightProperty.DefaultValue));
        Assert.Equal(UiPropertyInvalidation.Arrange, Canvas.RightProperty.Invalidation);

        Assert.Equal("Bottom", Canvas.BottomProperty.Name);
        Assert.Equal(typeof(Canvas), Canvas.BottomProperty.OwnerType);
        Assert.Equal(typeof(UiNode), Canvas.BottomProperty.TargetType);
        Assert.Equal(typeof(double), Canvas.BottomProperty.ValueType);
        Assert.True(double.IsNaN(Canvas.BottomProperty.DefaultValue));
        Assert.Equal(UiPropertyInvalidation.Arrange, Canvas.BottomProperty.Invalidation);
    }

    [Fact]
    public void PositionAccessorsUseTheUiPropertyPath()
    {
        var node = new TestNode();
        var changes = 0;
        var rightChanges = 0;
        node.Subscribe(Canvas.LeftProperty, (_, _) => changes++);
        node.Subscribe(Canvas.RightProperty, (_, _) => rightChanges++);

        Assert.True(double.IsNaN(Canvas.GetLeft(node)));
        Assert.True(double.IsNaN(Canvas.GetTop(node)));
        Assert.True(double.IsNaN(Canvas.GetRight(node)));
        Assert.True(double.IsNaN(Canvas.GetBottom(node)));
        Assert.Throws<ArgumentNullException>(() => Canvas.GetLeft(null!));
        Assert.Throws<ArgumentNullException>(() => Canvas.SetLeft(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Canvas.GetTop(null!));
        Assert.Throws<ArgumentNullException>(() => Canvas.SetTop(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Canvas.GetRight(null!));
        Assert.Throws<ArgumentNullException>(() => Canvas.SetRight(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Canvas.GetBottom(null!));
        Assert.Throws<ArgumentNullException>(() => Canvas.SetBottom(null!, 0));

        Canvas.SetLeft(node, 5);
        Canvas.SetLeft(node, 5);
        Canvas.SetTop(node, -3);
        Canvas.SetRight(node, -8);
        Canvas.SetRight(node, -8);
        Canvas.SetBottom(node, 12);

        Assert.Equal(5, Canvas.GetLeft(node));
        Assert.Equal(-3, Canvas.GetTop(node));
        Assert.Equal(-8, Canvas.GetRight(node));
        Assert.Equal(12, Canvas.GetBottom(node));
        Assert.Equal(1, changes);
        Assert.Equal(1, rightChanges);

        node.ClearValue(Canvas.LeftProperty);
        node.ClearValue(Canvas.RightProperty);
        node.ClearValue(Canvas.BottomProperty);
        Assert.True(double.IsNaN(Canvas.GetLeft(node)));
        Assert.True(double.IsNaN(Canvas.GetRight(node)));
        Assert.True(double.IsNaN(Canvas.GetBottom(node)));
    }

    [Fact]
    public void MeasureGivesChildrenInfiniteSpaceAndDoesNotSizeToThem()
    {
        var canvas = new Canvas { Padding = new Thickness(2, 3, 4, 5) };
        var child = new TestNode { CoreDesiredSize = new Size(40, 30) };
        Canvas.SetLeft(child, 100);
        Canvas.SetTop(child, -20);
        canvas.Children.Add(child);

        canvas.Measure(new Size(200, 100));

        Assert.Equal(Size.Infinite, child.LastMeasureConstraint);
        Assert.Equal(new Size(6, 8), canvas.DesiredSize);

        var empty = new Canvas { Padding = canvas.Padding };
        empty.Measure(new Size(200, 100));
        Assert.Equal(canvas.DesiredSize, empty.DesiredSize);
    }

    [Fact]
    public void CanvasOuterLayoutPropertiesStillControlDesiredSize()
    {
        var canvas = new Canvas
        {
            Width = 50,
            Height = 30,
            Margin = new Thickness(2)
        };
        canvas.Children.Add(new TestNode { CoreDesiredSize = new Size(500, 400) });

        canvas.Measure(new Size(100, 100));

        Assert.Equal(new Size(54, 34), canvas.DesiredSize);
    }

    [Fact]
    public void ArrangePositionsTheChildMarginBoxFromContentBounds()
    {
        var canvas = new Canvas { Padding = new Thickness(10, 20, 0, 0) };
        var child = new TestNode
        {
            CoreDesiredSize = new Size(30, 15),
            Margin = new Thickness(2, 3, 4, 5)
        };
        Canvas.SetLeft(child, -5);
        Canvas.SetTop(child, 7);
        canvas.Children.Add(child);
        canvas.Measure(new Size(200, 100));

        canvas.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(new Rect(7, 30, 30, 15), child.LayoutBounds);
        Assert.Equal(new Size(30, 15), child.LastArrangeSize);
        Assert.Equal(new Size(10, 20), canvas.DesiredSize);
    }

    [Fact]
    public void UnspecifiedAndOutOfBoundsPositionsArrangeWithoutSizingCanvas()
    {
        var canvas = new Canvas();
        var unspecified = new TestNode { CoreDesiredSize = new Size(10, 5) };
        var outside = new TestNode { CoreDesiredSize = new Size(20, 10) };
        Canvas.SetLeft(outside, 150);
        Canvas.SetTop(outside, -40);
        canvas.Children.Add(unspecified);
        canvas.Children.Add(outside);
        canvas.Measure(new Size(100, 100));

        canvas.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(new Rect(0, 0, 10, 5), unspecified.LayoutBounds);
        Assert.Equal(new Rect(150, -40, 20, 10), outside.LayoutBounds);
        Assert.Equal(Size.Zero, canvas.DesiredSize);
    }

    [Fact]
    public void RightAndBottomPositionTheChildMarginBoxFromContentEdges()
    {
        var canvas = new Canvas { Padding = new Thickness(10, 20, 30, 40) };
        var child = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Margin = new Thickness(2, 3, 4, 5)
        };
        Canvas.SetRight(child, 7);
        Canvas.SetBottom(child, 4);
        canvas.Children.Add(child);
        canvas.Measure(new Size(200, 100));

        canvas.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(new Rect(139, 41, 20, 10), child.LayoutBounds);
        Assert.Equal(new Size(20, 10), child.LastArrangeSize);
    }

    [Fact]
    public void LeftAndTopTakePriorityWithoutStretchingTheChild()
    {
        var canvas = new Canvas();
        var child = new TestNode { CoreDesiredSize = new Size(10, 5) };
        Canvas.SetLeft(child, 7);
        Canvas.SetRight(child, double.PositiveInfinity);
        Canvas.SetTop(child, 9);
        Canvas.SetBottom(child, double.NegativeInfinity);
        canvas.Children.Add(child);
        canvas.Measure(new Size(100, 50));

        canvas.Arrange(new Rect(0, 0, 100, 50));

        Assert.Equal(new Rect(7, 9, 10, 5), child.LayoutBounds);
        Assert.Equal(new Size(10, 5), child.LastArrangeSize);
    }

    [Fact]
    public void NegativeRightAndBottomCanPositionBeyondContentEdges()
    {
        var canvas = new Canvas();
        var child = new TestNode { CoreDesiredSize = new Size(10, 5) };
        Canvas.SetRight(child, -10);
        Canvas.SetBottom(child, -5);
        canvas.Children.Add(child);
        canvas.Measure(new Size(100, 50));

        canvas.Arrange(new Rect(0, 0, 100, 50));

        Assert.Equal(new Rect(100, 50, 10, 5), child.LayoutBounds);
        Assert.Equal(Size.Zero, canvas.DesiredSize);
    }

    [Theory]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    public void SelectedNonFiniteTrailingPositionFailsBeforeAnyChildArrange(
        bool horizontal,
        double position)
    {
        var canvas = new Canvas();
        var child = new TestNode { CoreDesiredSize = new Size(10, 10) };
        if (horizontal)
            Canvas.SetRight(child, position);
        else
            Canvas.SetBottom(child, position);
        canvas.Children.Add(child);
        canvas.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, 100, 100)));

        Assert.Equal(0, child.ArrangeCount);
    }

    [Theory]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    public void InitialNonFiniteLeadingPositionFailsBeforeAnyChildArrange(
        bool vertical,
        double position)
    {
        var canvas = new Canvas();
        var first = new TestNode { CoreDesiredSize = new Size(10, 10) };
        var second = new TestNode { CoreDesiredSize = new Size(10, 10) };
        if (vertical)
            Canvas.SetTop(second, position);
        else
            Canvas.SetLeft(second, position);
        canvas.Children.Add(first);
        canvas.Children.Add(second);
        canvas.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, 100, 100)));

        Assert.Equal(0, first.ArrangeCount);
        Assert.Equal(0, second.ArrangeCount);
        Assert.False(canvas.IsArrangeValid);
    }

    [Fact]
    public void ChildCollectionChangedDuringArrangeFailsFastAndInvalidatesCanvasPass()
    {
        var canvas = new Canvas();
        var first = new TestNode { CoreDesiredSize = new Size(10, 10) };
        var second = new TestNode { CoreDesiredSize = new Size(10, 10) };
        first.ArrangeAction = () => canvas.Children.Add(new TestNode());
        canvas.Children.Add(first);
        canvas.Children.Add(second);
        canvas.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, 100, 100)));

        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(0, second.ArrangeCount);
        Assert.False(canvas.IsArrangeValid);
    }

    [Fact]
    public void SlotOriginOverflowFailsBeforeAnyChildArrange()
    {
        var canvas = new Canvas
        {
            Padding = new Thickness(double.MaxValue, 0, 0, 0)
        };
        var child = new TestNode { CoreDesiredSize = new Size(10, 10) };
        Canvas.SetLeft(child, double.MaxValue);
        canvas.Children.Add(child);
        canvas.Measure(new Size(double.MaxValue, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, double.MaxValue, 100)));

        Assert.Equal(0, child.ArrangeCount);
        Assert.False(canvas.IsArrangeValid);
    }

    [Fact]
    public void PositionChangedByEarlierChildIsRecheckedBeforeLaterArrange()
    {
        var canvas = new Canvas();
        var first = new TestNode { CoreDesiredSize = new Size(10, 10) };
        var second = new TestNode { CoreDesiredSize = new Size(10, 10) };
        first.ArrangeAction = () => Canvas.SetTop(second, double.NegativeInfinity);
        canvas.Children.Add(first);
        canvas.Children.Add(second);
        canvas.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, 100, 100)));

        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(0, second.ArrangeCount);
        Assert.False(canvas.IsArrangeValid);
    }

    [Fact]
    public void PositionPriorityIsReevaluatedBeforeEachChildArrange()
    {
        var canvas = new Canvas();
        var first = new TestNode { CoreDesiredSize = new Size(10, 10) };
        var second = new TestNode { CoreDesiredSize = new Size(10, 10) };
        Canvas.SetLeft(second, 5);
        Canvas.SetRight(second, double.PositiveInfinity);
        first.ArrangeAction = () => second.ClearValue(Canvas.LeftProperty);
        canvas.Children.Add(first);
        canvas.Children.Add(second);
        canvas.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            canvas.Arrange(new Rect(0, 0, 100, 100)));

        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(0, second.ArrangeCount);
    }

    [Fact]
    public void PositionChangeInvalidatesArrangeButNotMeasure()
    {
        var root = new TestPanel();
        var canvas = new Canvas();
        var child = new TestNode { CoreDesiredSize = new Size(10, 10) };
        canvas.Children.Add(child);
        root.Children.Add(canvas);
        root.Measure(new Size(100, 100));
        root.Arrange(new Rect(0, 0, 100, 100));

        Canvas.SetLeft(child, 5);

        Assert.True(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.True(canvas.IsMeasureValid);
        Assert.False(canvas.IsArrangeValid);
        Assert.True(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void ActivePositionPathsRejectWrongThreadWithoutPartialState()
    {
        var node = new TestNode();
        Canvas.SetLeft(node, 4);
        var screen = new UiScreen(node);
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 100, 100));
        screen.Open();

        var setterError = RunOnBackgroundThread(() =>
            Record.Exception(() => Canvas.SetLeft(node, 8)));
        var setValueError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.SetValue(Canvas.LeftProperty, 8)));
        var clearError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.ClearValue(Canvas.LeftProperty)));
        var source = new PositionSource { Position = 8 };
        var bindError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.Bind(Canvas.LeftProperty, source, item => item.Position)));

        Assert.IsType<InvalidOperationException>(setterError);
        Assert.IsType<InvalidOperationException>(setValueError);
        Assert.IsType<InvalidOperationException>(clearError);
        Assert.IsType<InvalidOperationException>(bindError);
        Assert.Equal(4, Canvas.GetLeft(node));
        Assert.False(node.IsBound(Canvas.LeftProperty));
        Assert.True(node.IsMeasureValid);
        Assert.True(node.IsArrangeValid);

        node.Bind(Canvas.LeftProperty, source, item => item.Position);
        var updateError = RunOnBackgroundThread(() =>
            Record.Exception(() => source.Position = 12));

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.Equal(8, Canvas.GetLeft(node));
        Assert.True(node.IsBound(Canvas.LeftProperty));
        screen.Close();
    }

    private sealed class TestPanel : Panel
    {
    }

    private sealed class TestNode : UiNode
    {
        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal int ArrangeCount { get; private set; }
        internal Action? ArrangeAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCount++;
            LastArrangeSize = finalSize;
            ArrangeAction?.Invoke();
        }
    }

    private sealed class PositionSource : INotifyPropertyChanged
    {
        private double _position;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Position
        {
            get => _position;
            set
            {
                if (_position.Equals(value))
                    return;

                _position = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
            }
        }
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }
}
