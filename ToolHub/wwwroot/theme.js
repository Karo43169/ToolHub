window.toolhubTheme = {
    get: function () {
        try { return localStorage.getItem("toolhub.theme") || "dark"; }
        catch { return "dark"; }
    },
    set: function (theme) {
        try { localStorage.setItem("toolhub.theme", theme); } catch { }

        const isLight = theme === "light";
        document.documentElement.classList.toggle("light", isLight);
    }
};
