using MudBlazor;

namespace ToDo.RazorLib;

public static class Themes {
    public static readonly MudTheme OneDarkPro = new() {
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
        },
        PaletteLight = new PaletteLight {
            Primary = "#C678DD",
            Secondary = "#61AFEF",
            Tertiary = "#D19A66",
            Info = "#56B6C2",
            Success = "#98C379",
            Warning = "#E5C07B",
            Error = "#E06C75",
            AppbarBackground = "#FFFFFF",
            Background = "#F5F5F5",
            Surface = "#FFFFFF",
            TextPrimary = "#282C34",
            TextSecondary = "rgba(40,44,52,0.70)",
            LinesDefault = "rgba(40,44,52,0.12)",
        }
    };

    public static readonly MudTheme Blue = new() {
        PaletteDark = new PaletteDark {
            Primary = "#2196F3",
            Secondary = "#03A9F4",
            Background = "#0D1117",
            Surface = "#161B22",
        },
        PaletteLight = new PaletteLight {
            Primary = "#2196F3",
            Secondary = "#03A9F4",
            Background = "#F0F2F5",
            Surface = "#FFFFFF",
        }
    };
    public static readonly MudTheme Green = new() {
        PaletteDark = new PaletteDark {
            Primary = "#4CAF50",
            Secondary = "#81C784",
            Background = "#0D1117",
            Surface = "#161B22",
        },
        PaletteLight = new PaletteLight {
            Primary = "#4CAF50",
            Secondary = "#81C784",
            Background = "#F6FAF6",
            Surface = "#FFFFFF",
        }
    };

    public static readonly MudTheme Solarized = new() {
        PaletteDark = new PaletteDark {
            Primary = "#268bd2",
            Secondary = "#2aa198",
            Background = "#002b36",
            Surface = "#073642",
            AppbarBackground = "#002b36",
            DrawerBackground = "#073642",
            TextPrimary = "#839496",
            TextSecondary = "#586e75",
            LinesDefault = "#586e75",
            Divider = "#586e75",
            Success = "#859900",
            Warning = "#b58900",
            Error = "#dc322f",
            Info = "#2aa198",
        },
        PaletteLight = new PaletteLight {
            Primary = "#268bd2",
            Secondary = "#2aa198",
            Background = "#fdf6e3",
            Surface = "#eee8d5",
            AppbarBackground = "#fdf6e3",
            DrawerBackground = "#eee8d5",
            TextPrimary = "#657b83",
            TextSecondary = "#93a1a1",
            LinesDefault = "#93a1a1",
            Divider = "#93a1a1",
            Success = "#859900",
            Warning = "#b58900",
            Error = "#dc322f",
            Info = "#2aa198",
        }
    };

    public static MudTheme CreateCustomTheme(string primary, string secondary, string background, string surface) {
        return new MudTheme {
            PaletteDark = new PaletteDark {
                Primary = primary,
                Secondary = secondary,
                Background = background,
                Surface = surface,
                TextPrimary = "#FFFFFF",
                TextSecondary = "rgba(255,255,255,0.70)",
                LinesDefault = "rgba(255,255,255,0.12)",
            },
            PaletteLight = new PaletteLight {
                Primary = primary,
                Secondary = secondary,
                Background = background,
                Surface = surface,
                TextPrimary = "#000000",
                TextSecondary = "rgba(0,0,0,0.70)",
                LinesDefault = "rgba(0,0,0,0.12)",
            }
        };
    }
}
