using AndroidApp = global::Android.App.Application;

namespace ToDo.Maui.Android;

public partial class MainPage : ContentPage {
    public MainPage() {
        InitializeComponent();
    }

    protected override void OnAppearing() {
        base.OnAppearing();
        ApplyStatusBarPadding();
    }

    private void ApplyStatusBarPadding() {
        var activity = Platform.CurrentActivity;
        if (activity?.Resources?.GetIdentifier("status_bar_height", "dimen", "android") is int resourceId && resourceId > 0) {
            var statusBarPixels = activity.Resources.GetDimensionPixelSize(resourceId);
            var density = activity.Resources.DisplayMetrics?.Density ?? 1f;
            var statusBarDp = statusBarPixels / density;
            Padding = new Thickness(0, statusBarDp, 0, 0);
        }
    }
}
