(function () {
  const DEFAULT_DELAY = 2500;
  const TITLES = {
    success: "Exito",
    danger: "Error",
    error: "Error",
    warning: "Aviso",
    info: "Info"
  };

  const normalizeVariant = (variant) => {
    if (variant === "error") return "danger";
    if (["success", "danger", "warning", "info"].includes(variant)) return variant;
    return "success";
  };

  const getHost = () => {
    let host = document.querySelector("[data-app-toast-host]");
    if (!host) {
      host = document.createElement("div");
      host.setAttribute("data-app-toast-host", "");
      host.setAttribute("aria-live", "polite");
      host.setAttribute("aria-atomic", "true");
      document.body.appendChild(host);
    }

    host.className = "app-toast-host toast-container position-fixed top-0 end-0 p-3";
    return host;
  };

  const show = (message, variant = "success", options = {}) => {
    const tone = normalizeVariant(variant);
    const delay = Number.isFinite(options.delay) ? options.delay : DEFAULT_DELAY;
    const title = options.title || TITLES[variant] || TITLES[tone] || "Aviso";
    const host = getHost();
    const toast = document.createElement("div");

    toast.className = `app-toast toast text-bg-${tone} border-0 border-start border-4 border-${tone}`;
    toast.setAttribute("role", tone === "danger" ? "alert" : "status");
    toast.setAttribute("aria-live", tone === "danger" ? "assertive" : "polite");
    toast.setAttribute("aria-atomic", "true");

    const header = document.createElement("div");
    header.className = `toast-header text-bg-${tone} border-0`;

    const titleEl = document.createElement("strong");
    titleEl.className = "me-auto";
    titleEl.textContent = title;

    const close = document.createElement("button");
    close.type = "button";
    close.className = "btn-close btn-close-white";
    close.setAttribute("data-bs-dismiss", "toast");
    close.setAttribute("aria-label", "Cerrar");

    const body = document.createElement("div");
    body.className = "toast-body";
    const messageEl = document.createElement("strong");
    messageEl.className = "d-block mb-1";
    messageEl.textContent = message ?? "";

    header.append(titleEl, close);
    body.appendChild(messageEl);
    toast.append(header, body);
    host.appendChild(toast);

    if (window.bootstrap?.Toast) {
      const bsToast = window.bootstrap.Toast.getOrCreateInstance(toast, { delay });
      toast.addEventListener("hidden.bs.toast", () => toast.remove());
      bsToast.show();
      return toast;
    }

    requestAnimationFrame(() => toast.classList.add("show"));
    setTimeout(() => toast.remove(), delay);
    return toast;
  };

  window.AppToast = { show };
})();
