using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Raqmi.Client.Ui;

public static class ViewTransitions
{
    public static void FadeIn(FrameworkElement element)
    {
        element.Opacity = 0;
        element.RenderTransform = new TranslateTransform(0, 14);
        element.Visibility = Visibility.Visible;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        if (element.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }
    }

    public static void SwitchPanel(FrameworkElement hide, FrameworkElement show)
    {
        hide.Visibility = Visibility.Collapsed;
        FadeIn(show);
    }
}
