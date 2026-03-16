(function () {
    var body = document.body;
    var toggle = document.getElementById("sidebarCollapseToggle");
    var icon = document.getElementById("sidebarCollapseIcon");
    var storageKey = "assettracker.sidebar.collapsed";

    if (!body || !toggle || !icon) {
        return;
    }

    function setCollapsedState(isCollapsed) {
        body.classList.toggle("sidebar-collapsed", isCollapsed);
        icon.className = isCollapsed ? "bi bi-chevron-right me-2" : "bi bi-chevron-left me-2";
        toggle.setAttribute("aria-expanded", (!isCollapsed).toString());

        try {
            localStorage.setItem(storageKey, isCollapsed ? "1" : "0");
        } catch (_ignored) {
            // Ignore localStorage failures.
        }
    }

    var storedValue = null;
    try {
        storedValue = localStorage.getItem(storageKey);
    } catch (_ignored) {
        storedValue = null;
    }

    setCollapsedState(storedValue === "1");

    toggle.addEventListener("click", function () {
        setCollapsedState(!body.classList.contains("sidebar-collapsed"));
    });
})();

(function () {
    var searchableSelects = Array.prototype.slice.call(document.querySelectorAll("select.searchable-vendor"));
    if (searchableSelects.length === 0) {
        return;
    }

    function initTomSelect() {
        if (!window.TomSelect) {
            return;
        }

        searchableSelects.forEach(function (select) {
            if (select.tomselect) {
                return;
            }

            new TomSelect(select, {
                create: false,
                allowEmptyOption: true,
                searchField: ["text"],
                sortField: [
                    {
                        field: "text",
                        direction: "asc"
                    }
                ],
                placeholder: select.dataset.placeholder || "Search vendor...",
                plugins: {
                    clear_button: { title: "Clear selection" }
                }
            });
        });
    }

    var cssId = "tom-select-css";
    if (!document.getElementById(cssId)) {
        var css = document.createElement("link");
        css.id = cssId;
        css.rel = "stylesheet";
        css.href = "https://cdn.jsdelivr.net/npm/tom-select@2.3.1/dist/css/tom-select.bootstrap5.min.css";
        document.head.appendChild(css);
    }

    if (window.TomSelect) {
        initTomSelect();
        return;
    }

    var scriptId = "tom-select-js";
    var existingScript = document.getElementById(scriptId);
    if (existingScript) {
        existingScript.addEventListener("load", initTomSelect, { once: true });
        return;
    }

    var script = document.createElement("script");
    script.id = scriptId;
    script.src = "https://cdn.jsdelivr.net/npm/tom-select@2.3.1/dist/js/tom-select.complete.min.js";
    script.defer = true;
    script.addEventListener("load", initTomSelect, { once: true });
    document.body.appendChild(script);
})();
