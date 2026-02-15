window.toolhubTheme = {
    get: function () {
        try { return localStorage.getItem("toolhub.theme") || "light"; }
        catch { return "dark"; }
    },
    set: function (theme) {
        try { localStorage.setItem("toolhub.theme", theme); } catch { }

        const isLight = theme === "light";
        document.body.classList.toggle("light", isLight);
    }
};
