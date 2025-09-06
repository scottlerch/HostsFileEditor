using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace HostsFileEditor.Services;

public class AnimationService
{
    public async Task SlideInAsync(UIElement element, bool fromRight = true, double? offset = null)
    {
        if (element is not FrameworkElement fe)
        {
            return;
        }

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Translation");

        var width = offset ?? fe.ActualWidth;
        if (width <= 0)
        {
            width = 400;
        }
        var startX = (float)(fromRight ? width : -width);

        visual.Properties.InsertVector3("Translation", new Vector3(startX, 0, 0));

        var compositor = visual.Compositor;
        var anim = compositor.CreateVector3KeyFrameAnimation();
        anim.Duration = TimeSpan.FromMilliseconds(300);
        anim.InsertKeyFrame(0f, new Vector3(startX, 0, 0));
        anim.InsertKeyFrame(1f, new Vector3(0, 0, 0));

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Translation", anim);
        batch.End();
        var tcs = new TaskCompletionSource<bool>();
        batch.Completed += (_, __) => tcs.TrySetResult(true);
        await tcs.Task.ConfigureAwait(true);
    }

    public async Task SlideOutAsync(UIElement element, bool toRight = true, double? offset = null)
    {
        if (element is not FrameworkElement fe)
        {
            return;
        }

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Translation");

        var width = offset ?? fe.ActualWidth;
        if (width <= 0)
        {
            width = 400;
        }
        var endX = (float)(toRight ? width : -width);

        var compositor = visual.Compositor;
        var anim = compositor.CreateVector3KeyFrameAnimation();
        anim.Duration = TimeSpan.FromMilliseconds(300);
        anim.InsertKeyFrame(0f, new Vector3(0, 0, 0));
        anim.InsertKeyFrame(1f, new Vector3(endX, 0, 0));

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Translation", anim);
        batch.End();
        var tcs = new TaskCompletionSource<bool>();
        batch.Completed += (_, __) => tcs.TrySetResult(true);
        await tcs.Task.ConfigureAwait(true);
    }
}
