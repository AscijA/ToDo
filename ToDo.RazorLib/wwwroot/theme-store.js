window.themeStore = {
    set: function (key, value) {
        localStorage.setItem(key, value);
    },
    get: function (key) {
        return localStorage.getItem(key);
    },
    persist: function (primary, background, isDark) {
        localStorage.setItem('theme_primary', primary);
        localStorage.setItem('theme_background', background);
        localStorage.setItem('theme_isDark', isDark);
    }
};