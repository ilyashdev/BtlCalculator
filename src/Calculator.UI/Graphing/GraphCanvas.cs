using Avalonia.Controls;
using Avalonia.Media;

namespace Calculator.UI.Graphing;

/// <summary>The graph surface: draws the <see cref="Drawable"/> over its whole area. Pointer input is handled by the view.</summary>
public sealed class GraphCanvas : Control
{
    public GraphDrawable? Drawable { get; set; }

    public override void Render(DrawingContext context) => Drawable?.Draw(context);
}
