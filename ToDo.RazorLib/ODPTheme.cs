using MudBlazor;

namespace ToDo.RazorLib;

public static class ODPTheme {
    public static readonly MudTheme Theme = new() {
        PaletteDark = new PaletteDark {
            // Core brand colors
            Black = "#000000",
            White = "#FFFFFF",

            Primary = "#C678DD",               // One Dark Pro purple
            PrimaryContrastText = "#FFFFFF",

            Secondary = "#61AFEF",             // Blue
            SecondaryContrastText = "#FFFFFF",

            Tertiary = "#D19A66",              // Orange
            TertiaryContrastText = "#282C34",

            Info = "#56B6C2",                  // Cyan
            InfoContrastText = "#282C34",

            Success = "#98C379",               // Green
            SuccessContrastText = "#282C34",

            Warning = "#E5C07B",               // Yellow
            WarningContrastText = "#282C34",

            Error = "#E06C75",                 // Red
            ErrorContrastText = "#FFFFFF",

            Dark = "#21252B",
            DarkContrastText = "#FFFFFF",

            // Text
            TextPrimary = "#ABB2BF",
            TextSecondary = "rgba(171,178,191,0.70)",
            TextDisabled = "rgba(171,178,191,0.38)",

            // Actions
            ActionDefault = "rgba(171,178,191,0.70)",
            ActionDisabled = "rgba(171,178,191,0.30)",
            ActionDisabledBackground = "rgba(171,178,191,0.12)",

            // Layout surfaces
            Background = "#282C34",
            BackgroundGray = "#2C313C",
            Surface = "#21252B",

            DrawerBackground = "#282C34",
            DrawerText = "#ABB2BF",
            DrawerIcon = "#7F848E",

            AppbarBackground = "#21252B",
            AppbarText = "#ABB2BF",

            // Borders / lines / tables
            LinesDefault = "rgba(171,178,191,0.12)",
            LinesInputs = "rgba(171,178,191,0.35)",
            TableLines = "rgba(171,178,191,0.10)",
            TableStriped = "rgba(255,255,255,0.02)",
            TableHover = "rgba(255,255,255,0.04)",

            Divider = "rgba(171,178,191,0.12)",
            DividerLight = "rgba(171,178,191,0.06)",

            Skeleton = "rgba(171,178,191,0.08)",

            // Opacity behavior
            BorderOpacity = 1.0,
            HoverOpacity = 0.06,
            RippleOpacity = 0.10,
            RippleOpacitySecondary = 0.20,

            // Gray scale helpers
            GrayDefault = "#5C6370",
            GrayLight = "#7F848E",
            GrayLighter = "#ABB2BF",
            GrayDark = "#4B5263",
            GrayDarker = "#3E4451",

            // Overlays
            OverlayDark = "rgba(0,0,0,0.55)",
            OverlayLight = "rgba(255,255,255,0.08)"
        }
    };
}
