namespace ToDo.Maui.Windows;

public partial class MainPage : ContentPage {
    public MainPage() {
        InitializeComponent();

#if WINDOWS
        blazorWebView.HandlerChanged += (_, _) => {
            if (blazorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView) {
                webView.AllowDrop = true;
            }
        };
#endif
    }
}
