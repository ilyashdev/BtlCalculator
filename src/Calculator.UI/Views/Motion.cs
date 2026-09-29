using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Calculator.UI.Views;

/// <summary>
/// The short transitions of the app, with the Fluent "fast" timing of the original (167 ms): panels fade in while
/// moving up a little, panes slide, knobs glide. The state (visibility, final position) is set at once; only the look
/// is animated, and every transition ends in the rest state (opacity 1, no offset). Hiding ends by collapsing the view.
/// </summary>
/// <remarks>
/// Transitions are played frame by frame on the frames the window renders (TopLevel.RequestAnimationFrame); the offset
/// is a <see cref="TranslateTransform"/> as the view's render transform. Avalonia's own animations from code need a
/// visual as the target and have no animator for the render transform, so they cannot move a view.
/// </remarks>
public static class Motion
{
    public static readonly TimeSpan FastDuration = TimeSpan.FromMilliseconds(167);
    private const double EntranceOffset = 12;

    // The running transition of each view; a new one cancels it.
    private static readonly ConditionalWeakTable<Visual, CancellationTokenSource> Running = [];

    /// <summary>Shows a panel: fades in while moving up a little.</summary>
    public static void Show(Visual view)
    {
        if (view.IsVisible && !Running.TryGetValue(view, out _))
        {
            return;
        }

        view.IsVisible = true;
        Start(Play(view, opacity: (0, 1), offsetX: (0, 0), offsetY: (EntranceOffset, 0), entering: true));
    }

    /// <summary>Hides a panel: fades out, then collapses it.</summary>
    public static void Hide(Visual view) => Start(Leave(view, opacity: 0, offsetX: 0));

    public static void SetVisible(Visual view, bool visible)
    {
        if (visible)
        {
            Show(view);
        }
        else
        {
            Hide(view);
        }
    }

    /// <summary>Fades a view in from transparent (a mode view that replaced another one).</summary>
    public static void FadeIn(Visual view) => Start(Play(view, opacity: (0, 1), offsetX: (0, 0), offsetY: (0, 0), entering: true));

    /// <summary>
    /// Opens or closes a pane that slides in from the start edge (the navigation pane): the left edge, or the right
    /// one in right-to-left languages, where the page is mirrored.
    /// </summary>
    public static void SlideFromStart(Control pane, Visual scrim, bool open)
    {
        double hidden = Services.LanguageService.IsRightToLeft ? pane.Width : -pane.Width;
        if (open)
        {
            pane.IsVisible = true;
            Start(Play(pane, opacity: (1, 1), offsetX: (hidden, 0), offsetY: (0, 0), entering: true));
            scrim.IsVisible = true;
            FadeIn(scrim);
            return;
        }

        scrim.IsVisible = false;
        Start(Leave(pane, opacity: 1, offsetX: hidden));
    }

    /// <summary>Glides a view from <paramref name="distance"/> away from its layout position back to it (a knob).</summary>
    public static void GlideFrom(Visual view, double distance) =>
        Start(Play(view, opacity: (1, 1), offsetX: (distance, 0), offsetY: (0, 0), entering: true));

    private static async Task Leave(Visual view, double opacity, double offsetX)
    {
        if (!view.IsVisible)
        {
            return;
        }

        if (await Play(view, opacity: (1, opacity), offsetX: (0, offsetX), offsetY: (0, 0), entering: false))
        {
            view.IsVisible = false;
            Apply(view, opacity: 1, offsetX: 0, offsetY: 0);
        }
    }

    /// <summary>Plays one transition frame by frame, on the frames the window renders.</summary>
    /// <returns>True when the transition ran to its end, false when a newer one replaced it.</returns>
    private static async Task<bool> Play(
        Visual view,
        (double From, double To) opacity,
        (double From, double To) offsetX,
        (double From, double To) offsetY,
        bool entering)
    {
        if (Running.TryGetValue(view, out CancellationTokenSource? previous))
        {
            await previous.CancelAsync();
        }

        // The first frame at once, so the view never shows its final state before the transition.
        Apply(view, opacity.From, offsetX.From, offsetY.From);
        if (TopLevel.GetTopLevel(view) is not TopLevel topLevel)
        {
            // Not on screen: nothing to animate.
            Apply(view, opacity.To, offsetX.To, offsetY.To);
            return true;
        }

        using var cancellation = new CancellationTokenSource();
        Running.AddOrUpdate(view, cancellation);
        Easing easing = entering ? new CubicEaseOut() : new CubicEaseIn();
        var completion = new TaskCompletionSource<bool>();
        TimeSpan? start = null;

        void OnFrame(TimeSpan time)
        {
            if (cancellation.IsCancellationRequested)
            {
                completion.TrySetResult(false);
                return;
            }

            start ??= time;
            double progress = Math.Clamp((time - start.Value) / FastDuration, 0, 1);
            double eased = easing.Ease(progress);
            Apply(view, Lerp(opacity, eased), Lerp(offsetX, eased), Lerp(offsetY, eased));
            if (progress >= 1)
            {
                completion.TrySetResult(true);
                return;
            }

            topLevel.RequestAnimationFrame(OnFrame);
        }

        topLevel.RequestAnimationFrame(OnFrame);
        bool finished = await completion.Task;
        if (Running.TryGetValue(view, out CancellationTokenSource? current) && current == cancellation)
        {
            Running.Remove(view);
        }

        return finished;
    }

    private static double Lerp((double From, double To) values, double progress) => values.From + ((values.To - values.From) * progress);

    private static void Apply(Visual view, double opacity, double offsetX, double offsetY)
    {
        view.Opacity = opacity;
        if (view.RenderTransform is not TranslateTransform offset)
        {
            offset = new TranslateTransform();
            view.RenderTransform = offset;
        }

        offset.X = offsetX;
        offset.Y = offsetY;
    }
    /// <summary>
    /// Starts a transition without waiting for it. A failure must not vanish with the task: it is thrown on the UI
    /// thread, where it reaches the app's unhandled exception handling.
    /// </summary>
    private static void Start(Task transition) =>
        transition.ContinueWith(
            failed => Dispatcher.UIThread.Post(() => failed.Exception!.Handle(_ => false)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
}
