(function () {
  const SELECTOR = 'select:not([multiple]):not([data-app-select="false"])';
  const enhanced = new WeakSet();
  let openDropdown = null;

  const canEnhance = (select) => {
    if (!(select instanceof HTMLSelectElement)) return false;
    if (select.multiple) return false;
    const size = Number.parseInt(select.getAttribute("size") || "0", 10);
    return !Number.isFinite(size) || size <= 1;
  };

  const shouldAppendToBody = (select) => select?.dataset?.appSelectAppendToBody === "true";

  const getDropdownMenu = (dropdown) => dropdown?.__appSelectMenu || dropdown?.querySelector("[data-app-select-menu]");

  const getDropdownTrigger = (dropdown) => dropdown?.querySelector("[data-app-select-trigger]");

  const positionBodyMenu = (dropdown) => {
    if (!dropdown || !shouldAppendToBody(dropdown.__appSelectSelect)) return;

    const trigger = getDropdownTrigger(dropdown);
    const menu = getDropdownMenu(dropdown);
    if (!trigger || !menu) return;

    const rect = trigger.getBoundingClientRect();
    const spaceBelow = Math.max(window.innerHeight - rect.bottom - 12, 160);
    const spaceAbove = Math.max(rect.top - 12, 160);
    const openAbove = spaceBelow < 240 && spaceAbove > spaceBelow;
    const maxHeight = Math.min(288, openAbove ? spaceAbove : spaceBelow);

    menu.style.position = "fixed";
    menu.style.left = `${Math.round(rect.left)}px`;
    menu.style.width = `${Math.round(rect.width)}px`;
    menu.style.maxHeight = `${Math.max(160, Math.round(maxHeight))}px`;
    menu.style.top = openAbove
      ? `${Math.max(12, Math.round(rect.top - Math.min(maxHeight, menu.scrollHeight || 288) - 8))}px`
      : `${Math.round(rect.bottom + 8)}px`;
  };

  const closeDropdown = (dropdown) => {
    if (!dropdown) return;
    dropdown.classList.remove("is-open");
    const trigger = getDropdownTrigger(dropdown);
    const menu = getDropdownMenu(dropdown);
    trigger?.setAttribute("aria-expanded", "false");
    if (menu) {
      menu.hidden = true;
      menu.style.position = "";
      menu.style.top = "";
      menu.style.left = "";
      menu.style.width = "";
      menu.style.maxHeight = "";
      if (dropdown.contains(menu) === false) {
        dropdown.appendChild(menu);
      }
    }
    if (openDropdown === dropdown) {
      openDropdown = null;
    }
  };

  const closeOpenDropdown = () => closeDropdown(openDropdown);

  const openSelect = (dropdown) => {
    if (!dropdown) return;
    closeOpenDropdown();
    dropdown.classList.add("is-open");
    const trigger = getDropdownTrigger(dropdown);
    const menu = getDropdownMenu(dropdown);
    trigger?.setAttribute("aria-expanded", "true");
    if (menu) {
      menu.hidden = false;
      if (shouldAppendToBody(dropdown.__appSelectSelect) && menu.parentElement !== document.body) {
        document.body.appendChild(menu);
      }
      positionBodyMenu(dropdown);
      const selected = menu.querySelector(".is-selected") || menu.querySelector("[data-app-select-option]:not(:disabled)");
      selected?.scrollIntoView({ block: "nearest" });
    }
    openDropdown = dropdown;
  };

  const syncLabelFor = (select, trigger) => {
    if (!select.id) return;
    const triggerId = `${select.id}AppSelect`;
    trigger.id = triggerId;
    document.querySelectorAll(`label[for="${CSS.escape(select.id)}"]`).forEach((label) => {
      label.setAttribute("for", triggerId);
    });
  };

  const getSelectedOption = (select) => {
    return select.selectedOptions?.[0] || select.options[select.selectedIndex] || select.options[0] || null;
  };

  const render = (select, dropdown) => {
    const trigger = getDropdownTrigger(dropdown);
    const label = dropdown.querySelector("[data-app-select-label]");
    const menu = getDropdownMenu(dropdown);
    if (!trigger || !label || !menu) return;

    const selected = getSelectedOption(select);
    label.textContent = selected?.textContent?.trim() || "";
    trigger.disabled = select.disabled;
    trigger.classList.toggle("is-empty", !select.value);
    menu.innerHTML = "";

    Array.from(select.options).filter((option) => !option.hidden).forEach((option) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "app-select-option";
      button.setAttribute("data-app-select-option", "");
      button.setAttribute("role", "option");
      button.dataset.value = option.value;
      button.textContent = option.textContent?.trim() || "";
      button.disabled = option.disabled;

      const isSelected = option.selected;
      button.classList.toggle("is-selected", isSelected);
      button.setAttribute("aria-selected", isSelected ? "true" : "false");

      button.addEventListener("click", () => {
        if (button.disabled) return;
        select.value = option.value;
        select.dispatchEvent(new Event("change", { bubbles: true }));
        closeDropdown(dropdown);
        trigger.focus();
      });

      button.addEventListener("keydown", (event) => {
        const options = Array.from(menu.querySelectorAll("[data-app-select-option]:not(:disabled)"));
        const index = options.indexOf(button);
        if (event.key === "ArrowDown") {
          event.preventDefault();
          options[Math.min(index + 1, options.length - 1)]?.focus();
        } else if (event.key === "ArrowUp") {
          event.preventDefault();
          options[Math.max(index - 1, 0)]?.focus();
        } else if (event.key === "Escape") {
          event.preventDefault();
          closeDropdown(dropdown);
          trigger.focus();
        }
      });

      menu.appendChild(button);
    });
  };

  const enhanceSelect = (select) => {
    if (!canEnhance(select) || enhanced.has(select)) return;
    enhanced.add(select);
    select.dataset.appSelectEnhanced = "true";
    select.classList.add("app-select-native");

    const dropdown = document.createElement("div");
    dropdown.className = "app-select";
    dropdown.setAttribute("data-app-select", "");
    dropdown.__appSelectSelect = select;

    const trigger = document.createElement("button");
    trigger.type = "button";
    trigger.className = "app-select-trigger";
    trigger.setAttribute("data-app-select-trigger", "");
    trigger.setAttribute("aria-haspopup", "listbox");
    trigger.setAttribute("aria-expanded", "false");

    const label = document.createElement("span");
    label.setAttribute("data-app-select-label", "");

    const icon = document.createElement("span");
    icon.className = "app-select-icon";
    icon.setAttribute("aria-hidden", "true");
    icon.innerHTML = '<svg viewBox="0 0 20 20" focusable="false"><path fill="currentColor" d="M5.2 7.4 10 12.2l4.8-4.8 1.1 1.2-5.3 5.3a.85.85 0 0 1-1.2 0L4.1 8.6z"/></svg>';

    const menu = document.createElement("div");
    menu.className = "app-select-menu";
    menu.setAttribute("data-app-select-menu", "");
    menu.setAttribute("role", "listbox");
    menu.hidden = true;
    dropdown.__appSelectMenu = menu;

    trigger.append(label, icon);
    dropdown.append(trigger, menu);
    select.insertAdjacentElement("afterend", dropdown);
    syncLabelFor(select, trigger);
    render(select, dropdown);

    trigger.addEventListener("click", () => {
      if (dropdown.classList.contains("is-open")) {
        closeDropdown(dropdown);
      } else {
        openSelect(dropdown);
      }
    });

    trigger.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        openSelect(dropdown);
        const first = menu.querySelector("[data-app-select-option]:not(:disabled)");
        first?.focus();
      } else if (event.key === "Escape") {
        closeDropdown(dropdown);
      }
    });

    select.addEventListener("change", () => render(select, dropdown));

    const repositionOnViewportChange = () => {
      if (openDropdown === dropdown) {
        positionBodyMenu(dropdown);
      }
    };

    window.addEventListener("resize", repositionOnViewportChange);
    window.addEventListener("scroll", repositionOnViewportChange, true);

    const selectObserver = new MutationObserver(() => render(select, dropdown));
    selectObserver.observe(select, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["disabled", "hidden", "selected", "label", "value"]
    });
  };

  const refresh = (root = document) => {
    if (!root || typeof root.querySelectorAll !== "function") return;
    root.querySelectorAll(SELECTOR).forEach(enhanceSelect);
  };

  document.addEventListener("click", (event) => {
    const menu = getDropdownMenu(openDropdown);
    if (openDropdown && !openDropdown.contains(event.target) && !(menu && menu.contains(event.target))) {
      closeOpenDropdown();
    }
  });

  document.addEventListener("DOMContentLoaded", () => refresh(document));

  const documentObserver = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        if (!(node instanceof Element)) return;
        if (node.matches?.(SELECTOR)) {
          enhanceSelect(node);
        }
        refresh(node);
      });
    });
  });

  documentObserver.observe(document.documentElement, { childList: true, subtree: true });

  window.AppSelect = {
    refresh,
    enhance: enhanceSelect
  };
})();
