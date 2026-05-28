(function (windowObj, documentObj) {
  "use strict";

  const selector = 'input[type="date"]:not([data-app-calendar="false"]), input[data-app-calendar="true"]';

  const buildOptions = (input) => {
    const dateFormat = input.dataset.calendarFormat || "Y-m-d";
    const hasValue = typeof input.value === "string" && input.value.trim().length > 0;

    const options = {
      dateFormat,
      allowInput: true,
      disableMobile: true,
      monthSelectorType: "static",
      prevArrow: "<span aria-hidden='true'>&lsaquo;</span>",
      nextArrow: "<span aria-hidden='true'>&rsaquo;</span>"
    };

    // If field is empty, initialize with today's date.
    if (!hasValue) {
      options.defaultDate = new Date();
    }

    return options;
  };

  const initInput = (input) => {
    if (!(input instanceof HTMLInputElement)) return;
    if (typeof windowObj.flatpickr !== "function") return;
    if (input._flatpickr) return;

    windowObj.flatpickr(input, buildOptions(input));
  };

  const refresh = (root = documentObj) => {
    if (!root || typeof root.querySelectorAll !== "function") return;
    root.querySelectorAll(selector).forEach(initInput);
  };

  documentObj.addEventListener("DOMContentLoaded", () => refresh(documentObj));

  windowObj.AppCalendar = {
    refresh,
    initInput
  };
})(window, document);
