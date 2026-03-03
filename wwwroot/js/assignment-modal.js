(function () {
    const assetInput = document.getElementById("assetSearchText");
    const assetHidden = document.getElementById("assetIdHidden");
    const assetList = document.getElementById("assetSuggestions");
    const assetBtn = document.getElementById("assetDropdownBtn");
    const assetClientError = document.getElementById("assetClientError");

    const receiverInput = document.getElementById("receiverSearchText");
    const receiverList = document.getElementById("receiverSuggestions");
    const receiverBtn = document.getElementById("receiverDropdownBtn");
    const staffHidden = document.getElementById("staffProfileIdHidden");
    const departmentHidden = document.getElementById("departmentHidden");
    const locationHidden = document.getElementById("locationHidden");
    const staffClientError = document.getElementById("staffClientError");

    const staffRadio = document.getElementById("targetStaff");
    const departmentRadio = document.getElementById("targetDepartment");
    const locationRadio = document.getElementById("targetLocation");
    const form = document.getElementById("assignmentCreateForm");

    if (!assetInput || !assetHidden || !assetList || !assetBtn ||
        !receiverInput || !receiverList || !receiverBtn ||
        !staffHidden || !departmentHidden || !locationHidden ||
        !staffRadio || !departmentRadio || !locationRadio || !form) {
        return;
    }

    function debounce(fn, wait) {
        let timer = null;
        return function () {
            const args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(null, args); }, wait);
        };
    }

    function escapeHtml(value) {
        return (value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function closeList(list) {
        list.classList.add("d-none");
        list.innerHTML = "";
        list.dataset.index = "-1";
        list.dataset.payload = "[]";
    }

    function currentTargetType() {
        if (departmentRadio.checked) return "Department";
        if (locationRadio.checked) return "Location";
        return "Staff";
    }

    function showError(el, show) {
        if (!el) return;
        el.classList.toggle("d-none", !show);
    }

    function createCombo(config) {
        const input = config.input;
        const hidden = config.hidden;
        const list = config.list;
        const btn = config.button;
        const endpointBuilder = config.endpointBuilder;
        const mapSelected = config.mapSelected;
        const shouldSearch = config.shouldSearch || function () { return true; };

        async function fetchResults(term) {
            if (!shouldSearch()) {
                closeList(list);
                return;
            }

            const endpoint = endpointBuilder(term);
            const response = await fetch(endpoint, { headers: { Accept: "application/json" } });
            if (!response.ok) {
                closeList(list);
                return;
            }

            const items = await response.json();
            if (!items || !items.length) {
                closeList(list);
                return;
            }

            list.innerHTML = items
                .map(function (item, index) {
                    return "<li class=\"typeahead-item\" role=\"option\" data-index=\"" + index + "\">" + escapeHtml(item.label || "") + "</li>";
                })
                .join("");
            list.dataset.payload = JSON.stringify(items);
            list.dataset.index = "-1";
            list.classList.remove("d-none");
        }

        const debouncedFetch = debounce(function (term) {
            fetchResults(term || "");
        }, 250);

        input.addEventListener("input", function () {
            hidden.value = "";
            debouncedFetch(input.value.trim());
        });

        input.addEventListener("focus", function () {
            fetchResults(input.value.trim());
        });

        btn.addEventListener("click", function (e) {
            e.preventDefault();
            if (!shouldSearch()) return;

            if (!list.classList.contains("d-none")) {
                closeList(list);
                return;
            }
            fetchResults(input.value.trim());
            input.focus();
        });

        input.addEventListener("keydown", function (event) {
            if (list.classList.contains("d-none")) return;

            const items = Array.from(list.querySelectorAll(".typeahead-item"));
            if (!items.length) return;

            let index = parseInt(list.dataset.index || "-1", 10);

            if (event.key === "ArrowDown") {
                event.preventDefault();
                index = Math.min(index + 1, items.length - 1);
            } else if (event.key === "ArrowUp") {
                event.preventDefault();
                index = Math.max(index - 1, 0);
            } else if (event.key === "Enter") {
                if (index >= 0) {
                    event.preventDefault();
                    items[index].click();
                }
                return;
            } else if (event.key === "Escape") {
                closeList(list);
                return;
            } else {
                return;
            }

            list.dataset.index = String(index);
            items.forEach(function (el, i) {
                el.classList.toggle("active", i === index);
            });
        });

        list.addEventListener("mousedown", function (event) {
            const itemElement = event.target.closest(".typeahead-item");
            if (!itemElement) return;

            const index = parseInt(itemElement.dataset.index || "-1", 10);
            const payload = JSON.parse(list.dataset.payload || "[]");
            if (index < 0 || index >= payload.length) return;

            const selected = payload[index];
            input.value = selected.label || "";
            hidden.value = selected.id || "";
            mapSelected(selected);
            closeList(list);
        });
    }

    createCombo({
        input: assetInput,
        hidden: assetHidden,
        list: assetList,
        button: assetBtn,
        endpointBuilder: function (term) {
            return "/api/lookups/assets?q=" + encodeURIComponent(term);
        },
        mapSelected: function () {
            showError(assetClientError, false);
        }
    });

    createCombo({
        input: receiverInput,
        hidden: staffHidden,
        list: receiverList,
        button: receiverBtn,
        shouldSearch: function () {
            return currentTargetType() === "Staff";
        },
        endpointBuilder: function (term) {
            return "/api/lookups/staff?q=" + encodeURIComponent(term);
        },
        mapSelected: function () {
            departmentHidden.value = "";
            locationHidden.value = "";
            showError(staffClientError, false);
        }
    });

    function applyReceiverMode(resetInput) {
        const type = currentTargetType();
        closeList(receiverList);

        if (type === "Staff") {
            receiverBtn.classList.remove("d-none");
            receiverInput.placeholder = "Search staff by name or employee number...";
            departmentHidden.value = "";
            locationHidden.value = "";
            if (resetInput) receiverInput.value = "";
            return;
        }

        receiverBtn.classList.add("d-none");
        receiverInput.placeholder = type === "Department"
            ? "Type department..."
            : "Type location...";
        staffHidden.value = "";
        if (resetInput) receiverInput.value = "";
        showError(staffClientError, false);
    }

    [staffRadio, departmentRadio, locationRadio].forEach(function (radio) {
        radio.addEventListener("change", function () {
            applyReceiverMode(true);
        });
    });

    receiverInput.addEventListener("input", function () {
        if (currentTargetType() !== "Staff") {
            if (currentTargetType() === "Department") {
                departmentHidden.value = receiverInput.value.trim();
                locationHidden.value = "";
            } else if (currentTargetType() === "Location") {
                locationHidden.value = receiverInput.value.trim();
                departmentHidden.value = "";
            }
        }
    });

    document.addEventListener("click", function (event) {
        if (!event.target.closest(".typeahead-wrap")) {
            closeList(assetList);
            closeList(receiverList);
        }
    });

    form.addEventListener("submit", function (event) {
        showError(assetClientError, false);
        showError(staffClientError, false);

        const type = currentTargetType();

        if (!assetHidden.value) {
            event.preventDefault();
            showError(assetClientError, true);
        }

        if (type === "Staff") {
            departmentHidden.value = "";
            locationHidden.value = "";
            if (!staffHidden.value) {
                event.preventDefault();
                showError(staffClientError, true);
            }
        } else if (type === "Department") {
            staffHidden.value = "";
            departmentHidden.value = receiverInput.value.trim();
            locationHidden.value = "";
        } else {
            staffHidden.value = "";
            locationHidden.value = receiverInput.value.trim();
            departmentHidden.value = "";
        }
    });

    applyReceiverMode(false);
})();
