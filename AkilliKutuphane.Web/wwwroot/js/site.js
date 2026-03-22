(function initializeUiEnhancements() {
    const loader = document.getElementById("global-loader");
    const themeToggle = document.getElementById("theme-toggle");
    const body = document.body;

    const savedTheme = localStorage.getItem("theme");
    if (savedTheme) {
        body.setAttribute("data-bs-theme", savedTheme);
    }

    if (themeToggle) {
        themeToggle.addEventListener("click", function () {
            const currentTheme = body.getAttribute("data-bs-theme") || "light";
            const nextTheme = currentTheme === "light" ? "dark" : "light";
            body.setAttribute("data-bs-theme", nextTheme);
            localStorage.setItem("theme", nextTheme);
        });
    }

    document.querySelectorAll("form").forEach(function (form) {
        form.addEventListener("submit", function () {
            if (loader) {
                loader.classList.remove("d-none");
            }
        });
    });

    const toasts = document.querySelectorAll(".toast");
    toasts.forEach(function (toastElement) {
        const toast = new bootstrap.Toast(toastElement, { delay: 2800 });
        toast.show();
    });
})();
